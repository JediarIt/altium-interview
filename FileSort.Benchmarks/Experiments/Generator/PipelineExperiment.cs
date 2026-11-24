using System.CommandLine;
using System.Diagnostics;

using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Generator.Output;
using FileSort.Generator.Pipeline;

namespace FileSort.Benchmarks.Experiments.Generator;

/// <summary>
/// The generation pipeline without the command line, into a sink that discards the output or into a file: how
/// generation scales with threads, where the disk becomes the limit, and what the block capacity changes.
/// </summary>
internal static class PipelineExperiment
{
    private const string Discard = "discard";
    private const string ToFile = "file";

    public static Command CreateCommand()
    {
        Option<string> sink = new("--sink")
        {
            Description = $"Where the output goes: {Discard} (thrown away, generation alone) or {ToFile}.",
            Required = true,
        };
        sink.AcceptOnlyFromAmong(Discard, ToFile);
        Option<string> file = ExperimentOptions.File(required: false);
        Option<long> size = ExperimentOptions.Size();
        Option<int[]> threads = ExperimentOptions.Numbers(
            "--threads", "Thread counts, separated by commas.", 1, 2, 4, 8, 16);
        Option<int[]> capacities = ExperimentOptions.Numbers(
            "--block-capacity", "Block capacities in KiB, separated by commas.", ContentFormat.BlockCapacity / 1024);
        Option<bool> buffered = new("--buffered")
        {
            Description = "Write the file through the file cache of the operating system.",
        };
        Option<int> runs = ExperimentOptions.Runs(1);
        Option<int> pause = ExperimentOptions.Pause();

        Command command = new(
            "pipeline",
            "The generation pipeline without the generator's command line: thread scaling and the block capacity.")
        {
            sink, file, size, threads, capacities, buffered, runs, pause,
        };
        command.Validators.Add(result =>
        {
            // The tokens are read as they were typed: the value of an option that failed its own check cannot be
            // asked for here.
            bool toFile = result.GetResult(sink)?.Tokens is [{ Value: ToFile }];
            if (toFile && result.GetResult(file) is null)
            {
                result.AddError($"Option '--file' is required with '--sink {ToFile}'.");
            }
        });
        command.SetAction(result =>
        {
            string? path = result.GetValue(sink) == ToFile ? Path.GetFullPath(result.GetValue(file)!) : null;
            Run(
                path,
                result.GetValue(size),
                result.GetValue(threads)!,
                result.GetValue(capacities)!,
                result.GetValue(buffered),
                result.GetValue(runs),
                TimeSpan.FromSeconds(path is null ? 0 : result.GetValue(pause)));
        });
        return command;
    }

    private static void Run(
        string? file, long size, int[] threads, int[] capacities, bool buffered, int runs, TimeSpan pause)
    {
        BlockGenerator generator = new(new ContentOptions { Seed = 1 });
        string target = file is null ? "discarded" : $"{file} ({(buffered ? "buffered" : "unbuffered")})";
        Console.WriteLine($"Output: {target}, size: {Measure.Size(size)}");
        Measure.Header("Block capacity, KiB", "Threads", "Median, MiB/s", "Min", "Max", "Runs");

        foreach (int capacity in capacities)
        {
            foreach (int threadCount in threads)
            {
                List<double> throughputs = [];
                for (int run = 0; run < runs; run++)
                {
                    TimeSpan elapsed = RunOnce(generator, size, threadCount, capacity * 1024, file, buffered);
                    throughputs.Add(Measure.ToThroughput(size, elapsed));
                    Thread.Sleep(pause);
                }

                RunStatistics result = RunStatistics.From(throughputs);
                Measure.Row(capacity, threadCount, result.Median, result.Min, result.Max, result.Runs);
            }
        }
    }

    private static TimeSpan RunOnce(
        BlockGenerator generator, long size, int threads, int blockCapacity, string? file, bool buffered)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (file is null)
        {
            GenerationPipeline.Run(
                generator, size, threads, new DiscardingSink(), CancellationToken.None, blockCapacity: blockCapacity);
            return stopwatch.Elapsed;
        }

        try
        {
            using FileSink sink = new(file, OutputSpace.Required(size, generator.MaxLineLength), unbuffered: !buffered);
            GenerationPipeline.Run(
                generator, size, threads, sink, CancellationToken.None, blockCapacity: blockCapacity);
            sink.Commit();
            return stopwatch.Elapsed;
        }
        finally
        {
            File.Delete(file);
        }
    }
}
