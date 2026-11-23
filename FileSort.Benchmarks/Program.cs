using System.CommandLine;

using BenchmarkDotNet.Running;

using FileSort.Benchmarks.Experiments.Checker;
using FileSort.Benchmarks.Experiments.Generator;
using FileSort.Benchmarks.Micro.Generator;

// The command line is built on System.CommandLine, like the generator's: one way to declare options, to validate
// them and to print help in every program of the solution.
Command micro = new(
    "micro",
    "BenchmarkDotNet: the hot paths of the generator and of the checker and, without a disk, the generator's "
    + "pipeline and the checker's scans. Everything after the command goes to BenchmarkDotNet: `micro --filter *`, "
    + "`micro --filter *Scan*`, `micro --list flat`.")
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
