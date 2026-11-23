using System.Runtime.ExceptionServices;

namespace FileSort.Checker.Cli;

/// <summary>
/// Work that runs on other threads while the calling thread stays free for the console: only that thread writes
/// to it, so what the work has to say is collected and printed by the watcher.
/// </summary>
internal static class BackgroundWork
{
    /// <summary>
    /// Runs the work and calls <paramref name="watch"/> on the calling thread, once at the start and then every
    /// <paramref name="interval"/>, until the work has ended. A failure of the work leaves as it was thrown. If the
    /// watcher fails, the work is cancelled and has ended before the failure leaves.
    /// </summary>
    public static T Run<T>(Func<CancellationToken, T> work, Action watch, TimeSpan interval, CancellationToken cancellationToken)
    {
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<T> task = Task.Run(() => work(stopping.Token), CancellationToken.None);
        try
        {
            do
            {
                watch();
            }
            while (!task.Wait(interval, CancellationToken.None));
        }
        catch (AggregateException e) when (task.IsCompleted)
        {
            ExceptionDispatchInfo.Capture(e.InnerExceptions[0]).Throw();
        }
        finally
        {
            // After a failure of the watcher the work must not go on. When the work has ended, this does nothing.
            stopping.Cancel();
            WaitForTheEnd(task);
        }

        return task.Result;
    }

    private static void WaitForTheEnd(Task task)
    {
        try
        {
            task.Wait(CancellationToken.None);
        }
        catch (AggregateException)
        {
            // How the work ended is reported where it was awaited; here it only has to be over.
        }
    }
}
