using System.Buffers;
using System.Text;

using FileSort.Checker.Lines;

namespace FileSort.Checker.Checks.Chunks;

/// <summary>
/// Reads a stream as chunks of whole lines: each read fills a buffer and cuts it after its last line feed; what
/// follows that line feed, the start of an unfinished line, is carried over to the next chunk. A line that does not
/// fit makes the buffer of its chunk grow, up to the longest line allowed. A byte order mark at the start of the
/// stream is skipped. Every chunk gets a buffer of its own from the pool of arrays; whoever checks the chunk gives
/// the buffer back with <see cref="Release"/>. For the order check, the reader puts a copy of the last line of a
/// chunk in front of the next chunk, in the same buffer.
/// </summary>
internal sealed class ChunkReader
{
    private const byte LineFeed = (byte)'\n';

    private readonly Stream _stream;
    private readonly int _chunkSize;

    // The most bytes that a line within the limit takes together with its terminator.
    private readonly int _longestLine;

    private readonly bool _keepPreviousLine;

    // The start of an unfinished line, left by the chunk before.
    private byte[] _carry = [];
    private int _carryLength;

    // The last line of the chunk before, with its terminator, when the next chunk is to get a copy of it.
    private byte[] _previousLine = [];
    private int _previousLineLength;

    // The offset in the file of the first byte of the chunk that is read next, the carried bytes included: the byte
    // that follows the copy of the previous line in the buffer.
    private long _offset;

    private bool _started;
    private bool _ended;

    /// <param name="stream">The file.</param>
    /// <param name="lineEnding">The line ending of the file.</param>
    /// <param name="chunkSize">The size of a chunk, as long as no line is longer.</param>
    /// <param name="maxLineLength">The longest line that is read, in bytes without its terminator.</param>
    /// <param name="keepPreviousLine">Put the last line of a chunk in front of the next chunk.</param>
    public ChunkReader(Stream stream, LineEnding lineEnding, int chunkSize, int maxLineLength, bool keepPreviousLine = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLineLength);

        _stream = stream;
        _chunkSize = chunkSize;
        _longestLine = maxLineLength + (lineEnding == LineEnding.CrLf ? 2 : 1);
        _keepPreviousLine = keepPreviousLine;
    }

    /// <summary>The UTF-8 byte order mark, the bytes <c>EF BB BF</c>, as the framework defines it.</summary>
    public static ReadOnlySpan<byte> ByteOrderMark => Encoding.UTF8.Preamble;

    /// <summary>The file starts with a byte order mark. Known after the first read.</summary>
    public bool HasByteOrderMark { get; private set; }

    /// <summary>How many bytes the last call of <see cref="Read"/> took from the stream.</summary>
    public long ReadBytes { get; private set; }

    /// <summary>Gives the buffer of a chunk back to the pool, once nothing looks at the chunk any more.</summary>
    public static void Release(Chunk chunk)
    {
        if (chunk.Buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(chunk.Buffer);
        }
    }

    /// <summary>
    /// Reads the next chunk: from the stream until its buffer is full or the stream ends. Returns null when the
    /// file has no more lines. A file that fits in one buffer is one chunk.
    /// </summary>
    public Chunk? Read()
    {
        ReadBytes = 0;
        if (_ended)
        {
            return null;
        }

        // The copy of the previous line, then the carried bytes, then at least one more byte; and room for a byte order
        // mark, which has to be seen whole. The copy and the carried bytes are short as a rule, and then the buffer is
        // exactly one chunk.
        int previous = _keepPreviousLine ? _previousLineLength : 0;
        int capacity = Math.Max(Math.Max(_chunkSize, ByteOrderMark.Length), previous + _carryLength + 1);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(capacity);
        _previousLine.AsSpan(0, previous).CopyTo(buffer);
        _carry.AsSpan(0, _carryLength).CopyTo(buffer.AsSpan(previous));
        int filled = previous + _carryLength;
        _carryLength = 0;

        while (true)
        {
            bool endOfFile = Fill(buffer, capacity, ref filled);
            int start = previous + SkipByteOrderMark(buffer.AsSpan(previous, filled - previous));
            if (TryCut(buffer, previous, start, filled, endOfFile) is Chunk chunk)
            {
                return chunk.Bytes.IsEmpty && !chunk.IsLineWithoutEnd ? ReleaseEmpty(chunk) : chunk;
            }

            // One unfinished line fills the buffer: the buffer grows, by doubling, up to the longest line allowed.
            capacity = (int)Math.Min(2L * capacity, (long)start + _longestLine);
            buffer = Grow(buffer, filled, capacity);
        }
    }

    // Reads until the buffer is full or the stream ends, and tells whether the stream has ended. A stream that can
    // tell its length says so also when the buffer took its last byte exactly: a file of exactly one chunk is then
    // one chunk that reaches the end of the file, like any smaller file.
    private bool Fill(byte[] buffer, int capacity, ref int filled)
    {
        int room = capacity - filled;
        int read = _stream.ReadAtLeast(buffer.AsSpan(filled, room), room, throwOnEndOfStream: false);
        filled += read;
        ReadBytes += read;
        return read < room || (_stream.CanSeek && _stream.Position >= _stream.Length);
    }

    // Only the very first bytes of the file can be a byte order mark; it is not part of the first line.
    private int SkipByteOrderMark(ReadOnlySpan<byte> read)
    {
        if (!_started)
        {
            _started = true;
            HasByteOrderMark = read.StartsWith(ByteOrderMark);
        }

        return HasByteOrderMark && _offset == 0 ? ByteOrderMark.Length : 0;
    }

    // Cuts a chunk out of buffer[start..filled]: everything at the end of the file, the lines up to the last line
    // feed otherwise. buffer[..previous] is the copy of the previous line, and buffer[previous] is the byte at
    // _offset in the file. Returns null when there is no line feed yet and the line may still end within the limit.
    private Chunk? TryCut(byte[] buffer, int previous, int start, int filled, bool endOfFile)
    {
        ReadOnlySpan<byte> read = buffer.AsSpan(start, filled - start);
        long offset = _offset + (start - previous);
        if (endOfFile)
        {
            _ended = true;
            return new Chunk(buffer, previous, start, read.Length, offset, endsFile: true);
        }

        int feed = read.LastIndexOf(LineFeed);
        if (feed >= 0)
        {
            int length = feed + 1;
            KeepLastLine(read[..length]);
            Carry(read[length..]);
            _offset = offset + length;
            return new Chunk(buffer, previous, start, length, offset, endsFile: false);
        }

        if (read.Length < _longestLine)
        {
            return null;
        }

        // As many bytes as the longest line with its terminator, and no line feed among them: the line is longer.
        _ended = true;
        ArrayPool<byte>.Shared.Return(buffer);
        return Chunk.LineWithoutEnd(offset);
    }

    // The last line of the lines of a chunk, which end with a line feed: from the line feed before it, or from the
    // start of the chunk when the chunk is one line.
    private void KeepLastLine(ReadOnlySpan<byte> lines)
    {
        if (!_keepPreviousLine)
        {
            return;
        }

        ReadOnlySpan<byte> last = lines[(lines[..^1].LastIndexOf(LineFeed) + 1)..];
        if (_previousLine.Length < last.Length)
        {
            _previousLine = new byte[Math.Max(last.Length, 2 * _previousLine.Length)];
        }

        last.CopyTo(_previousLine);
        _previousLineLength = last.Length;
    }

    private void Carry(ReadOnlySpan<byte> unfinishedLine)
    {
        if (_carry.Length < unfinishedLine.Length)
        {
            _carry = new byte[unfinishedLine.Length];
        }

        unfinishedLine.CopyTo(_carry);
        _carryLength = unfinishedLine.Length;
    }

    private static byte[] Grow(byte[] buffer, int filled, int capacity)
    {
        byte[] larger = ArrayPool<byte>.Shared.Rent(capacity);
        buffer.AsSpan(0, filled).CopyTo(larger);
        ArrayPool<byte>.Shared.Return(buffer);
        return larger;
    }

    // A file that ends right after a byte order mark, or an empty one, has no lines and no chunk.
    private static Chunk? ReleaseEmpty(Chunk chunk)
    {
        Release(chunk);
        return null;
    }
}
