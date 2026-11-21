using FileSort.Generator.Content.Blocks;

namespace FileSort.Generator.Pipeline;

/// <summary>
/// The blocks that are being generated right now: a bounded number of thread-pool tasks, taken in the order of
/// their indices. The window owns the block buffers and reuses them, so a run holds one buffer per block in flight
/// and one for the block that the caller is writing.
/// </summary>
internal sealed class BlockWindow : IDisposable
{
    private readonly BlockGenerator _generator;
    private readonly int _blockCapacity;
    private readonly int _capacity;
    private readonly long _maxBlocks;
    private readonly Stack<byte[]> _freeBuffers;
    private readonly Queue<(Task<BlockContent> Task, byte[] Buffer)> _inFlight;
    private readonly CancellationTokenSource _stop;
    private long _nextBlock;

    /// <param name="generator">Produces the content of a block from its index.</param>
    /// <param name="blockCapacity">The size of a block buffer in bytes.</param>
    /// <param name="threads">The upper limit on blocks generated at the same time.</param>
    /// <param name="maxBlocks">Blocks with this index and above are never started.</param>
    /// <param name="cancellationToken">Stops the blocks in flight.</param>
    public BlockWindow(
        BlockGenerator generator,
        int blockCapacity,
        int threads,
        long maxBlocks,
        CancellationToken cancellationToken)
    {
        _generator = generator;
        _blockCapacity = blockCapacity;
        _capacity = (int)Math.Min(threads, maxBlocks);
        _maxBlocks = maxBlocks;
        _freeBuffers = new Stack<byte[]>(_capacity + 1);
        _inFlight = new Queue<(Task<BlockContent> Task, byte[] Buffer)>(_capacity);
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    }

    /// <summary>
    /// Waits for the oldest block in flight and returns its buffer and what the buffer holds. The window is full
    /// again before the method returns, so generation goes on while the caller writes the block. The caller gives
    /// the buffer back with <see cref="Recycle"/>.
    /// </summary>
    public (byte[] Buffer, BlockContent Content) TakeNext()
    {
        Fill();
        (Task<BlockContent> task, byte[] buffer) = _inFlight.Dequeue();
        BlockContent content = task.GetAwaiter().GetResult();
        Fill();
        return (buffer, content);
    }

    /// <summary>Returns the buffer of a block that the caller has finished with, for a later block.</summary>
    public void Recycle(byte[] buffer) => _freeBuffers.Push(buffer);

    /// <summary>
    /// Stops the blocks in flight and waits for them, so that nothing this window started still runs when the caller
    /// moves on: after a failure the remaining blocks do not keep the processor busy, and a block that failed for any
    /// reason other than the stop is reported, not lost.
    /// </summary>
    public void Dispose()
    {
        try
        {
            _stop.Cancel();
            foreach ((Task<BlockContent> task, _) in _inFlight)
            {
                WaitForStopped(task);
            }
        }
        finally
        {
            _stop.Dispose();
        }
    }

    private void Fill()
    {
        while (_inFlight.Count < _capacity && _nextBlock < _maxBlocks)
        {
            Start(_nextBlock++);
        }
    }

    private void Start(long index)
    {
        byte[] buffer = _freeBuffers.TryPop(out byte[]? free) ? free : new byte[_blockCapacity];
        CancellationToken stop = _stop.Token;
        Task<BlockContent> task = Task.Run(() => Generate(index, buffer, stop), stop);
        _inFlight.Enqueue((task, buffer));
    }

    private BlockContent Generate(long index, byte[] buffer, CancellationToken stop)
    {
        stop.ThrowIfCancellationRequested();
        return _generator.Generate(index, buffer);
    }

    // A block that was stopped ends with a cancellation, which is the expected outcome here; any other failure of
    // a block is an error and is not hidden.
    private static void WaitForStopped(Task task)
    {
        try
        {
            task.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
    }
}
