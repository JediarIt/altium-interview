using System.Text;

using FileSort.Engine;
using FileSort.Engine.Blocks;
using FileSort.Engine.Chunks;
using FileSort.Tests.Engine.Chunks;

namespace FileSort.Tests.Engine.Blocks;

/// <summary>
/// The splitting of a chunk into blocks: blocks in order, of about equal size, also when one key
/// fills most of the chunk, and sizes summed from the lengths in the entries.
/// </summary>
public class BlockPlanTests
{
    private static SortedChunk Chunk(IEnumerable<string> lines, int threads)
    {
        byte[] input = Encoding.UTF8.GetBytes(string.Concat(lines.Select(line => line + "\n")));
        ChunkBuffer buffer = new((4 * input.Length) + (1 << 16));
        return TestChunks.Maker(input, buffer, threads, segmentEntries: 8).Next()!;
    }

    private static BlockPlan Plan(SortedChunk chunk, int blocks)
    {
        ParallelWork work = new(4, TestContext.Current.CancellationToken);
        return BlockPlan.Create(chunk, blocks, terminatorLength: 1, work);
    }

    [Fact]
    public void One_key_in_most_lines_is_still_split_into_blocks_of_about_equal_size()
    {
        List<string> lines = [.. Enumerable.Repeat("7. Apple", 9000)];
        lines.AddRange(Enumerable.Range(0, 1000).Select(i => $"{i}. Banana"));
        SortedChunk chunk = Chunk(lines, threads: 7);

        BlockPlan plan = Plan(chunk, blocks: 50);

        long average = chunk.Lines / plan.Blocks;
        Assert.Equal(chunk.Lines, Enumerable.Range(0, plan.Blocks).Sum(plan.Lines));
        Assert.All(Enumerable.Range(0, plan.Blocks), block => Assert.InRange(plan.Lines(block), 0, 3 * average));
    }

    [Fact]
    public void The_bounds_of_every_segment_never_go_back()
    {
        SortedChunk chunk = Chunk(Enumerable.Range(0, 5000).Select(i => $"{i * 37 % 911}. Line {i % 97}"), threads: 16);

        BlockPlan plan = Plan(chunk, blocks: 37);

        for (int segment = 0; segment < plan.Segments; segment++)
        {
            Assert.Equal(0, plan.From(0, segment));
            Assert.Equal(chunk.Segments[segment].Length, plan.To(plan.Blocks - 1, segment));
            for (int block = 0; block < plan.Blocks; block++)
            {
                Assert.True(plan.From(block, segment) <= plan.To(block, segment));
                Assert.True(block == 0 || plan.From(block, segment) == plan.To(block - 1, segment));
            }
        }
    }

    [Fact]
    public void The_size_of_a_block_is_the_bytes_of_its_lines_with_their_terminators()
    {
        SortedChunk chunk = Chunk(["10. Apple", "2. B", "300. Cherry pie", "4. D"], threads: 1);

        BlockPlan plan = Plan(chunk, blocks: 1);

        Assert.Equal(10 + 5 + 16 + 5, plan.Size(0));
        Assert.Equal(plan.Size(0), plan.TotalSize);
    }
}
