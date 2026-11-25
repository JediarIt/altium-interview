namespace FileSort.Engine.Chunks.Parsing;

/// <summary>
/// Where the index entries of a chunk go: right after its lines, from the first cache line that starts there, one
/// slice per range. Every slice starts on a cache line, so that two threads that fill or sort their slices never
/// write to one cache line.
/// </summary>
internal sealed class IndexLayout
{
    private readonly int[] _sliceStarts;

    private IndexLayout(int entriesOffset, int[] sliceStarts)
    {
        EntriesOffset = entriesOffset;
        _sliceStarts = sliceStarts;
    }

    /// <summary>The byte offset of the first entry in the chunk buffer, on a cache line.</summary>
    public int EntriesOffset { get; }

    /// <summary>The entries of all slices, with the gaps between them.</summary>
    public int EntriesWithGaps => _sliceStarts[^1];

    /// <summary>
    /// The bytes that the alignment can take at most beyond the lines and the entries: the start of the entries moved
    /// to a cache line, and a gap of fewer than <see cref="ChunkBuffer.EntriesPerCacheLines"/> entries after every
    /// slice.
    /// </summary>
    public static int MaxAlignment(int ranges)
    {
        return ChunkBuffer.CacheLine + (ranges * (ChunkBuffer.EntriesPerCacheLines - 1) * ChunkBuffer.IndexEntrySize);
    }

    /// <summary>The layout of the entries of the given ranges, whose lines end at
    /// <paramref name="linesEnd"/>.</summary>
    public static IndexLayout For(ChunkBuffer buffer, int linesEnd, LineRanges ranges)
    {
        int[] starts = new int[ranges.Count + 1];
        for (int range = 0; range < ranges.Count; range++)
        {
            starts[range + 1] = RoundUpToCacheLines(starts[range] + ranges.Lines(range));
        }

        return new IndexLayout(buffer.AlignToCacheLine(linesEnd), starts);
    }

    /// <summary>The index of the first entry of a range's slice, counted from <see cref="EntriesOffset"/>.</summary>
    public int SliceStart(int range) => _sliceStarts[range];

    /// <summary>Whether the lines and all the entries fit in the buffer.</summary>
    public bool Fits(ChunkBuffer buffer)
    {
        return (long)EntriesOffset + ((long)EntriesWithGaps * ChunkBuffer.IndexEntrySize) <= buffer.Length;
    }

    // A count of entries rounded up to whole cache lines of entries: with 4 entries of 16 bytes in a cache line, 13
    // becomes 16 and 16 stays 16.
    private static int RoundUpToCacheLines(int entries)
    {
        int group = ChunkBuffer.EntriesPerCacheLines;
        return (entries + group - 1) / group * group;
    }
}
