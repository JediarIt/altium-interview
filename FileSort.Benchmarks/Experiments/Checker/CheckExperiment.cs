using System.CommandLine;
using System.Diagnostics;
using System.Globalization;

using FileSort.Checker.Checks;
using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Cli;

namespace FileSort.Benchmarks.Experiments.Checker;

/// <summary>
/// The whole check of a pair of files, without the checker's command line, for several sizes of a chunk and several
/// thread counts: what the size of a chunk changes, and how the check scales with threads. The files are only read, so
/// a series wears nothing; after the first run
/// they come from the file cache of the operating system, and the series measures the processor cost of the check.
/// </summary>
internal static class CheckExperiment
{
    private const int Mebibyte = 1024 * 1024;

    public static Command CreateCommand()
    {
        Option<string> input = new("--input")
        {
            Description = "The input file of the pair.",
            HelpName = "path",
            Required = true,
        };
        Option<string> sorted = new("--sorted")
        {
            Description = "The sorted file of the pair, for example one made by sorted-copy.",
            HelpName = "path",
            Required = true,
        };
        Option<int[]> chunks = ExperimentOptions.Numbers(
            "--chunks", "Sizes of a chunk in MiB, separated by commas.", 1, 4, 16);
        Option<int[]> threads = ExperimentOptions.Numbers(
            "--threads", "Thread counts, separated by commas. The default is the number of logical processors.",
            ScanSettings.DefaultThreads);
        Option<int> runs = ExperimentOptions.Runs(5);

        Command command = new(
            "check",
            "The check of a pair of files for several sizes of a chunk and several thread counts. It only reads "
            + "the files.")
        {
            input, sorted, chunks, threads, runs,
        };
        command.SetAction(result => Run(
            Path.GetFullPath(result.GetValue(input)!),
            Path.GetFullPath(result.GetValue(sorted)!),
            result.GetValue(chunks)!,
            result.GetValue(threads)!,
            result.GetValue(runs)));
        return command;
    }

    private static void Run(string inputPath, string sortedPath, int[] chunks, int[] threads, int runs)
    {
        Console.WriteLine($"Input: {inputPath}, sorted: {sortedPath}");
        Measure.Header("Chunk, MiB", "Threads", "Median, s", "Median, MiB/s", "Min", "Max", "Runs");

        foreach (int chunk in chunks)
        {
            foreach (int threadCount in threads)
            {
                MeasureRow(inputPath, sortedPath, chunk, threadCount, runs);
            }
        }
    }

    // One row of the table: the runs of one size of a chunk and one thread count.
    private static void MeasureRow(string inputPath, string sortedPath, int chunk, int threadCount, int runs)
    {
        List<double> seconds = [];
        List<double> throughputs = [];
        for (int run = 0; run < runs; run++)
        {
            (long bytes, TimeSpan elapsed) = CheckOnce(inputPath, sortedPath, chunk * Mebibyte, threadCount);
            seconds.Add(elapsed.TotalSeconds);
            throughputs.Add(Measure.ToThroughput(bytes, elapsed));
        }

        RunStatistics throughput = RunStatistics.From(throughputs);
        string medianSeconds = RunStatistics.From(seconds).Median.ToString("F2", CultureInfo.InvariantCulture);
        Measure.Row(
            chunk, threadCount, medianSeconds, throughput.Median, throughput.Min, throughput.Max, throughput.Runs);
    }

    // The files are opened the way the checker opens them, and the time covers what the checker times.
    private static (long Bytes, TimeSpan Elapsed) CheckOnce(
        string inputPath, string sortedPath, int chunkSize, int threads)
    {
        using PairFiles files = PairFiles.Open(inputPath, sortedPath);
        ScanSettings settings = new() { ChunkSize = chunkSize, Threads = threads };

        Stopwatch stopwatch = Stopwatch.StartNew();
        CheckResult result = PairCheck.Run(
            files.Input, files.Sorted, lineEnding: null, settings, new ScanFeed(), CancellationToken.None);
        TimeSpan elapsed = stopwatch.Elapsed;

        if (result.Outcome != CheckOutcome.Correct)
        {
            throw new InvalidDataException(
                $"The pair is not a correct one ({result.Outcome}): its time would mean nothing.");
        }

        return (files.TotalSize, elapsed);
    }
}
