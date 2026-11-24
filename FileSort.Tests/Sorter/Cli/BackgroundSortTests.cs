using FileSort.Cli.Terminal;
using FileSort.Sorter.Cli;

namespace FileSort.Tests.Sorter.Cli;

/// <summary>The sort on a thread of its own, followed by the calling thread.</summary>
public class BackgroundSortTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    [Fact]
    public void The_result_of_the_sort_is_returned_after_the_follower_has_seen_its_end()
    {
        int caller = Environment.CurrentManagedThreadId;
        bool endedWhenFollowed = false;

        int sorter = BackgroundSort.Run(
            _ => Environment.CurrentManagedThreadId,
            sort => endedWhenFollowed = ((IAsyncResult)sort).AsyncWaitHandle.WaitOne(Patience),
            TestContext.Current.CancellationToken);

        Assert.True(endedWhenFollowed);
        Assert.NotEqual(caller, sorter);
    }

    [Fact]
    public void A_failure_of_the_sort_leaves_as_it_was_thrown()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => BackgroundSort.Run<int>(
            _ => throw new InvalidOperationException("The sort broke."),
            sort => ((IAsyncResult)sort).AsyncWaitHandle.WaitOne(Patience),
            TestContext.Current.CancellationToken));

        Assert.Equal("The sort broke.", e.Message);
    }

    [Fact]
    public void A_failure_of_the_follower_cancels_the_sort_and_leaves_after_the_sort_has_ended()
    {
        using ManualResetEventSlim started = new();
        bool ended = false;

        Assert.Throws<ConsoleWriteException>(() => BackgroundSort.Run(
            token =>
            {
                started.Set();
                token.WaitHandle.WaitOne(Patience);
                Thread.Sleep(50);
                ended = true;
                token.ThrowIfCancellationRequested();
                return 0;
            },
            _ =>
            {
                started.Wait(Patience, TestContext.Current.CancellationToken);
                throw new ConsoleWriteException(new IOException("The console is gone."));
            },
            TestContext.Current.CancellationToken));

        Assert.True(ended);
    }

    [Fact]
    public void A_sort_that_succeeded_before_the_follower_failed_keeps_its_result()
    {
        // The output is in place: the console that failed while the last bars were drawn costs the bars, not the
        // exit code.
        int result = BackgroundSort.Run(
            _ => 42,
            sort =>
            {
                ((IAsyncResult)sort).AsyncWaitHandle.WaitOne(Patience);
                throw new ConsoleWriteException(new IOException("The console is gone."));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
    }
}
