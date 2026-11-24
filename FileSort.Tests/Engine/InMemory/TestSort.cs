using FileSort.Common;
using FileSort.Engine;
using FileSort.Engine.Runs;
using FileSort.Tests.Format;

namespace FileSort.Tests.Engine.InMemory;

/// <summary>
/// Sorts in memory, with the input in memory and the runs and the output in a <see cref="MemoryRunStorage"/>: the
/// whole engine, with buffers as small as the tests like, and no disk.
/// </summary>
internal static class TestSort
{
    /// <summary>The line limit of the tests: small, so that small buffers hold several longest lines.</summary>
    public const int MaxLineLength = 100;

    /// <summary>Settings for a sort in memory with the given chunk buffer; small segments, blocks and window.</summary>
    public static SortSettings Settings(
        int chunkBuffer,
        int threads,
        LineEnding? lineEnding = null,
        int maxLineLength = MaxLineLength,
        int segmentEntries = 8,
        int blockBytes = 256,
        int windowBytes = 0)
    {
        int window = windowBytes > 0 ? windowBytes : Math.Max(4096, 4 * (maxLineLength + 2));
        return new SortSettings
        {
            InputPath = "input.txt",
            OutputPath = "output.txt",
            MemoryBytes = chunkBuffer + window,
            WindowBytes = window,
            Threads = threads,
            LineEnding = lineEnding,
            MaxLineLength = maxLineLength,
            SegmentEntries = segmentEntries,
            BlockBytes = blockBytes,
            LargestRunBuffer = 16 * 1024,
            FreeSpace = _ => null,
        };
    }

    /// <summary>The smallest chunk buffer that the settings of the tests allow with so many threads.</summary>
    public static int SmallestChunkBuffer(int threads, int maxLineLength = MaxLineLength)
    {
        return (int)SortJob.SmallestChunkBuffer(maxLineLength, threads);
    }

    /// <summary>Sorts the input with the settings and returns what the sort did and wrote.</summary>
    public static SortOutcome Run(byte[] input, SortSettings settings)
    {
        return RunCancellable(input, settings, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Sorts the input with the settings, for a test that cancels the sort: it can give the storage and the counters
    /// of the sort, to cancel at a moment of its choice.
    /// </summary>
    public static SortOutcome RunCancellable(
        byte[] input,
        SortSettings settings,
        CancellationToken cancellationToken,
        MemoryRunStorage? storage = null,
        SortProgress? progress = null)
    {
        storage ??= new MemoryRunStorage();
        progress ??= new SortProgress();
        SpaceCheck space = new(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "output.txt"), settings.FreeSpace);
        SortJob job = new(settings, progress);
        SortResult result = job.Sort(new MemoryInput(input), storage, space, cancellationToken);
        byte[] output = storage.Output ?? throw new InvalidOperationException("No output was written.");
        return new SortOutcome(result, output, storage, progress);
    }

    /// <summary>What a correct sort writes for the input: the naive sort with the tie-break by bytes.</summary>
    public static byte[] Expected(byte[] input, LineEnding lineEnding)
    {
        return NaiveSort.SortFile(input, lineEnding == LineEnding.CrLf ? "\r\n" : "\n");
    }
}
