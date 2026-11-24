using FileSort.Engine;
using FileSort.Tests.Engine.InMemory;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Engine;

/// <summary>
/// A cancellation after the output was created, which the tests of files cannot reach: at the start of the final merge,
/// and at the start of the write of an input that fits in one chunk. The sort stops there and does not finish the
/// output; deleting what it wrote is the run storage's, which the tests of files check.
/// </summary>
public class CancellationTests
{
    private static readonly byte[] Input = TestContent.Generate(TestContent.Options(seed: 3), 64 * 1024);

    [Fact]
    public void A_cancellation_during_the_final_merge_stops_the_sort()
    {
        using CancellationTokenSource cancellation = new();
        MemoryRunStorage storage = new() { OnCreateOutput = cancellation.Cancel };

        Assert.ThrowsAny<OperationCanceledException>(() => TestSort.RunCancellable(
            Input, TestSort.Settings(TestSort.SmallestChunkBuffer(2) * 4, 2), cancellation.Token, storage));

        Assert.True(storage.RunsCreated > 2, "The input made runs to merge.");
        Assert.True(storage.Output!.Length < Input.Length, "The output was not finished.");
    }

    [Fact]
    public void A_cancellation_during_the_write_of_one_chunk_stops_the_sort()
    {
        using CancellationTokenSource cancellation = new();
        MemoryRunStorage storage = new() { OnCreateOutput = cancellation.Cancel };

        Assert.ThrowsAny<OperationCanceledException>(() => TestSort.RunCancellable(
            Input, TestSort.Settings(1 << 20, 2, blockBytes: 1024), cancellation.Token, storage));

        Assert.Equal(0, storage.RunsCreated);
        Assert.True(storage.Output!.Length < Input.Length, "The output was not finished.");
    }
}