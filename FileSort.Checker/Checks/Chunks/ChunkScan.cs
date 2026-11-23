using FileSort.Checker.Checks.Order;
using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Lines;

namespace FileSort.Checker.Checks.Chunks;

/// <summary>
/// The check of one chunk, on one thread: every line is split off, checked to be valid and added to the digest of
/// the chunk; for the output, every line is also compared with the line before it, the first one with the last line
/// of the chunk before. The check stops at the first line that is not valid or is too long. It knows nothing of the
/// other chunks, so chunks are checked at the same time on different threads.
/// </summary>
internal sealed class ChunkScan(Chunk chunk, LineEnding lineEnding, int maxLineLength, bool checkOrder)
{
    private const byte CarriageReturn = (byte)'\r';

    private readonly Chunk _chunk = chunk;
    private readonly LineEnding _lineEnding = lineEnding;
    private readonly int _maxLineLength = maxLineLength;
    private readonly OrderCheck? _order = checkOrder ? new OrderCheck(chunk.Buffer) : null;

    private LineDigest _digest;
    private long _terminatedLines;
    private long _carriageReturnEndings;

    /// <summary>Checks the lines of the chunk. One <see cref="ChunkScan"/> checks one chunk, once.</summary>
    public ChunkResult Run()
    {
        PrecedeWithThePreviousLine();

        LineSplitter lines = new(_chunk.Bytes, _lineEnding, _chunk.EndsFile, _maxLineLength);
        while (true)
        {
            LineRead read = lines.TryRead(out ReadOnlySpan<byte> line);
            if (read == LineRead.None)
            {
                return Result(ChunkEnd.Completed, faultyLine: null);
            }

            if (read == LineRead.TooLong)
            {
                return Result(ChunkEnd.LineTooLong, Faulty(lines, LineFault.None, line));
            }

            int numberLength = 0;
            LineFault fault = read == LineRead.BareLineFeed
                ? LineFault.BareLineFeed
                : LineParser.Parse(line, out numberLength);
            if (fault != LineFault.None)
            {
                return Result(ChunkEnd.InvalidLine, Faulty(lines, fault, line));
            }

            Count(line, _chunk.BytesStart + lines.LineOffset, numberLength, lines.LineHasTerminator);
        }
    }

    // The last line of the chunk before, which the reader put in front of this chunk, is what the first line is
    // compared with. When it is not a valid line, the chunk before reports it and ends the scan, so there is no
    // comparison to make.
    private void PrecedeWithThePreviousLine()
    {
        if (_order is null || _chunk.PreviousLine.IsEmpty)
        {
            return;
        }

        LineSplitter previous = new(_chunk.PreviousLine, _lineEnding, endsFile: false, _maxLineLength);
        if (previous.TryRead(out ReadOnlySpan<byte> line) == LineRead.Line
            && LineParser.Parse(line, out int numberLength) == LineFault.None)
        {
            // The previous line lies at the very start of the buffer.
            _order.Precede(0, line.Length, numberLength);
        }
    }

    private void Count(ReadOnlySpan<byte> line, int offsetInBuffer, int numberLength, bool hasTerminator)
    {
        _digest = _digest.Add(line);

        // How many lines end with a carriage return tells, in the summary, an output written with \r\n for an
        // input written with \n. The last line of a file may have no terminator, so it is left out.
        if (hasTerminator)
        {
            _terminatedLines++;
            _carriageReturnEndings += line.EndsWith(CarriageReturn) ? 1 : 0;
        }

        _order?.Add(offsetInBuffer, line.Length, numberLength);
    }

    private static InvalidLine Faulty(LineSplitter lines, LineFault fault, ReadOnlySpan<byte> line)
    {
        return new InvalidLine(lines.LineNumber, lines.LineOffset, fault, LineSample.Of(line));
    }

    private ChunkResult Result(ChunkEnd end, InvalidLine? faultyLine)
    {
        return new ChunkResult
        {
            End = end,
            FaultyLine = faultyLine,
            Digest = _digest,
            TerminatedLines = _terminatedLines,
            CarriageReturnEndings = _carriageReturnEndings,
            Order = _order?.Result,
        };
    }
}
