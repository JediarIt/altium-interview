using FileSort.Common;
using FileSort.Engine.Chunks;
using FileSort.Engine.Lines;
using FileSort.Engine.Runs;

namespace FileSort.Engine.Merging;

/// <summary>
/// One merge of D10 of the design: runs merged with a loser tree on one thread into one sorted sequence of lines,
/// written to a new run or to the output. The lines are copied as they lie in the runs, terminators included; the
/// readers parse them again, without the checks they passed when their chunk was made.
/// </summary>
internal sealed class RunMerger
{
    private readonly IRunStorage _storage;
    private readonly ChunkBuffer _buffer;
    private readonly byte[] _output;
    private readonly LineEnding _lineEnding;
    private readonly int _largestRunBuffer;
    private readonly SortProgress _progress;

    /// <param name="storage">The storage of the runs.</param>
    /// <param name="buffer">The chunk buffer, cut into the buffers of the runs.</param>
    /// <param name="output">The buffer of what is written: the window of the chunk phase.</param>
    /// <param name="lineEnding">The line ending of the lines in the runs.</param>
    /// <param name="largestRunBuffer">The most bytes a run gets for its buffer, however few runs there are.</param>
    /// <param name="progress">The counters of the bytes written.</param>
    public RunMerger(
        IRunStorage storage,
        ChunkBuffer buffer,
        byte[] output,
        LineEnding lineEnding,
        int largestRunBuffer,
        SortProgress progress)
    {
        _storage = storage;
        _buffer = buffer;
        _output = output;
        _lineEnding = lineEnding;
        _largestRunBuffer = largestRunBuffer;
        _progress = progress;
    }

    /// <summary>Merges the runs and writes the lines to the destination. Returns the bytes written.</summary>
    public long Merge(IReadOnlyList<Run> runs, Stream destination, CancellationToken cancellationToken)
    {
        RunReader[] readers = Readers(runs);
        LoserTree tree = new(runs.Count, new EntryComparer(_buffer.Bytes));
        for (int source = 0; source < readers.Length; source++)
        {
            if (readers[source].Next(cancellationToken, out IndexEntry head))
            {
                tree.SetHead(source, head);
            }
            else
            {
                tree.SetEmpty(source);
            }
        }

        tree.Start();
        long written = 0;
        int used = 0;
        while (!tree.Done)
        {
            RunReader reader = readers[tree.Winner];
            ReadOnlySpan<byte> line = reader.Line;
            if (used + line.Length > _output.Length)
            {
                written += Flush(destination, used, cancellationToken);
                used = 0;
            }

            line.CopyTo(_output.AsSpan(used));
            used += line.Length;
            MoveOn(tree, reader, cancellationToken);
        }

        return written + Flush(destination, used, cancellationToken);
    }

    // Every run gets an equal slice of the chunk buffer, but no more than the largest buffer.
    private RunReader[] Readers(IReadOnlyList<Run> runs)
    {
        int slice = (int)Math.Min(_largestRunBuffer, _buffer.Length / runs.Count);
        RunReader[] readers = new RunReader[runs.Count];
        for (int source = 0; source < runs.Count; source++)
        {
            readers[source] = new RunReader(_storage, runs[source], _buffer.Bytes, source * slice, slice, _lineEnding);
        }

        return readers;
    }

    private static void MoveOn(LoserTree tree, RunReader reader, CancellationToken cancellationToken)
    {
        if (reader.Next(cancellationToken, out IndexEntry next))
        {
            tree.Advance(next);
        }
        else
        {
            tree.Exhaust();
        }
    }

    private int Flush(Stream destination, int used, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        destination.Write(_output, 0, used);
        _progress.Wrote(used);
        return used;
    }
}
