using FileSort.Common;
using FileSort.Engine.Chunks.Parsing;
using FileSort.Engine.Chunks.Reading;
using FileSort.Engine.Chunks.Sorting;
using FileSort.Engine.Lines;
using FileSort.Engine.Lines.Parsing;

namespace FileSort.Engine.Chunks;

/// <summary>
/// Makes the chunks of the input one after another (the first phase of the sort, up to the write): reads the bytes of a
/// chunk, cuts them into ranges and counts their lines, parses every range on its own thread, reports the first
/// problem of the file, and sorts the index in segments.
/// </summary>
internal sealed class ChunkMaker
{
    // The lines walked between two looks at the cancellation when a chunk is cut shorter.
    private const int CancellationInterval = 1 << 16;

    private readonly ChunkReader _reader;
    private readonly ChunkBuffer _buffer;
    private readonly ParallelWork _work;
    private readonly LineEnding _lineEnding;
    private readonly int _maxLineLength;
    private readonly int _segmentSize;

    public ChunkMaker(
        ChunkReader reader,
        ChunkBuffer buffer,
        ParallelWork work,
        LineEnding lineEnding,
        int maxLineLength,
        int segmentSize)
    {
        _reader = reader;
        _buffer = buffer;
        _work = work;
        _lineEnding = lineEnding;
        _maxLineLength = maxLineLength;
        _segmentSize = segmentSize;
    }

    /// <summary>The next chunk, sorted, or null when the input has been read to its end.</summary>
    /// <exception cref="InvalidLineException">A line of the chunk is not valid, and no line before it stops the
    /// sort.</exception>
    /// <exception cref="LineTooLongException">A line of the chunk is too long, and no line before it stops the
    /// sort.</exception>
    public SortedChunk? Next()
    {
        if (_reader.Read(_work.CancellationToken) is not ChunkBytes read)
        {
            return null;
        }

        (ChunkBytes bytes, LineRanges ranges, IndexLayout layout) = Layout(read);
        Parse(bytes, ranges, layout);
        Segment[] segments = Sort(ranges, layout);
        _reader.Advance(bytes.Length, ranges.TotalLines);
        return new SortedChunk(_buffer, bytes, layout.EntriesOffset, segments, ranges.TotalLines);
    }

    // The ranges of the chunk and the place of their entries. When the lines were shorter than expected and their
    // entries do not fit, the chunk ends earlier, at the line where lines and entries fill the buffer, and the rest
    // goes to the next chunk.
    private (ChunkBytes Bytes, LineRanges Ranges, IndexLayout Layout) Layout(ChunkBytes bytes)
    {
        LineRanges ranges = LineRanges.Split(
            _buffer.Bytes, bytes.Length, bytes.LastLineUnterminated, _work.Threads, _work);
        IndexLayout layout = IndexLayout.For(_buffer, bytes.Length, ranges);
        if (layout.Fits(_buffer))
        {
            return (bytes, ranges, layout);
        }

        int cut = FittingCut(ranges);
        ChunkBytes shorter = bytes with { Length = cut, EndsFile = false, LastLineUnterminated = false };
        ranges = LineRanges.Split(_buffer.Bytes, cut, lastLineUnterminated: false, _work.Threads, _work);
        return (shorter, ranges, IndexLayout.For(_buffer, cut, ranges));
    }

    // The end of the last line that still fits with the entries of all lines before it and the most that their
    // alignment can take: whole ranges first, then line by line in the range where the room runs out.
    private int FittingCut(LineRanges ranges)
    {
        long room = _buffer.Length - IndexLayout.MaxAlignment(ranges.Count);
        long lines = 0;
        for (int range = 0; range < ranges.Count; range++)
        {
            long linesAfter = lines + ranges.Lines(range);
            if (ranges.End(range) + (linesAfter * ChunkBuffer.IndexEntrySize) > room)
            {
                return CutInside(ranges.Start(range), ranges.End(range), lines, room);
            }

            lines = linesAfter;
        }

        throw new InvalidOperationException("The chunk fits; it needs no shorter cut.");
    }

    // Walks one range line by line; with one thread and a huge chunk of very short lines that is hundreds of millions
    // of lines, so the cancellation is looked at now and then.
    private int CutInside(int start, int end, long lines, long room)
    {
        int cut = start;
        while (true)
        {
            if (lines % CancellationInterval == 0)
            {
                _work.CancellationToken.ThrowIfCancellationRequested();
            }

            int lineFeed = _buffer.Bytes.AsSpan(cut, end - cut).IndexOf(LineFormat.LineFeed);
            int next = cut + lineFeed + 1;
            if (lineFeed < 0 || next + ((lines + 1) * ChunkBuffer.IndexEntrySize) > room)
            {
                break;
            }

            cut = next;
            lines++;
        }

        if (cut == 0)
        {
            throw new InvalidOperationException("The chunk buffer cannot hold one line and its entry.");
        }

        return cut;
    }

    // Every range is parsed on its own thread into its slice of the index. The ranges lie in the order of the file,
    // so the first one with a problem holds the first problem of the chunk; the chunks before it had none.
    private void Parse(ChunkBytes bytes, LineRanges ranges, IndexLayout layout)
    {
        RangeResult[] results = new RangeResult[ranges.Count];
        CancellationToken cancellationToken = _work.CancellationToken;
        _work.For(ranges.Count, range =>
        {
            int sliceOffset = layout.EntriesOffset + (layout.SliceStart(range) * ChunkBuffer.IndexEntrySize);
            Span<IndexEntry> slice = _buffer.Entries(sliceOffset, ranges.Lines(range));
            results[range] = RangeParser.Parse(
                _buffer.Bytes,
                ranges.Start(range),
                ranges.End(range),
                _lineEnding,
                _maxLineLength,
                slice,
                cancellationToken);
        });

        long linesBefore = _reader.LinesBefore;
        for (int range = 0; range < ranges.Count; range++)
        {
            if (results[range].Problem is RangeProblem problem)
            {
                throw Failure(problem, linesBefore + problem.LineIndex + 1, bytes.FileOffset + problem.Offset);
            }

            linesBefore += ranges.Lines(range);
        }
    }

    private Exception Failure(RangeProblem problem, long lineNumber, long offset)
    {
        return problem.TooLong
            ? new LineTooLongException(lineNumber, offset, _maxLineLength, _lineEnding)
            : new InvalidLineException(lineNumber, offset, problem.Fault, _lineEnding);
    }

    // The ranges are sorted on all threads, every range by one thread, its segments one after another. A range is
    // not always sorted by the thread that parsed it: the two are separate loops, each balanced over the threads on
    // its own.
    private Segment[] Sort(LineRanges ranges, IndexLayout layout)
    {
        Segment[][] planned = new Segment[ranges.Count][];
        for (int range = 0; range < ranges.Count; range++)
        {
            planned[range] = [.. SegmentSorter.Plan(layout.SliceStart(range), ranges.Lines(range), _segmentSize)];
        }

        CancellationToken cancellationToken = _work.CancellationToken;
        int entriesOffset = layout.EntriesOffset;
        _work.For(ranges.Count, range => SegmentSorter.Sort(_buffer, entriesOffset, planned[range], cancellationToken));

        // The segments of all ranges, in the order of the ranges.
        return [.. planned.SelectMany(segments => segments)];
    }
}
