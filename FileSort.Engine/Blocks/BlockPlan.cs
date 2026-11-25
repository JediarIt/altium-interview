using System.Buffers;

using FileSort.Engine.Chunks;
using FileSort.Engine.Lines;

namespace FileSort.Engine.Blocks;

/// <summary>
/// The blocks of a chunk (D8 of the design): block b holds, from every sorted segment, the entries from splitter b - 1
/// up to splitter b. Every entry of a block comes after every entry of the blocks before it, so the blocks can be
/// merged at the same time and written one after another as one sorted run.
/// </summary>
/// <remarks>
/// An example: if the splitters are <c>Apple…</c> and <c>Banana…</c>, one block holds every line from <c>Apple…</c> to
/// just before <c>Banana…</c>, taken from every segment, and while one thread merges it, another merges the block from
/// <c>Banana…</c>.
/// <para>
/// The two large arrays of a plan, the samples and the bounds, are rented from the shared pool of arrays and given
/// back, the bounds when the plan is disposed. At the default memory a chunk has about 17,000 samples, 400 KB; an
/// array of 85,000 bytes or more lives on the heap of large objects, which only a rare full collection frees, so
/// arrays allocated for every chunk stayed in memory until the end of the sort: about 85 MB after the 195 chunks of
/// a 100 GB input.
/// </para>
/// </remarks>
internal sealed class BlockPlan : IDisposable
{
    private readonly long[] _sizes;
    private int[] _bounds;

    private BlockPlan(int blocks, int segments, int[] bounds, long[] sizes)
    {
        Blocks = blocks;
        Segments = segments;
        _bounds = bounds;
        _sizes = sizes;
    }

    public int Blocks { get; }

    public int Segments { get; }

    /// <summary>The bytes of all blocks: the run.</summary>
    public long TotalSize => _sizes.Sum();

    /// <summary>
    /// Plans about <paramref name="blocks"/> blocks of about equal numbers of lines. From every segment as many evenly
    /// spaced entries as there are blocks are taken as samples; every S-th of the sorted samples, S being the number
    /// of segments, is a splitter.
    /// </summary>
    public static BlockPlan Create(SortedChunk chunk, int blocks, int terminatorLength, ParallelWork work)
    {
        int segments = chunk.Segments.Count;
        blocks = Math.Max(1, blocks);
        EntryComparer comparer = new(chunk.Buffer.Bytes);
        Splitter[] splitters = Splitters(chunk, blocks, comparer);

        // Bound k of segment s is where block k starts in it: 0 for the first block, the length of the segment after
        // the last block, and in between the first entry that is not before splitter k. A rented array holds what its
        // last user left in it, so the bounds of the first block are cleared; all the others are written below.
        int[] bounds = ArrayPool<int>.Shared.Rent((blocks + 1) * segments);
        bounds.AsSpan(0, segments).Clear();
        for (int segment = 0; segment < segments; segment++)
        {
            bounds[(blocks * segments) + segment] = chunk.Segments[segment].Length;
        }

        // Splitter k ends block k: where it falls in every segment is where block k + 1 starts.
        work.For(blocks - 1, k =>
        {
            Span<int> nextBlockStarts = bounds.AsSpan((k + 1) * segments, segments);
            FindBounds(chunk, splitters[k], comparer, nextBlockStarts);
        });

        long[] sizes = new long[blocks];
        BlockPlan plan = new(blocks, segments, bounds, sizes);
        work.For(blocks, block => sizes[block] = plan.Measure(chunk, block, terminatorLength));
        return plan;
    }

    /// <summary>Where the block starts in a segment, counted from the start of the segment.</summary>
    public int From(int block, int segment) => _bounds[(block * Segments) + segment];

    /// <summary>Where the block ends in a segment, counted from the start of the segment.</summary>
    public int To(int block, int segment) => _bounds[((block + 1) * Segments) + segment];

    /// <summary>The bytes of the lines of a block, each with its terminator.</summary>
    public long Size(int block) => _sizes[block];

    /// <summary>The entries of a block, from all segments.</summary>
    public long Lines(int block)
    {
        long lines = 0;
        for (int segment = 0; segment < Segments; segment++)
        {
            lines += To(block, segment) - From(block, segment);
        }

        return lines;
    }

    /// <summary>Gives the array of the bounds back to the pool; the plan answers no question after that.</summary>
    public void Dispose()
    {
        if (_bounds.Length > 0)
        {
            ArrayPool<int>.Shared.Return(_bounds);
            _bounds = [];
        }
    }

    // The samples in the strict order, and every S-th of them as a splitter: blocks - 1 splitters.
    private static Splitter[] Splitters(SortedChunk chunk, int blocks, EntryComparer comparer)
    {
        int segments = chunk.Segments.Count;
        Splitter[] rented = ArrayPool<Splitter>.Shared.Rent(segments * blocks);
        try
        {
            Span<Splitter> samples = rented.AsSpan(0, segments * blocks);
            TakeSamples(chunk, blocks, samples);
            samples.Sort((x, y) => x.CompareTo(in y, comparer));
            Splitter[] splitters = new Splitter[blocks - 1];
            for (int k = 0; k < splitters.Length; k++)
            {
                splitters[k] = samples[(k + 1) * segments];
            }

            return splitters;
        }
        finally
        {
            ArrayPool<Splitter>.Shared.Return(rented);
        }
    }

    // From every segment, as many evenly spaced entries as there are blocks, one segment after another.
    private static void TakeSamples(SortedChunk chunk, int blocks, Span<Splitter> samples)
    {
        for (int segment = 0; segment < chunk.Segments.Count; segment++)
        {
            Span<IndexEntry> entries = chunk.Entries(chunk.Segments[segment]);
            Span<Splitter> ofSegment = samples.Slice(segment * blocks, blocks);
            for (int sample = 0; sample < blocks; sample++)
            {
                int index = (int)((long)sample * entries.Length / blocks);
                ofSegment[sample] = new Splitter(entries[index], segment, index);
            }
        }
    }

    // In every segment, the first entry that is not before the splitter.
    private static void FindBounds(SortedChunk chunk, Splitter splitter, EntryComparer comparer, Span<int> bounds)
    {
        for (int segment = 0; segment < bounds.Length; segment++)
        {
            bounds[segment] = FirstNotBefore(chunk.Entries(chunk.Segments[segment]), segment, splitter, comparer);
        }
    }

    // A binary search in the strict order: within a sorted segment it is the order of the entries, because entries
    // that compare equal are told apart by their index.
    private static int FirstNotBefore(Span<IndexEntry> entries, int segment, Splitter splitter, EntryComparer comparer)
    {
        int low = 0;
        int high = entries.Length;
        while (low < high)
        {
            int middle = (int)(((uint)low + (uint)high) / 2);
            if (splitter.CompareTo(in entries[middle], segment, middle, comparer) > 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    // The exact size of a block, summed from the lengths in its entries.
    private long Measure(SortedChunk chunk, int block, int terminatorLength)
    {
        long size = 0;
        for (int segment = 0; segment < Segments; segment++)
        {
            Span<IndexEntry> entries = chunk.Entries(chunk.Segments[segment]);
            for (int index = From(block, segment); index < To(block, segment); index++)
            {
                size += entries[index].LineLength + terminatorLength;
            }
        }

        return size;
    }
}
