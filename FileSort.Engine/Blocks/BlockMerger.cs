using FileSort.Engine.Chunks;
using FileSort.Engine.Lines;
using FileSort.Engine.Merging;

namespace FileSort.Engine.Blocks;

/// <summary>
/// Merges one block of a chunk: the entries of the block from every sorted segment, with a loser tree, into the lines
/// of the block, each followed by the terminator. One merger belongs to one worker thread, which creates it, so its
/// arrays never share a cache line with another thread's.
/// </summary>
internal sealed class BlockMerger
{
    private readonly SortedChunk _chunk;
    private readonly BlockPlan _plan;
    private readonly byte[] _terminator;
    private readonly LoserTree _tree;
    private readonly int[] _next;
    private readonly int[] _ends;

    public BlockMerger(SortedChunk chunk, BlockPlan plan, ReadOnlySpan<byte> terminator)
    {
        _chunk = chunk;
        _plan = plan;
        _terminator = terminator.ToArray();
        _tree = new LoserTree(plan.Segments, new EntryComparer(chunk.Buffer.Bytes));
        _next = new int[plan.Segments];
        _ends = new int[plan.Segments];
    }

    /// <summary>
    /// Writes the lines of the block into the output from the offset on. The room must hold the whole block, unless a
    /// sink is given: then the room is a piece buffer, and every time the next line does not fit, the lines so far go
    /// to the sink. Returns the bytes left in the room, which the caller writes when a sink is given.
    /// </summary>
    public int Merge(
        int block, byte[] output, int offset, int room, Action<int>? flush, CancellationToken cancellationToken)
    {
        Start(block);
        Span<IndexEntry> index = _chunk.Index;
        byte[] lines = _chunk.Buffer.Bytes;
        int position = offset;
        while (!_tree.Done)
        {
            int source = _tree.Winner;
            IndexEntry entry = _tree.WinnerHead;
            int length = entry.LineLength + _terminator.Length;
            if (position + length > offset + room)
            {
                position = Flush(flush, position - offset, offset, cancellationToken);
            }

            lines.AsSpan((int)entry.Offset, entry.LineLength).CopyTo(output.AsSpan(position));
            _terminator.CopyTo(output.AsSpan(position + entry.LineLength));
            position += length;
            MoveOn(source, index);
        }

        return position - offset;
    }

    // Every segment's part of the block is a source of the tree; an empty part is a source without lines.
    private void Start(int block)
    {
        Span<IndexEntry> index = _chunk.Index;
        for (int segment = 0; segment < _plan.Segments; segment++)
        {
            int start = _chunk.Segments[segment].Start;
            _next[segment] = start + _plan.From(block, segment);
            _ends[segment] = start + _plan.To(block, segment);
            if (_next[segment] < _ends[segment])
            {
                _tree.SetHead(segment, index[_next[segment]]);
            }
            else
            {
                _tree.SetEmpty(segment);
            }
        }

        _tree.Start();
    }

    private void MoveOn(int source, Span<IndexEntry> index)
    {
        int next = ++_next[source];
        if (next < _ends[source])
        {
            _tree.Advance(index[next]);
        }
        else
        {
            _tree.Exhaust();
        }
    }

    // A full piece goes to the sink, and the room is empty again. Without a sink the room held the whole block, so a
    // line that does not fit means a wrong size.
    private static int Flush(Action<int>? flush, int used, int offset, CancellationToken cancellationToken)
    {
        if (flush is null)
        {
            throw new InvalidOperationException("The buffer of a block is smaller than the block.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        flush(used);
        return offset;
    }
}
