using System.CommandLine;

using BenchmarkDotNet.Running;

using FileSort.Benchmarks.Experiments;
using FileSort.Benchmarks.Micro;

// The command line is built on System.CommandLine, like the generator's: one way to declare options, to validate
// them and to print help in every program of the solution.
Command micro = new(
    "micro",
    "BenchmarkDotNet: the hot path of the generator and, without a disk, the whole pipeline. Everything after the "
    + "command goes to BenchmarkDotNet: `micro --filter *`, `micro --filter *Pipeline*`, `micro --list flat`.")
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
};

try
{
    return root.Parse(args).Invoke(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
}
catch (ArgumentException e)
{
    // An experiment rejects a combination that the options allow one by one, such as a size too small for the
    // chunks and the writers.
    Console.Error.WriteLine(e.Message);
    return 2;
}
