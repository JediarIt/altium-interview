using System.Runtime.InteropServices;

using FileSort.Engine.Lines;

namespace FileSort.Engine.Chunks;

/// <summary>
/// The one large array of the sort (D4 and D13 of the design). In the first phase a chunk's lines fill it from the
/// start and their index entries follow them; in the second phase it is cut into the buffers of the runs that are
/// merged. It is allocated once, without zeroing, on the heap of pinned objects: its address never changes, so the
/// alignment of the entries to cache lines, computed from the address, stays true.
/// </summary>
internal sealed class ChunkBuffer
{
    /// <summary>The bytes of a cache line: two threads that write to one cache line slow each other down.</summary>
    public const int CacheLine = 64;

    /// <summary>
    /// The entries whose bytes fill a whole cache line: 4 entries of 16 bytes are one line of 64 bytes. A slice of the
    /// index that starts at a multiple of this count starts on a cache line. A constant needs the 16 written out,
    /// because <see cref="IndexEntrySize"/> is not one; a test holds the two together.
    /// </summary>
    public const int EntriesPerCacheLines = CacheLine / 16;

    private readonly long _address;

    public ChunkBuffer(int length)
    {
        Bytes = GC.AllocateUninitializedArray<byte>(length, pinned: true);
        _address = Marshal.UnsafeAddrOfPinnedArrayElement(Bytes, 0);
    }

    public byte[] Bytes { get; }

    public int Length => Bytes.Length;

    /// <summary>The first offset at or after <paramref name="offset"/> whose address is a multiple of 64.</summary>
    public int AlignToCacheLine(int offset)
    {
        long misalignment = (_address + offset) % CacheLine;
        return misalignment == 0 ? offset : offset + (int)(CacheLine - misalignment);
    }

    /// <summary>
    /// The entries that start at the given byte offset, which must be aligned by <see cref="AlignToCacheLine"/> for
    /// the entries of different threads not to share cache lines.
    /// </summary>
    public Span<IndexEntry> Entries(int byteOffset, int count)
    {
        return MemoryMarshal.Cast<byte, IndexEntry>(Bytes.AsSpan(byteOffset, count * IndexEntrySize));
    }

    /// <summary>How many entries fit between a byte offset and the end of the buffer.</summary>
    public int EntriesFrom(int byteOffset)
    {
        return (Length - byteOffset) / IndexEntrySize;
    }

    /// <summary>The bytes of one index entry.</summary>
    public static int IndexEntrySize => System.Runtime.CompilerServices.Unsafe.SizeOf<IndexEntry>();
}
