using System.CommandLine;
using System.Diagnostics;
using System.Globalization;

using FileSort.Engine;
using FileSort.Engine.Runs;
using FileSort.Generator.Content;
using FileSort.Sorter.Cli.Terminal;

using Spectre.Console;

namespace FileSort.Benchmarks.Experiments.Sorter;

/// <summary>
/// What drawing the progress costs while every thread sorts. One sort in memory runs in turns with
/// the bars of the sorter drawn on the error stream of this console and with nothing drawn, which is what the sorter
/// does when that stream is redirected; the difference of the medians is the cost. The runs stay in memory and the
/// output is only counted, so the disk does not take part.
/// </summary>
internal static class ProgressCostExperiment
{
    public static Command CreateCommand()
    {
        Option<long> size = ExperimentOptions.Size(
            "--size", "The bytes of default content to sort, made in memory.", 256L << 20);
        Option<long> memory = ExperimentOptions.Size(
            "--memory",
            "The memory of the engine's buffers, small enough that the sort writes runs and merges them.",
            64L << 20);
        Option<int[]> threads = ExperimentOptions.Numbers(
            "--threads",
            "The threads of the sort; the default is the number of logical processors.",
            Environment.ProcessorCount);
        Option<int> runs = ExperimentOptions.Runs(3);

        Command command = new(
            "progress-cost",
            "The same sort in memory with the progress bars of the sorter drawn on the error stream and without "
            + "them, in turns. Run it in a console. It writes nothing to the disk.")
        {
            size, memory, threads, runs,
        };
        command.SetAction(result => Run(
            result.GetValue(size), result.GetValue(memory), result.GetValue(threads)![0], result.GetValue(runs)));
        return command;
    }

    private static void Run(long size, long memory, int threads, int runs)
    {
        if (Console.IsErrorRedirected)
        {
            throw new ArgumentException(
                "The bars are drawn on the error stream: run the experiment with it on a console.");
        }

        byte[] input = GeneratedContent.Create(size, EdgeCases.None);
        List<double> plain = [];
        List<double> drawn = [];
        int plainCollections = 0;
        int drawnCollections = 0;
        for (int run = 0; run < runs; run++)
        {
            plain.Add(SortOnce(input, memory, threads, new NoProgressView(), ref plainCollections));
            drawn.Add(SortOnce(input, memory, threads, new BarsProgressView(AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(Console.Error),
            })), ref drawnCollections));
        }

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{Measure.Size(input.Length)} of default content, {Measure.Size(memory)} for the buffers, "
            + $"{threads} threads, {runs} runs of each, in turns."));
        Measure.Header("Progress", "Median, s", "Min, s", "Max, s", "Runs", "Collections of gen 0 per sort");
        RunStatistics without = RunStatistics.From(plain);
        RunStatistics with = RunStatistics.From(drawn);
        double plainPerSort = plainCollections / (double)runs;
        double drawnPerSort = drawnCollections / (double)runs;
        Measure.Row("not drawn", F2(without.Median), F2(without.Min), F2(without.Max), without.Runs, plainPerSort);
        Measure.Row("drawn", F2(with.Median), F2(with.Min), F2(with.Max), with.Runs, drawnPerSort);
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture, $"The bars cost {(with.Median / without.Median) - 1:P1} of the median."));
    }

    // The collections of generation 0 during the sort are added to collections: drawing allocates, and every
    // collection stops the threads of the sort for a moment.
    private static double SortOnce(byte[] input, long memory, int threads, IProgressView view, ref int collections)
    {
        int collectionsBefore = GC.CollectionCount(0);
        SortSettings settings = new()
        {
            InputPath = "memory",
            OutputPath = "memory",
            MemoryBytes = memory,
            Threads = threads,
            FreeSpace = _ => null,
        };
        SortProgress progress = new();
        MemoryRunStorage storage = new();
        SpaceCheck space = new(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "memory"), settings.FreeSpace);

        Stopwatch stopwatch = Stopwatch.StartNew();
        Task<SortResult> sort = Task.Factory.StartNew(
            () => new SortJob(settings, progress).Sort(new MemoryInput(input), storage, space, CancellationToken.None),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        view.Follow(progress, sort);
        SortResult result = sort.GetAwaiter().GetResult();
        stopwatch.Stop();
        collections += GC.CollectionCount(0) - collectionsBefore;

        if (storage.OutputBytes != input.Length || result.Runs < 2)
        {
            throw new InvalidOperationException(
                "The output is not the size of the input, or the sort made no runs to merge.");
        }

        return stopwatch.Elapsed.TotalSeconds;
    }

    private static string F2(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
}
