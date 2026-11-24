using System.Text;

using FileSort.Common;
using FileSort.Engine;
using FileSort.Engine.Blocks;
using FileSort.Engine.Chunks;
using FileSort.Tests.Engine.Chunks;
using FileSort.Tests.Format;

namespace FileSort.Tests.Engine.Blocks;

/// <summary>
/// The write of a chunk by the block merge (D19 of the design, "Blocks"): a line that repeats thousands of times, long
/// lines that sort next to each other, a large block right after a window full of small ones, and a chunk of one
/// segment. The run must be the naive sort of the chunk, and the block buffers in flight must never pass the window.
/// </summary>
public class ChunkWriterTests
{
    private const int MaxLineLength = 100;

    public static TheoryData<int> Threads => new() { 1, 2, 7, 16 };

    private static string[] Shuffled(List<string> lines, int seed)
    {
        string[] shuffled = [.. lines];
        new System.Random(seed).Shuffle(shuffled);
        return shuffled;
    }

    // Writes the input, which must fit in one chunk, as one run, and checks the run and the window.
    private static void AssertWritesTheNaiveSort(
        IEnumerable<string> lines, int threads, int blockBytes, int windowBytes, int segmentEntries = 8)
    {
        byte[] input = Encoding.UTF8.GetBytes(string.Concat(lines.Select(line => line + "\n")));
        ChunkBuffer buffer = new((4 * input.Length) + (1 << 16));
        ChunkMaker maker = TestChunks.Maker(
            input, buffer, threads, maxLineLength: MaxLineLength, segmentEntries: segmentEntries);
        SortedChunk chunk = maker.Next()!;
        Assert.True(chunk.Bytes.EndsFile);
        BlockWindow window = new(windowBytes, MaxLineLength + 2);
        using MemoryStream run = new();

        long written = new ChunkWriter(window, blockBytes, LineEnding.Lf)
            .Write(chunk, run, new ParallelWork(threads, TestContext.Current.CancellationToken));

        Assert.Equal(NaiveSort.SortFile(input, "\n"), run.ToArray());
        Assert.Equal(run.Length, written);
        Assert.InRange(window.MostBytesInFlight, 0, window.RingLength);
    }

    [Theory]
    [MemberData(nameof(Threads))]
    public void A_line_that_repeats_thousands_of_times(int threads)
    {
        List<string> lines = [.. Enumerable.Repeat("7. Apple", 5000)];
        lines.AddRange(Enumerable.Range(0, 300).Select(i => $"{i}. Banana {i % 7}"));
        lines.AddRange(Enumerable.Repeat("07. Apple", 50));

        AssertWritesTheNaiveSort(Shuffled(lines, 1), threads, blockBytes: 256, windowBytes: 4096);
    }

    [Theory]
    [MemberData(nameof(Threads))]
    public void Long_lines_that_sort_next_to_each_other(int threads)
    {
        // The window of 1 KiB buffers blocks of up to a quarter of its ring; blocks of several lines of 98 bytes are
        // larger, and their workers write them through the piece buffer.
        List<string> lines = [.. Enumerable.Range(0, 60).Select(i => $"{i}. Zebra {new string('z', 85)}")];
        lines.AddRange(Enumerable.Range(0, 200).Select(i => $"{i}. A{i % 13}"));

        AssertWritesTheNaiveSort(Shuffled(lines, 2), threads, blockBytes: 400, windowBytes: 1024);
    }

    [Theory]
    [MemberData(nameof(Threads))]
    public void A_large_block_right_after_a_window_full_of_small_ones(int threads)
    {
        // Short lines that sort first fill the window with small blocks; the long lines after them make large blocks,
        // which may start only when every block before them is written.
        List<string> lines = [.. Enumerable.Range(0, 2000).Select(i => $"{i}. A{i % 50}")];
        lines.AddRange(Enumerable.Range(0, 40).Select(i => $"{i}. Z {new string('z', 90)}"));
        lines.AddRange(Enumerable.Range(0, 500).Select(i => $"{i}. ZZ{i % 5}"));

        AssertWritesTheNaiveSort(Shuffled(lines, 3), threads, blockBytes: 300, windowBytes: 1200);
    }

    [Fact]
    public void A_chunk_of_one_segment()
    {
        List<string> lines = [.. Enumerable.Range(0, 3000).Select(i => $"{i * 7919 % 3001}. Apple {i % 11}")];

        AssertWritesTheNaiveSort(lines, threads: 1, blockBytes: 512, windowBytes: 4096, segmentEntries: 1 << 16);
    }

    [Fact]
    public void Lines_with_equal_keys_are_split_between_blocks_without_changing_a_byte()
    {
        // Every line has the same key; the strict order of the splitters still cuts them into blocks of about equal
        // size, and identical lines in any order are the same bytes.
        List<string> lines = [.. Enumerable.Repeat("1. Same", 10_000)];

        AssertWritesTheNaiveSort(lines, threads: 7, blockBytes: 512, windowBytes: 4096);
    }
}
