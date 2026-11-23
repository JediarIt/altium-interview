namespace FileSort.Checker.Checks.Chunks;

/// <summary>
/// A part of a file that consists of whole lines, in a buffer of its own, so that it can be checked on another
/// thread while the file is read on. Every chunk but the last ends with a line feed; the last one reaches the end
/// of the file, and its last line may have no terminator. For the order check, the buffer starts with a copy of the
/// last line of the chunk before, with its terminator, so that every pair of neighbouring lines lies in one chunk.
/// </summary>
internal sealed class Chunk(byte[] buffer, int previousLineLength, int start, int length, long offset, bool endsFile)
{
    // Where the copy of the previous line and the bytes of the chunk lie in the buffer.
    private readonly int _previousLineLength = previousLineLength;
    private readonly int _start = start;
    private readonly int _length = length;

    /// <summary>The buffer that holds the bytes. It comes from the pool of arrays and goes back there.</summary>
    public byte[] Buffer { get; } = buffer;

    /// <summary>The offset of the first byte of the chunk from the start of the file, from 0.</summary>
    public long Offset { get; } = offset;

    /// <summary>The chunk reaches the end of the file.</summary>
    public bool EndsFile { get; } = endsFile;

    /// <summary>
    /// The chunk stands for one line that has no end within the longest line allowed: it has no bytes, and
    /// <see cref="Offset"/> is where that line starts.
    /// </summary>
    public bool IsLineWithoutEnd { get; private init; }

    /// <summary>The bytes of the chunk: whole lines with their terminators.</summary>
    public ReadOnlySpan<byte> Bytes => Buffer.AsSpan(_start, _length);

    /// <summary>Where <see cref="Bytes"/> start in <see cref="Buffer"/>.</summary>
    public int BytesStart => _start;

    /// <summary>
    /// The last line of the chunk before, with its terminator, at the start of <see cref="Buffer"/>; empty for the
    /// first chunk of a file, and when the order is not checked.
    /// </summary>
    public ReadOnlySpan<byte> PreviousLine => Buffer.AsSpan(0, _previousLineLength);

    public static Chunk LineWithoutEnd(long offset)
    {
        return new Chunk([], 0, 0, 0, offset, endsFile: false) { IsLineWithoutEnd = true };
    }
}
