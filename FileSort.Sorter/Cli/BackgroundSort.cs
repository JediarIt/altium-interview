namespace FileSort.Sorter.Cli;

/// <summary>
/// A sort on a thread of its own while the calling thread follows it, drawing its progress: only that thread writes to
/// the console while the sort runs.
/// </summary>
internal static class BackgroundSort
{
    /// <summary>
    /// Starts the sort, then calls <paramref name="follow"/> with it on the calling thread; <paramref name="follow"/>
    /// returns when the sort has ended. A failure of the sort leaves as it was thrown. If <paramref name="follow"/>
    /// fails, the sort is cancelled and has ended before the failure leaves, so nothing is still writing files when
    /// the program reports it. A sort that succeeded all the same, because it had passed its last look at the
    /// cancellation or had ended before the failure, has its output in place: its result is returned, and the
    /// failure of <paramref name="follow"/>, which can only be the console's, is dropped.
    /// </summary>
    public static T Run<T>(Func<CancellationToken, T> sort, Action<Task> follow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sort);
        ArgumentNullException.ThrowIfNull(follow);

        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<T> task = Task.Factory.StartNew(
            () => sort(stopping.Token), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            follow(task);
        }
        catch
        {
            stopping.Cancel();
            ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
            if (task.IsCompletedSuccessfully)
            {
                return task.Result;
            }

            throw;
        }

        return task.GetAwaiter().GetResult();
    }
}
