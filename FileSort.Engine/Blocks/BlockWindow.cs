namespace FileSort.Engine.Blocks;

/// <summary>
/// The window of D8 and D13 of the design: one array, allocated once, for the merged blocks that wait to be written.
/// Its end is set aside as the piece buffer of a large block, which its worker writes piece by piece; the rest holds
/// the buffers of ordinary blocks, used as a ring: blocks start in order and are written in order, so the oldest
/// buffer is always the next one to be freed. In the merge phase the whole array is the output buffer.
/// </summary>
/// <remarks>Not thread-safe: the writer of a chunk calls it under its lock.</remarks>
internal sealed class BlockWindow
{
    private readonly Queue<(int Offset, int Length)> _inFlight = new();
    private int _newestEnd;
    private long _bytesInFlight;

    /// <param name="length">The bytes of the whole window.</param>
    /// <param name="pieceLength">The bytes set aside at its end for the pieces of a large block.</param>
    public BlockWindow(int length, int pieceLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 2 * pieceLength);
        Bytes = GC.AllocateUninitializedArray<byte>(length);
        PieceLength = pieceLength;
    }

    public byte[] Bytes { get; }

    /// <summary>The bytes of the ring of block buffers.</summary>
    public int RingLength => Bytes.Length - PieceLength;

    /// <summary>Where the piece buffer starts.</summary>
    public int PieceOffset => RingLength;

    public int PieceLength { get; }

    /// <summary>
    /// The largest block that gets a buffer of its own, a quarter of the ring. A larger block is written by its worker
    /// through the piece buffer.
    /// </summary>
    public int LargestBuffered => RingLength / 4;

    /// <summary>The most bytes of block buffers that were reserved at one time and not yet freed.</summary>
    public long MostBytesInFlight { get; private set; }

    /// <summary>
    /// Reserves a buffer of the given length, not empty, after the newest one, or at the start of the ring when the
    /// end has no room. Returns false when the ring has no room for it until older buffers are freed.
    /// </summary>
    public bool TryReserve(int length, out int offset)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, LargestBuffered);

        offset = _inFlight.Count == 0 ? 0 : FreePlace(length);
        if (offset < 0)
        {
            return false;
        }

        _inFlight.Enqueue((offset, length));
        _newestEnd = offset + length;
        _bytesInFlight += length;
        MostBytesInFlight = Math.Max(MostBytesInFlight, _bytesInFlight);
        return true;
    }

    /// <summary>Frees the oldest buffer, which must be the one that starts at the given offset.</summary>
    public void Free(int offset)
    {
        (int oldest, int length) = _inFlight.Dequeue();
        if (oldest != offset)
        {
            throw new InvalidOperationException("Block buffers are freed in the order they were reserved.");
        }

        _bytesInFlight -= length;
    }

    // Where a buffer of the length fits now, or -1. Without a wrap the buffers lie from the oldest to the newest, and
    // the room is after the newest or before the oldest; after a wrap the room is between the newest and the oldest.
    private int FreePlace(int length)
    {
        int oldest = _inFlight.Peek().Offset;
        if (_newestEnd > oldest)
        {
            return _newestEnd + length <= RingLength ? _newestEnd : length <= oldest ? 0 : -1;
        }

        return _newestEnd + length <= oldest ? _newestEnd : -1;
    }
}
