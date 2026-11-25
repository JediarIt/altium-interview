using System.Diagnostics;

using FileSort.Engine.Blocks;
using FileSort.Engine.Chunks;
using FileSort.Engine.Chunks.Parsing;
using FileSort.Engine.Chunks.Reading;
using FileSort.Engine.Lines;
using FileSort.Engine.Merging;
using FileSort.Engine.Runs;

namespace FileSort.Engine;

/// <summary>
/// The sort, on any input and any run storage: the chunks, each read, parsed and
/// sorted on all threads and written as one run, then the merge of the runs into the output. An input that fits in
/// one chunk is written straight to the output, and an empty input gives an empty output.
/// </summary>
internal sealed class SortJob
{
    private readonly SortSettings _settings;
    private readonly SortProgress _progress;

    /// <exception cref="ArgumentException">The settings leave too little memory for the line limit, or are out of
    /// range.</exception>
    public SortJob(SortSettings settings, SortProgress progress)
    {
        Validate(settings);
        _settings = settings;
        _progress = progress;
    }

    /// <summary>The bytes of the chunk buffer: the memory less the window, at most the largest array of .NET.</summary>
    public static int ChunkBufferLength(SortSettings settings)
    {
        return (int)Math.Min(settings.MemoryBytes - settings.WindowBytes, Array.MaxLength);
    }

    /// <summary>
    /// The smallest buffer of a run in the merge: two longest lines with their terminators, which holds the current
    /// line of the run and leaves room for a read of sensible size. With lines of 1 MiB it is 2 MiB.
    /// </summary>
    public static int SmallestRunBuffer(int maxLineLength)
    {
        return 2 * (maxLineLength + 2);
    }

    /// <summary>The fewest bytes of the chunk buffer: at least two runs merge at once, and a longest line
    /// fits.</summary>
    public static long SmallestChunkBuffer(int maxLineLength, int threads)
    {
        return (2L * SmallestRunBuffer(maxLineLength)) + IndexLayout.MaxAlignment(threads);
    }

    public SortResult Sort(
        IInputSource input, IRunStorage storage, SpaceCheck space, CancellationToken cancellationToken)
    {
        ChunkBuffer buffer = new(ChunkBufferLength(_settings));
        BlockWindow window = new(_settings.WindowBytes, _settings.MaxLineLength + 2);
        InputStart start = InputStart.Read(input, _settings.LineEnding, _settings.MaxLineLength, buffer.Bytes);
        ParallelWork work = new(_settings.Threads, cancellationToken);
        _progress.StartChunks(input.Length);

        Stopwatch chunksTime = Stopwatch.StartNew();
        ChunkPhase chunks = WriteChunks(input, storage, buffer, window, start, work);
        chunksTime.Stop();

        Stopwatch mergeTime = Stopwatch.StartNew();
        int merges = Merge(chunks, storage, buffer, window, start, space, cancellationToken);
        mergeTime.Stop();

        return new SortResult
        {
            Lines = chunks.Lines,
            Bytes = chunks.Bytes,
            LineEnding = start.LineEnding,
            ByteOrderMark = start.ByteOrderMark,
            Runs = chunks.Runs.Count,
            MergePasses = merges,
            ChunksTime = chunksTime.Elapsed,
            MergeTime = mergeTime.Elapsed,
        };
    }

    // The first phase. The first chunk, when it reaches the end of the input, is the whole output.
    private ChunkPhase WriteChunks(
        IInputSource input,
        IRunStorage storage,
        ChunkBuffer buffer,
        BlockWindow window,
        InputStart start,
        ParallelWork work)
    {
        ChunkReader reader = new(
            input, buffer, start.FirstLineOffset, start.LineEnding, _settings.MaxLineLength,
            IndexLayout.MaxAlignment(_settings.Threads), _progress);
        ChunkMaker maker = new(
            reader, buffer, work, start.LineEnding, _settings.MaxLineLength, _settings.SegmentEntries);
        ChunkWriter writer = new(window, _settings.BlockBytes, start.LineEnding);
        ChunkPhase phase = new();

        while (maker.Next() is SortedChunk chunk)
        {
            phase.Lines += chunk.Lines;
            if (phase.Runs.Count == 0 && chunk.Bytes.EndsFile)
            {
                using Stream output = storage.CreateOutput();
                phase.Bytes = writer.Write(chunk, output, work);
                phase.WroteOutput = true;
                return phase;
            }

            using Stream run = storage.CreateRun(out int id);
            long length = writer.Write(chunk, run, work);
            phase.Runs.Add(new Run(id, length));
            phase.Bytes += length;
            _progress.RunWritten();
        }

        return phase;
    }

    // The second phase, when the first one wrote runs; an empty input gets an empty output here.
    private int Merge(
        ChunkPhase chunks,
        IRunStorage storage,
        ChunkBuffer buffer,
        BlockWindow window,
        InputStart start,
        SpaceCheck space,
        CancellationToken cancellationToken)
    {
        if (chunks.WroteOutput)
        {
            return 0;
        }

        if (chunks.Runs.Count == 0)
        {
            storage.CreateOutput().Dispose();
            return 0;
        }

        // The merge changes its list of runs as groups become runs; the result counts the runs of the chunks.
        RunMerger merger = new(storage, buffer, window.Bytes, start.LineEnding, _settings.LargestRunBuffer, _progress);
        int room = buffer.Length / SmallestRunBuffer(_settings.MaxLineLength);
        return new MergePhase(storage, merger, space, _progress, room).Merge([.. chunks.Runs], cancellationToken);
    }

    private static void Validate(SortSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.Threads, 1, nameof(settings.Threads));
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.MaxLineLength, 1, nameof(settings.MaxLineLength));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            settings.MaxLineLength, EntryShape.MaxLineLength, nameof(settings.MaxLineLength));
        ArgumentOutOfRangeException.ThrowIfLessThan(
            settings.WindowBytes, 2 * (settings.MaxLineLength + 2), nameof(settings.WindowBytes));
        ArgumentOutOfRangeException.ThrowIfLessThan(
            settings.MemoryBytes - settings.WindowBytes,
            SmallestChunkBuffer(settings.MaxLineLength, settings.Threads),
            nameof(settings.MemoryBytes));
        ArgumentOutOfRangeException.ThrowIfLessThan(
            settings.SegmentEntries, ChunkBuffer.EntriesPerCacheLines, nameof(settings.SegmentEntries));
        if (settings.SegmentEntries % ChunkBuffer.EntriesPerCacheLines != 0)
        {
            throw new ArgumentException(
                "A segment must be a whole number of cache lines of entries.", nameof(settings));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(settings.BlockBytes, 1, nameof(settings.BlockBytes));
        ArgumentOutOfRangeException.ThrowIfLessThan(
            settings.LargestRunBuffer, SmallestRunBuffer(settings.MaxLineLength), nameof(settings.LargestRunBuffer));
    }

    /// <summary>What the first phase did.</summary>
    private sealed class ChunkPhase
    {
        public long Lines { get; set; }

        public long Bytes { get; set; }

        public List<Run> Runs { get; } = [];

        /// <summary>The input fitted in one chunk, which was written as the output.</summary>
        public bool WroteOutput { get; set; }
    }
}
