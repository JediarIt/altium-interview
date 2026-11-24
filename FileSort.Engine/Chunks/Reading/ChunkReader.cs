using FileSort.Common;
using FileSort.Engine.Lines.Parsing;

namespace FileSort.Engine.Chunks.Reading;

/// <summary>
/// Reads the input into the chunk buffer, one chunk after another (D4 of the design): as many bytes as the lines and
/// their index entries can be expected to fill, cut after the last line feed read. The next chunk starts at the file
/// offset where this one ended, so nothing is copied around; the bytes after the cut are read again.
/// </summary>
internal sealed class ChunkReader
{
    /// <summary>Reads go in pieces of this size, and between them the cancellation and the progress are looked
    /// at.</summary>
    public const int PieceSize = 16 * 1024 * 1024;

    /// <summary>The average line of the generator's default content, its line feed included, rounded up.</summary>
    private const double FirstBytesPerLine = 25;

    private readonly IInputSource _input;
    private readonly ChunkBuffer _buffer;
    private readonly LineEnding _lineEnding;
    private readonly int _maxLineLength;
    private readonly int _alignment;
    private readonly SortProgress _progress;
    private long _position;
    private long _linesBefore;
    private double _bytesPerLine = FirstBytesPerLine;

    /// <param name="input">The input.</param>
    /// <param name="buffer">The chunk buffer, which the bytes are read into from its start.</param>
    /// <param name="firstLineOffset">Where the first line starts: after a byte order mark.</param>
    /// <param name="lineEnding">The line ending, for the report of a line that is too long.</param>
    /// <param name="maxLineLength">The longest line accepted, its terminator not counted.</param>
    /// <param name="alignment">The bytes that the index entries may lose to their alignment.</param>
    /// <param name="progress">The counters that show how far the input has been read.</param>
    public ChunkReader(
        IInputSource input, ChunkBuffer buffer, long firstLineOffset, LineEnding lineEnding, int maxLineLength,
        int alignment, SortProgress progress)
    {
        _input = input;
        _buffer = buffer;
        _position = firstLineOffset;
        _lineEnding = lineEnding;
        _maxLineLength = maxLineLength;
        _alignment = alignment;
        _progress = progress;
    }

    /// <summary>The bytes that tell for certain that a line is too long when no line feed is among them.</summary>
    public int LongestLineWithTerminator => _maxLineLength + 2;

    /// <summary>The lines of the chunks read and parsed so far.</summary>
    public long LinesBefore => _linesBefore;

    /// <summary>
    /// Reads the bytes of the next chunk, or returns null when the input has been read to its end.
    /// </summary>
    /// <exception cref="LineTooLongException">The first line of the chunk is too long: no line feed ends
    /// it.</exception>
    public ChunkBytes? Read(CancellationToken cancellationToken)
    {
        long remaining = _input.Length - _position;
        if (remaining == 0)
        {
            return null;
        }

        int read = ReadPieces((int)Math.Min(remaining, ReadSize()), cancellationToken);
        byte[] bytes = _buffer.Bytes;
        if (_position + read == _input.Length)
        {
            bool lastLineUnterminated = bytes[read - 1] != LineFormat.LineFeed;
            return new ChunkBytes(_position, read, EndsFile: true, lastLineUnterminated);
        }

        // At least a whole longest line and its terminator were read, so a chunk without a line feed starts with a
        // line that is too long, whatever the line ending.
        int lastLineFeed = bytes.AsSpan(0, read).LastIndexOf(LineFormat.LineFeed);
        if (lastLineFeed < 0)
        {
            throw new LineTooLongException(_linesBefore + 1, _position, _maxLineLength, _lineEnding);
        }

        return new ChunkBytes(_position, lastLineFeed + 1, EndsFile: false, LastLineUnterminated: false);
    }

    /// <summary>Moves on past a chunk that has been parsed: the next chunk starts where it ended.</summary>
    public void Advance(int length, long lines)
    {
        _position += length;
        _linesBefore += lines;
        if (lines > 0)
        {
            _bytesPerLine = length / (double)lines;
        }
    }

    // The bytes that the lines and their entries can be expected to fill, from the average line of the last chunk:
    // with lines of 25 bytes and entries of 16, 61% of the buffer. At least a longest line with its terminator and one
    // more byte, so that a chunk without a line feed proves a line too long.
    private long ReadSize()
    {
        long available = _buffer.Length - _alignment;
        long expected = (long)(available * _bytesPerLine / (_bytesPerLine + ChunkBuffer.IndexEntrySize));
        return Math.Max(expected, LongestLineWithTerminator + 1);
    }

    private int ReadPieces(int length, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int piece = _input.Read(_position + read, _buffer.Bytes.AsSpan(read, Math.Min(PieceSize, length - read)));
            if (piece == 0)
            {
                throw new IOException("The input ended before its length was read: it changed during the sort.");
            }

            read += piece;
            _progress.ReadUpTo(_position + read);
        }

        return read;
    }
}
