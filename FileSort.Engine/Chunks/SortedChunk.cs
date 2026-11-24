using FileSort.Engine.Chunks.Reading;
using FileSort.Engine.Chunks.Sorting;
using FileSort.Engine.Lines;

namespace FileSort.Engine.Chunks;

/// <summary>
/// A chunk that has been read, parsed and sorted: its lines lie in the chunk buffer from its start, and its index is a
/// set of sorted segments, which the block merge turns into one run.
/// </summary>
internal sealed class SortedChunk(
    ChunkBuffer buffer, ChunkBytes bytes, int entriesOffset, Segment[] segments, long lines)
{
    private readonly int _entriesOffset = entriesOffset;

    public ChunkBuffer Buffer { get; } = buffer;

    public ChunkBytes Bytes { get; } = bytes;

    /// <summary>The lines of the chunk.</summary>
    public long Lines { get; } = lines;

    public IReadOnlyList<Segment> Segments { get; } = segments;

    /// <summary>The whole index, from its first entry: the start of a segment is an index into it.</summary>
    public Span<IndexEntry> Index
    {
        get
        {
            int length = Segments.Count == 0 ? 0 : Segments[^1].Start + Segments[^1].Length;
            return Buffer.Entries(_entriesOffset, length);
        }
    }

    /// <summary>The entries of a segment, sorted.</summary>
    public Span<IndexEntry> Entries(Segment segment)
    {
        return Buffer.Entries(_entriesOffset + (segment.Start * ChunkBuffer.IndexEntrySize), segment.Length);
    }
}
