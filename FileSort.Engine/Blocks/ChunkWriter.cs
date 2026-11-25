using System.Runtime.ExceptionServices;

using FileSort.Common;
using FileSort.Engine.Chunks;

namespace FileSort.Engine.Blocks;

/// <summary>
/// Writes a sorted chunk as one sorted sequence of lines: its blocks are merged by worker threads at the same time and
/// written in order by one writer thread (D8 of the design). Merging the segments on one thread would cost more than
/// all the rest of the work on the chunk.
/// </summary>
/// <param name="window">The window of the block buffers.</param>
/// <param name="blockBytes">The bytes of lines a block aims at.</param>
/// <param name="lineEnding">The line ending, whose terminator follows every line written.</param>
internal sealed class ChunkWriter(BlockWindow window, int blockBytes, LineEnding lineEnding)
{
    private readonly BlockWindow _window = window;
    private readonly int _blockBytes = blockBytes;
    private readonly byte[] _terminator = lineEnding.Terminator().ToArray();

    /// <summary>Writes the lines of the chunk in order, each followed by the terminator, and returns their
    /// bytes.</summary>
    public long Write(SortedChunk chunk, Stream destination, ParallelWork work)
    {
        using BlockPlan plan = BlockPlan.Create(chunk, BlockCount(chunk), _terminator.Length, work);
        BlockSchedule schedule = new(plan, _window);
        CancellationToken cancellationToken = work.CancellationToken;
        using CancellationTokenRegistration stopOnCancel = cancellationToken.Register(schedule.Stop);
        ChunkWrite write = new(chunk, plan, schedule, destination, _window, _terminator, cancellationToken);

        // No more workers than blocks: a small chunk does not pay for threads that would find nothing to do.
        int workers = Math.Min(work.Threads, plan.Blocks);
        List<Task> threads = [];
        for (int worker = 0; worker < workers; worker++)
        {
            threads.Add(Start(write.MergeBlocks, schedule));
        }

        threads.Add(Start(write.WriteInOrder, schedule));
        WaitForAll(threads);
        cancellationToken.ThrowIfCancellationRequested();
        return plan.TotalSize;
    }

    // One block for every blockBytes of lines, the last one shorter, and at least one.
    private int BlockCount(SortedChunk chunk)
    {
        long blocks = (chunk.Bytes.Length + (long)_blockBytes - 1) / _blockBytes;
        return (int)Math.Max(1, blocks);
    }

    // A thread of its own for every worker and for the writer: they wait for each other, which a thread of the pool
    // should not do. A thread that fails stops the others.
    private static Task Start(Action body, BlockSchedule schedule)
    {
        return Task.Factory.StartNew(
            () =>
            {
                try
                {
                    body();
                }
                catch
                {
                    schedule.Stop();
                    throw;
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    // The first failure that is not the cancellation, or the cancellation itself.
    private static void WaitForAll(List<Task> threads)
    {
        try
        {
            Task.WaitAll(threads);
        }
        catch (AggregateException e)
        {
            Exception first = e.InnerExceptions.FirstOrDefault(inner => inner is not OperationCanceledException)
                ?? e.InnerExceptions[0];
            ExceptionDispatchInfo.Capture(first).Throw();
        }
    }

    /// <summary>The write of one chunk: what its workers and its writer share.</summary>
    private sealed class ChunkWrite(
        SortedChunk chunk,
        BlockPlan plan,
        BlockSchedule schedule,
        Stream destination,
        BlockWindow window,
        byte[] terminator,
        CancellationToken cancellationToken)
    {
        private readonly SortedChunk _chunk = chunk;
        private readonly BlockPlan _plan = plan;
        private readonly BlockSchedule _schedule = schedule;
        private readonly Stream _destination = destination;
        private readonly BlockWindow _window = window;
        private readonly byte[] _terminator = terminator;
        private readonly CancellationToken _cancellationToken = cancellationToken;

        // A worker: takes the blocks in the order they may start and merges each one, into its buffer in the window
        // or, for a large block, piece by piece straight to the destination.
        public void MergeBlocks()
        {
            BlockMerger merger = new(_chunk, _plan, _terminator);
            int block;
            while ((block = _schedule.StartNext(out int offset)) != BlockSchedule.None)
            {
                if (_schedule.IsLarge(block))
                {
                    MergeLarge(merger, block);
                    continue;
                }

                merger.Merge(block, _window.Bytes, offset, (int)_plan.Size(block), flush: null, _cancellationToken);
                _schedule.Merged(block);
            }
        }

        // The writer: every block in order, as soon as it is merged; a large block has been written by its worker.
        public void WriteInOrder()
        {
            for (int block = 0; block < _plan.Blocks; block++)
            {
                if (!_schedule.WaitForBlock(block, out int offset))
                {
                    return;
                }

                if (_schedule.IsBuffered(block))
                {
                    _destination.Write(_window.Bytes, offset, (int)_plan.Size(block));
                }

                _schedule.Written(block);
            }
        }

        // A large block goes through the piece buffer of the window: every full piece is written as it fills, and the
        // last one after the merge.
        private void MergeLarge(BlockMerger merger, int block)
        {
            int left = merger.Merge(
                block, _window.Bytes, _window.PieceOffset, _window.PieceLength, WritePiece, _cancellationToken);
            WritePiece(left);
            _schedule.LargeWritten(block);
        }

        private void WritePiece(int used)
        {
            _destination.Write(_window.Bytes, _window.PieceOffset, used);
        }
    }
}
