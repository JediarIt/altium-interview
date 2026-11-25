namespace FileSort.Engine.Blocks;

/// <summary>
/// The order in which the blocks of a chunk start and are written, so that their buffers stay within the window and
/// no thread waits for another in a circle. Blocks start strictly in order: block b starts only after block b - 1 has
/// started, and only when its exact size fits in what the window has left. One writer writes them in order and frees
/// the window as it goes. Every block before a waiting one has started and will finish, so room always comes.
/// </summary>
/// <remarks>
/// A block larger than a quarter of the window gets no buffer: it starts only when it is the next block to be
/// written, and its worker writes it itself through the piece buffer. No later block starts until it has started, so
/// nothing can fill the window behind it. A block without lines needs no buffer either.
/// </remarks>
internal sealed class BlockSchedule(BlockPlan plan, BlockWindow window)
{
    /// <summary>The block number that <see cref="StartNext"/> returns when no block is left to start.</summary>
    public const int None = -1;

    private readonly object _lock = new();
    private readonly BlockPlan _plan = plan;
    private readonly BlockWindow _window = window;
    private readonly int[] _offsets = new int[plan.Blocks];
    private readonly bool[] _merged = new bool[plan.Blocks];
    private int _nextToStart;
    private int _written;
    private bool _stopped;

    /// <summary>Whether a block is written by its worker through the piece buffer.</summary>
    public bool IsLarge(int block) => _plan.Size(block) > _window.LargestBuffered;

    /// <summary>Whether a block needs a buffer in the window: it has lines and is not large.</summary>
    public bool IsBuffered(int block) => _plan.Size(block) > 0 && !IsLarge(block);

    /// <summary>
    /// For a worker: waits until the next block can start, starts it and returns its number, with the offset of its
    /// buffer in the window; returns <see cref="None"/> when all blocks have started or the schedule was stopped.
    /// </summary>
    public int StartNext(out int offset)
    {
        offset = 0;
        lock (_lock)
        {
            while (!_stopped && _nextToStart < _plan.Blocks)
            {
                int block = _nextToStart;
                if (CanStart(block, out offset))
                {
                    _offsets[block] = offset;
                    _nextToStart++;
                    Monitor.PulseAll(_lock);
                    return block;
                }

                Monitor.Wait(_lock);
            }

            return None;
        }
    }

    /// <summary>For a worker: a block that is not large has been merged into its buffer.</summary>
    public void Merged(int block)
    {
        lock (_lock)
        {
            _merged[block] = true;
            Monitor.PulseAll(_lock);
        }
    }

    /// <summary>For the worker of a large block: the block has been written.</summary>
    public void LargeWritten(int block)
    {
        lock (_lock)
        {
            _written = block + 1;
            Monitor.PulseAll(_lock);
        }
    }

    /// <summary>
    /// For the writer: waits until a block can be written, and returns the offset of its buffer; false when the
    /// schedule was stopped. A large block has been written by its worker by then.
    /// </summary>
    public bool WaitForBlock(int block, out int offset)
    {
        lock (_lock)
        {
            while (!_stopped && !_merged[block] && _written <= block)
            {
                Monitor.Wait(_lock);
            }

            offset = _offsets[block];
            return !_stopped;
        }
    }

    /// <summary>For the writer: a block has been written, and its buffer is free.</summary>
    public void Written(int block)
    {
        lock (_lock)
        {
            if (IsBuffered(block))
            {
                _window.Free(_offsets[block]);
            }

            _written = Math.Max(_written, block + 1);
            Monitor.PulseAll(_lock);
        }
    }

    /// <summary>Makes every thread that waits leave: a failure, or the sort was cancelled.</summary>
    public void Stop()
    {
        lock (_lock)
        {
            _stopped = true;
            Monitor.PulseAll(_lock);
        }
    }

    // A block without lines starts at once; a large block when every block before it is written; any other block when
    // the window has room for its buffer.
    private bool CanStart(int block, out int offset)
    {
        offset = 0;
        if (_plan.Size(block) == 0)
        {
            return true;
        }

        return IsLarge(block) ? _written == block : _window.TryReserve((int)_plan.Size(block), out offset);
    }
}
