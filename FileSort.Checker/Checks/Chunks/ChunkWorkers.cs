namespace FileSort.Checker.Checks.Chunks;

/// <summary>
/// The threads that check chunks, shared by the scans of the two files: at most a given number of chunks are handed
/// out at the same time, queued in the thread pool or being checked, whichever file they belong to. Each holds a
/// buffer until it is checked, so this number bounds the memory. A scan that has a chunk to hand out waits here until
/// one of the places is free, so it reads no further ahead than the chunks can be checked.
/// </summary>
internal sealed class ChunkWorkers(int threads) : IDisposable
{
    private readonly SemaphoreSlim _free = new(threads, threads);

    /// <summary>
    /// Starts the check of a chunk on a thread of the pool, as soon as one of the allowed threads is free.
    /// </summary>
    /// <exception cref="OperationCanceledException">The user cancelled the check while it waited.</exception>
    public Task<ChunkResult> Start(Func<ChunkResult> check, CancellationToken cancellationToken)
    {
        _free.Wait(cancellationToken);

        // The task itself is not cancelled: a chunk is checked in milliseconds.
        return Task.Run(
            () =>
            {
                try
                {
                    return check();
                }
                finally
                {
                    _free.Release();
                }
            },
            CancellationToken.None);
    }

    public void Dispose()
    {
        _free.Dispose();
    }
}
