using System.Diagnostics;

using FileSort.Common;
using FileSort.Engine;
using FileSort.Tests.Engine.InMemory;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Engine.Chunks.Sorting;

/// <summary>
/// A sort with one thread that is cancelled while a large chunk is parsed, sorted or written must stop within the two
/// seconds of the requirement "Cancellation".
/// </summary>
[Collection(nameof(TimedTests))]
public class CancellationTimingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(100)]
    public void A_sort_with_one_thread_stops_soon_after_a_cancellation(int delay)
    {
        // A chunk of about a million lines, sorted by one thread in segments of 2^20 entries: the sort of a segment is
        // a third of a second, and a cancellation waits for one segment at most. The delays count from the moment
        // the chunk has been read, after which parsing, sorting and writing it take about 0.45 s on the reference
        // machine, so the cancellation lands inside that work on a machine several times as fast as well.
        byte[] input = TestContent.Generate(TestContent.Options(), 24L * 1024 * 1024);
        using CancellationTokenSource cancellation = new();
        SortProgress progress = new();
        Task sort = Task.Run(
            () => TestSort.RunCancellable(
                input,
                TestSort.Settings(
                    64 << 20, 1, LineEnding.Lf, segmentEntries: 1 << 20, blockBytes: 1 << 20, windowBytes: 8 << 20),
                cancellation.Token,
                progress: progress),
            TestContext.Current.CancellationToken);

        Assert.True(SpinWait.SpinUntil(() => progress.InputBytesRead == input.Length, TimeSpan.FromSeconds(30)));
        Thread.Sleep(delay);
        Stopwatch stopping = Stopwatch.StartNew();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => sort.GetAwaiter().GetResult());
        Assert.True(stopping.Elapsed < TimeSpan.FromSeconds(2), $"Stopped after {stopping.Elapsed}.");
    }
}
