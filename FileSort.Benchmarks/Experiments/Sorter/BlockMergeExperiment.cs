using System.CommandLine;
using System.Diagnostics;
using System.Globalization;

using FileSort.Benchmarks.Micro.Sorter;
using FileSort.Common;
using FileSort.Engine;
using FileSort.Engine.Blocks;
using FileSort.Engine.Chunks;
using FileSort.Engine.Chunks.Parsing;
using FileSort.Engine.Chunks.Reading;
using FileSort.Generator.Content;

namespace FileSort.Benchmarks.Experiments.Sorter;

/// <summary>
/// The write of one sorted chunk by the block merge, in memory, for several sizes of a block and
/// thread counts. The chunk is default content made by the generator, parsed and sorted once per thread count, then
/// written again and again to a stream that keeps nothing: what is measured is the merge of the blocks and the copy of
/// their lines, not a disk.
/// </summary>
internal static class BlockMergeExperiment
{
    private const int KiB = 1024;

    public static Command CreateCommand()
    {
        Option<long> size = ExperimentOptions.Size(
            "--size", "The bytes of default content in the chunk, made in memory: 64MB, 256MB.", 64L << 20);
        Option<int[]> threads = ExperimentOptions.Numbers(
            "--threads", "Thread counts, separated by commas.", 1, 2, 4, 8);
        Option<int[]> blocks = ExperimentOptions.Numbers(
            "--blocks", "Sizes of a block in KiB, separated by commas.", 256, 1024, 4096);
        Option<int> runs = ExperimentOptions.Runs(3);

        Command command = new(
            "block-merge",
            "The write of one sorted chunk by the block merge, in memory, for several sizes of a block and thread "
            + "counts. It writes nothing to the disk.")
        {
            size, threads, blocks, runs,
        };
        command.SetAction(result => Run(
            result.GetValue(size), result.GetValue(threads)!, result.GetValue(blocks)!, result.GetValue(runs)));
        return command;
    }

    private static void Run(long size, int[] threads, int[] blocks, int runs)
    {
        byte[] input = GeneratedContent.Create(size, EdgeCases.None);
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"A chunk of {Measure.Size(input.Length)} of default content, {runs} runs per row."));
        Measure.Header("Threads", "Block, KiB", "Median, ms", "Min", "Max", "Median, MiB/s", "Runs");

        foreach (int threadCount in threads)
        {
            ChunkBuffer buffer = new((int)Math.Min(Array.MaxLength, (3L * input.Length) + (1 << 20)));
            SortedChunk chunk = Chunk(input, buffer, threadCount);
            foreach (int block in blocks)
            {
                List<double> milliseconds = [];
                for (int run = 0; run < runs; run++)
                {
                    milliseconds.Add(WriteOnce(chunk, threadCount, block * KiB));
                }

                RunStatistics time = RunStatistics.From(milliseconds);
                double throughput = input.Length / (double)Measure.Mebibyte / (time.Median / 1000);
                Measure.Row(threadCount, block, F1(time.Median), F1(time.Min), F1(time.Max), throughput, time.Runs);
            }
        }
    }

    // The chunk as the engine makes it with so many threads: one range each, sorted in segments.
    private static SortedChunk Chunk(byte[] input, ChunkBuffer buffer, int threads)
    {
        MemoryInput source = new(input);
        int maxLineLength = SortSettings.DefaultMaxLineLength;
        ChunkReader reader = new(
            source, buffer, 0, LineEnding.Lf, maxLineLength, IndexLayout.MaxAlignment(threads), new SortProgress());
        ParallelWork work = new(threads, CancellationToken.None);
        ChunkMaker maker = new(reader, buffer, work, LineEnding.Lf, maxLineLength, segmentSize: 1 << 20);
        SortedChunk chunk = maker.Next() ?? throw new InvalidOperationException("The content made no chunk.");
        if (!chunk.Bytes.EndsFile)
        {
            throw new InvalidOperationException("The content does not fit in one chunk.");
        }

        return chunk;
    }

    private static double WriteOnce(SortedChunk chunk, int threads, int blockBytes)
    {
        BlockWindow window = new(SortSettings.DefaultWindowBytes, SortSettings.DefaultMaxLineLength + 2);
        ChunkWriter writer = new(window, blockBytes, LineEnding.Lf);
        Stopwatch stopwatch = Stopwatch.StartNew();
        writer.Write(chunk, Stream.Null, new ParallelWork(threads, CancellationToken.None));
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static string F1(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
