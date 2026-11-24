using FileSort.Engine.Chunks;

namespace FileSort.Tests.Engine.Chunks;

/// <summary>The constants that the alignment of the index to cache lines rests on.</summary>
public class ChunkBufferTests
{
    [Fact]
    public void The_entries_per_cache_line_fill_exactly_one_cache_line()
    {
        // EntriesPerCacheLines is written as CacheLine / 16: an entry of another size must change it too, or the
        // slices of the index stop starting on cache lines.
        Assert.Equal(ChunkBuffer.CacheLine, ChunkBuffer.EntriesPerCacheLines * ChunkBuffer.IndexEntrySize);
    }
}
