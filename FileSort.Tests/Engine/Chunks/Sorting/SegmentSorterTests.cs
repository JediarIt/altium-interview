using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using FileSort.Engine.Chunks;
using FileSort.Engine.Chunks.Sorting;
using FileSort.Engine.Lines;
using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Tests.Engine.InMemory;
using FileSort.Tests.Format;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Engine.Chunks.Sorting;

/// <summary>
/// The sort of a chunk in segments (D7 of the design): every segment in the order of the naive comparison with the
/// tie-break by bytes, every segment starting on a cache line, and a sort with one thread that stops soon after a
/// cancellation.
/// </summary>
public class SegmentSorterTests
{
    public static TheoryData<int> Threads => new() { 1, 2, 7, 16 };

    private static SortedChunk OneChunk(int threads, int segmentEntries)
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.All);
        byte[] input = TestContent.Generate(options, 256 * 1024);
        int maxLineLength = new BlockGenerator(options).MaxLineLength;
        ChunkMaker maker = TestChunks.Maker(
            input, new ChunkBuffer(1 << 21), threads, maxLineLength: maxLineLength, segmentEntries: segmentEntries);
        SortedChunk chunk = maker.Next()!;
        Assert.True(chunk.Bytes.EndsFile, "The input must fit in one chunk.");
        return chunk;
    }

    private static byte[] LineOf(SortedChunk chunk, IndexEntry entry)
    {
        return chunk.Buffer.Bytes.AsSpan((int)entry.Offset, entry.LineLength).ToArray();
    }

    // The order the sorter writes: the naive comparison of sort-order, then the bytes of the lines.
    private static int NaiveOrder(byte[] x, byte[] y)
    {
        int keys = NaiveLineComparer.Instance.Compare(NaiveLineParser.ParseLine(x), NaiveLineParser.ParseLine(y));
        return keys != 0 ? keys : ByteOrder.Instance.Compare(x, y);
    }

    [Theory]
    [MemberData(nameof(Threads))]
    public void Every_segment_is_in_the_naive_order(int threads)
    {
        SortedChunk chunk = OneChunk(threads, segmentEntries: 64);

        Assert.True(chunk.Segments.Count > threads);
        Assert.All(chunk.Segments, segment => Assert.InRange(segment.Length, 1, 64));
        Assert.Equal(chunk.Lines, chunk.Segments.Sum(segment => (long)segment.Length));
        foreach (Segment segment in chunk.Segments)
        {
            byte[][] lines = [.. chunk.Entries(segment).ToArray().Select(entry => LineOf(chunk, entry))];
            for (int i = 1; i < lines.Length; i++)
            {
                Assert.True(NaiveOrder(lines[i - 1], lines[i]) <= 0, $"Out of order at {i} of a segment.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Threads))]
    public void Every_segment_starts_on_a_cache_line(int threads)
    {
        SortedChunk chunk = OneChunk(threads, segmentEntries: 20);
        long buffer = Marshal.UnsafeAddrOfPinnedArrayElement(chunk.Buffer.Bytes, 0);

        foreach (Segment segment in chunk.Segments)
        {
            ref byte first = ref Unsafe.As<IndexEntry, byte>(ref MemoryMarshal.GetReference(chunk.Entries(segment)));
            long offset = Unsafe.ByteOffset(ref chunk.Buffer.Bytes[0], ref first);
            Assert.Equal(0, (buffer + offset) % ChunkBuffer.CacheLine);
        }
    }

    [Fact]
    public void A_segment_size_that_is_not_a_whole_cache_line_of_entries_is_refused()
    {
        Assert.Throws<ArgumentException>(() => TestSort.Run([], TestSort.Settings(1 << 16, 1, segmentEntries: 6)));
    }
}
