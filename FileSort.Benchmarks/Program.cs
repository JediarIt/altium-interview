using System.CommandLine;

using BenchmarkDotNet.Running;

using FileSort.Benchmarks.Experiments.Checker;
using FileSort.Benchmarks.Experiments.Generator;
using FileSort.Benchmarks.Experiments.Sorter;
using FileSort.Benchmarks.Micro.Generator;

// The command line is built on System.CommandLine, like the generator's: one way to declare options, to validate
// them and to print help in every program of the solution.
Command micro = new(
    "micro",
    "BenchmarkDotNet: the hot paths of the generator, the checker and the sorter and, without a disk, the generator's "
    + "pipeline, the checker's scans and the sorter's sort of a chunk. Everything after the command goes to "
    + "BenchmarkDotNet: `micro --filter *`, `micro --filter *Scan*`, `micro --list flat`.")
{
    TreatUnmatchedTokensAsErrors = false,
};
micro.SetAction(result =>
{
    BenchmarkSwitcher.FromAssembly(typeof(BlockBenchmarks).Assembly).Run([.. result.UnmatchedTokens]);
});

RootCommand root = new(
    "Benchmarks and experiments behind the numbers in the READMEs. The experiments that write files wear the disk: "
    + "keep sizes small and mind the total of a series, which is the size times the combinations times the runs.")
{
    micro,
    WritePathExperiment.CreateCommand(),
    FileSinkExperiment.CreateCommand(),
    PipelineExperiment.CreateCommand(),
    SortedCopyExperiment.CreateCommand(),
    CheckExperiment.CreateCommand(),
    BlockMergeExperiment.CreateCommand(),
    SortPhasesExperiment.CreateCommand(),
    ProgressCostExperiment.CreateCommand(),
};

try
{
    return root.Parse(args).Invoke(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
}
catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
{
    // An experiment rejects a combination that the options allow one by one, such as a size too small for the
    // chunks and the writers, or a file that it cannot read, write or use.
    Console.Error.WriteLine(e.Message);
    return 2;
}
