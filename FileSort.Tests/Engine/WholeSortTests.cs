using FileSort.Checker.Checks;
using FileSort.Common;
using FileSort.Engine;
using FileSort.Engine.Merging;
using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Tests.Checker.Checks;
using FileSort.Tests.Engine.InMemory;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Engine;

/// <summary>
/// The whole sort in memory on generated content with every edge case: whatever the number of threads and the size of
/// the chunk buffer, from chunks of a dozen lines with many merge passes to one chunk, the output is the naive sort of
/// the input byte for byte. The checker, a second oracle, finds the outputs correct.
/// </summary>
public class WholeSortTests
{
    private const int InputSize = 192 * 1024;

    private static byte[] Input(LineEnding lineEnding, out int maxLineLength)
    {
        ContentOptions options = TestContent.Options(seed: 5, edgeCases: EdgeCases.All, lineEnding: lineEnding);
        maxLineLength = new BlockGenerator(options).MaxLineLength;
        return TestContent.Generate(options, InputSize);
    }

    [Theory]
    [InlineData(LineEnding.Lf, 1)]
    [InlineData(LineEnding.Lf, 2)]
    [InlineData(LineEnding.Lf, 7)]
    [InlineData(LineEnding.Lf, 16)]
    [InlineData(LineEnding.CrLf, 1)]
    [InlineData(LineEnding.CrLf, 2)]
    [InlineData(LineEnding.CrLf, 7)]
    [InlineData(LineEnding.CrLf, 16)]
    public void Generated_lines_sort_like_the_naive_sort_whatever_the_threads_and_the_memory(
        LineEnding lineEnding, int threads)
    {
        byte[] input = Input(lineEnding, out int maxLineLength);
        byte[] expected = TestSort.Expected(input, lineEnding);
        int smallest = TestSort.SmallestChunkBuffer(threads, maxLineLength);

        foreach (int chunkBuffer in new[] { smallest, 3 * smallest, 64 * 1024, 4 * InputSize })
        {
            SortSettings settings = TestSort.Settings(chunkBuffer, threads, maxLineLength: maxLineLength);
            SortOutcome outcome = TestSort.Run(input, settings);

            Assert.Equal(expected, outcome.Output);
            Assert.Equal(lineEnding, outcome.Result.LineEnding);
            Assert.Equal(expected.Length, outcome.Result.Bytes);
        }
    }

    [Fact]
    public void The_smallest_memory_makes_many_runs_and_several_merge_passes()
    {
        byte[] input = Input(LineEnding.Lf, out int maxLineLength);
        int chunkBuffer = TestSort.SmallestChunkBuffer(1, maxLineLength);

        SortOutcome outcome = TestSort.Run(input, TestSort.Settings(chunkBuffer, 1, maxLineLength: maxLineLength));

        int room = chunkBuffer / (2 * (maxLineLength + 2));
        Assert.True(outcome.Result.Runs > 10 * room, $"{outcome.Result.Runs} runs for room {room}.");
        Assert.Equal(MergePlan.Merges(outcome.Result.Runs, room), outcome.Result.MergePasses);
        Assert.True(outcome.Result.MergePasses > 2);
        Assert.Empty(outcome.Storage.Runs);
    }

    [Theory]
    [InlineData(LineEnding.Lf)]
    [InlineData(LineEnding.CrLf)]
    public void The_checker_finds_the_outputs_correct(LineEnding lineEnding)
    {
        byte[] input = Input(lineEnding, out int maxLineLength);

        SortOutcome outcome = TestSort.Run(
            input, TestSort.Settings(TestSort.SmallestChunkBuffer(4, maxLineLength), 4, maxLineLength: maxLineLength));

        CheckResult result = TestCheck.Run(input, outcome.Output);
        Assert.Equal(CheckOutcome.Correct, result.Outcome);
    }

    [Fact]
    public void The_same_output_for_every_setting()
    {
        byte[] input = Input(LineEnding.Lf, out int maxLineLength);

        int smallestBuffer = TestSort.SmallestChunkBuffer(1, maxLineLength);
        SortSettings smallestSettings = TestSort.Settings(smallestBuffer, 1, maxLineLength: maxLineLength);
        SortSettings largeSettings = TestSort.Settings(
            1 << 22, 16, maxLineLength: maxLineLength, segmentEntries: 1 << 10, blockBytes: 1 << 16);
        byte[] smallest = TestSort.Run(input, smallestSettings).Output;
        byte[] large = TestSort.Run(input, largeSettings).Output;

        Assert.Equal(smallest, large);
    }
}
