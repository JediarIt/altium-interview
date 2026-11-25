using FileSort.Engine;
using FileSort.Tests.Engine.InMemory;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Engine;

/// <summary>The counters of a sort, as a progress display reads them, and the result.</summary>
public class SortProgressTests
{
    [Fact]
    public void After_a_sort_with_runs_the_counters_show_the_last_pass_complete()
    {
        byte[] input = TestContent.Generate(TestContent.Options(seed: 9), 128 * 1024);

        SortOutcome outcome = TestSort.Run(input, TestSort.Settings(TestSort.SmallestChunkBuffer(2), 2));

        SortProgress progress = outcome.Progress;
        Assert.Equal(SortPhase.Merge, progress.Phase);
        Assert.Equal(input.Length, progress.InputBytes);
        Assert.Equal(input.Length, progress.InputBytesRead);
        Assert.Equal(outcome.Result.Runs, progress.RunsWritten);
        Assert.Equal(outcome.Result.MergePasses, progress.Passes);
        Assert.Equal(progress.Passes, progress.Pass);
        Assert.Equal(input.Length, progress.PassBytes);
        Assert.Equal(progress.PassBytes, progress.PassBytesWritten);
        Assert.Equal(input.Length, outcome.Result.Bytes);
    }

    [Fact]
    public void An_input_of_one_chunk_has_no_runs_and_no_merge()
    {
        byte[] input = TestContent.Generate(TestContent.Options(seed: 9), 16 * 1024);

        SortOutcome outcome = TestSort.Run(input, TestSort.Settings(1 << 20, 4));

        Assert.Equal(SortPhase.Chunks, outcome.Progress.Phase);
        Assert.Equal(0, outcome.Result.Runs);
        Assert.Equal(0, outcome.Result.MergePasses);
        Assert.Equal(0, outcome.Progress.Pass);
        Assert.Equal(input.Length, outcome.Progress.InputBytesRead);
        Assert.Equal(input.Length, outcome.Result.Bytes);
        Assert.Equal(input.Count(b => b == '\n'), outcome.Result.Lines);
    }

    [Fact]
    public void Before_the_sort_nothing_has_started()
    {
        SortProgress progress = new();

        Assert.Equal(SortPhase.Starting, progress.Phase);
        Assert.Equal(0, progress.InputBytesRead);
        Assert.Equal(0, progress.Pass);
    }
}
