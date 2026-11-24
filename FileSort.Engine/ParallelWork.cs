using System.Runtime.ExceptionServices;

namespace FileSort.Engine;

/// <summary>
/// Runs the items of a step of the sort, such as the ranges of a chunk, on up to the given number of threads at once,
/// and stops with the cancellation of the sort. A failure of an item leaves as it was thrown.
/// </summary>
internal sealed class ParallelWork(int threads, CancellationToken cancellationToken)
{
    private readonly int _threads = threads;
    private readonly CancellationToken _cancellationToken = cancellationToken;

    public int Threads => _threads;

    public CancellationToken CancellationToken => _cancellationToken;

    /// <summary>Runs <paramref name="body"/> for every item from 0 to <paramref name="count"/> - 1.</summary>
    public void For(int count, Action<int> body)
    {
        ParallelOptions options = new() { MaxDegreeOfParallelism = _threads, CancellationToken = _cancellationToken };
        try
        {
            Parallel.For(0, count, options, body);
        }
        catch (AggregateException e)
        {
            // The first failure that is not the cancellation, or the cancellation itself.
            Exception first = e.InnerExceptions.FirstOrDefault(inner => inner is not OperationCanceledException)
                ?? e.InnerExceptions[0];
            ExceptionDispatchInfo.Capture(first).Throw();
        }
    }
}
