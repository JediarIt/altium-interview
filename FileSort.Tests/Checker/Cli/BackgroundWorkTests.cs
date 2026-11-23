using FileSort.Checker.Cli;

namespace FileSort.Tests.Checker.Cli;

public class BackgroundWorkTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(10);

    [Fact]
    public void The_result_of_the_work_is_returned_and_the_watcher_runs_at_least_once()
    {
        int watched = 0;

        int result = BackgroundWork.Run(_ => 42, () => watched++, Interval, CancellationToken.None);

        Assert.Equal(42, result);
        Assert.True(watched >= 1);
    }

    [Fact]
    public void The_watcher_runs_on_the_calling_thread_while_the_work_runs_on_another()
    {
        int caller = Environment.CurrentManagedThreadId;
        int watcher = -1;
        using ManualResetEventSlim watchedTwice = new();
        int watched = 0;

        int worker = BackgroundWork.Run(
            token =>
            {
                watchedTwice.Wait(TimeSpan.FromSeconds(30), token);
                return Environment.CurrentManagedThreadId;
            },
            () =>
            {
                watcher = Environment.CurrentManagedThreadId;
                if (++watched == 2)
                {
                    watchedTwice.Set();
                }
            },
            Interval,
            CancellationToken.None);

        // The work did not end before the watcher had run twice: the watcher keeps running while the work does.
        Assert.Equal(caller, watcher);
        Assert.NotEqual(caller, worker);
        Assert.True(watched >= 2);
    }

    [Fact]
    public void A_failure_of_the_work_leaves_as_it_was_thrown()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(
            () => BackgroundWork.Run<int>(_ => throw new InvalidOperationException("Something broke."), () => { }, Interval, CancellationToken.None));

        Assert.Equal("Something broke.", e.Message);
    }

    [Fact]
    public void A_cancellation_reaches_the_work_and_leaves_as_a_cancellation()
    {
        using CancellationTokenSource cancellation = new();

        Assert.ThrowsAny<OperationCanceledException>(() => BackgroundWork.Run(
            token =>
            {
                token.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                token.ThrowIfCancellationRequested();
                return 0;
            },
            cancellation.Cancel,
            Interval,
            cancellation.Token));
    }

    [Fact]
    public void A_failure_of_the_watcher_stops_the_work_before_it_leaves()
    {
        bool workEnded = false;

        IOException e = Assert.Throws<IOException>(() => BackgroundWork.Run(
            token =>
            {
                // Runs until it is told to stop, like a scan of a large file.
                token.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                workEnded = token.IsCancellationRequested;
                return 0;
            },
            () => throw new IOException("The console is broken."),
            Interval,
            CancellationToken.None));

        Assert.Equal("The console is broken.", e.Message);
        Assert.True(workEnded, "The work was still running, or was not cancelled, when the failure left.");
    }
}
