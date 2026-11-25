using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;

using FileSort.Engine;
using FileSort.Engine.Runs;
using FileSort.Generator.Content;

namespace FileSort.Benchmarks.Experiments.Sorter;

/// <summary>
/// The whole sort in memory, its two phases timed apart, for several thread counts. The input is
/// default content made by the generator in memory; the runs stay in memory and the output is only counted, so the
/// disk does not take part.
/// </summary>
internal static class SortPhasesExperiment
{
    public static Command CreateCommand()
    {
        Option<long> size = ExperimentOptions.Size(
            "--size", "The bytes of default content to sort, made in memory: 128MB, 1GB.", 128L << 20);
        Option<long> memory = ExperimentOptions.Size(
            "--memory", "The memory of the engine's buffers: the --memory of the sorter less its reserve.", 64L << 20);
        Option<int[]> threads = ExperimentOptions.Numbers(
            "--threads", "Thread counts, separated by commas.", 1, 2, 4, 8);
        Option<int> runs = ExperimentOptions.Runs(3);

        Command command = new(
            "sort-phases",
            "The whole sort in memory, the chunks and the merge timed apart, for several thread counts. It writes "
            + "nothing to the disk.")
        {
            size, memory, threads, runs,
        };
        command.SetAction(result => Run(
            result.GetValue(size), result.GetValue(memory), result.GetValue(threads)!, result.GetValue(runs)));
        return command;
    }

    private static void Run(long size, long memory, int[] threads, int runs)
    {
        byte[] input = GeneratedContent.Create(size, EdgeCases.None);
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Measure.Size(input.Length)} of default content, {Measure.Size(memory)} for the buffers, "
            + $"{runs} runs per row."));
        Console.WriteLine($"Garbage collector: {CollectorMode()}");
        Measure.Header(
            "Threads",
            "Runs written",
            "Chunks, s",
            "Merge, s",
            "Whole, s",
            "Min",
            "Max",
            "Whole, MiB/s",
            "Runs",
            "Gen 0",
            "Gen 1",
            "Gen 2",
            "GC pause, ms",
            "Peak working set, MiB");

        foreach (int threadCount in threads)
        {
            GcActivity before = GcActivity.Now();
            List<SortResult> results = [];
            for (int run = 0; run < runs; run++)
            {
                results.Add(SortOnce(input, memory, threadCount));
            }

            GcActivity collector = GcActivity.Now().Since(before);
            RunStatistics chunks = RunStatistics.From([.. results.Select(result => result.ChunksTime.TotalSeconds)]);
            RunStatistics merge = RunStatistics.From([.. results.Select(result => result.MergeTime.TotalSeconds)]);
            RunStatistics whole = RunStatistics.From([.. results.Select(WholeSeconds)]);
            double throughput = input.Length / (double)Measure.Mebibyte / whole.Median;
            Measure.Row(
                threadCount,
                results[0].Runs,
                F2(chunks.Median),
                F2(merge.Median),
                F2(whole.Median),
                F2(whole.Min),
                F2(whole.Max),
                throughput,
                whole.Runs,
                collector.Gen0,
                collector.Gen1,
                collector.Gen2,
                collector.Pause.TotalMilliseconds,
                PeakWorkingSetMebibytes());
        }
    }

    /// <summary>
    /// The mode of the collector this process runs with, as the runtime reports it, so that a run under
    /// <c>DOTNET_gcServer</c> or <c>DOTNET_gcConcurrent</c> shows that the setting took effect.
    /// </summary>
    private static string CollectorMode()
    {
        IEnumerable<string> settings = GC.GetConfigurationVariables()
            .Where(setting => IsModeSetting(setting.Key))
            .OrderBy(setting => setting.Key, StringComparer.Ordinal)
            .Select(setting => string.Create(CultureInfo.InvariantCulture, $"{setting.Key}={setting.Value}"));
        string kind = GCSettings.IsServerGC ? "server" : "workstation";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{kind}, latency mode {GCSettings.LatencyMode} ({string.Join(", ", settings)})");
    }

    private static bool IsModeSetting(string name)
    {
        return name.Contains("Server", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Concurrent", StringComparison.OrdinalIgnoreCase)
            || name.Contains("HeapCount", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Adaptation", StringComparison.OrdinalIgnoreCase);
    }

    private static double PeakWorkingSetMebibytes()
    {
        using Process process = Process.GetCurrentProcess();
        return process.PeakWorkingSet64 / (double)Measure.Mebibyte;
    }

    private static double WholeSeconds(SortResult result)
    {
        return (result.ChunksTime + result.MergeTime).TotalSeconds;
    }

    private static SortResult SortOnce(byte[] input, long memory, int threads)
    {
        SortSettings settings = new()
        {
            InputPath = "memory",
            OutputPath = "memory",
            MemoryBytes = memory,
            Threads = threads,
            FreeSpace = _ => null,
        };
        MemoryRunStorage storage = new();
        SpaceCheck space = new(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "memory"), settings.FreeSpace);
        SortJob job = new(settings, new SortProgress());
        SortResult result = job.Sort(new MemoryInput(input), storage, space, CancellationToken.None);
        if (storage.OutputBytes != input.Length)
        {
            throw new InvalidOperationException("The output is not the size of the input.");
        }

        return result;
    }

    private static string F2(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
}
