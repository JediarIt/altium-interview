using BenchmarkDotNet.Attributes;

using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Generator.Pipeline;

namespace FileSort.Benchmarks.Micro.Generator;

/// <summary>
/// Generation without a disk: 256 MiB of content through the pipeline into a sink that throws it away, for several
/// thread counts. The mean time gives the throughput (256 MiB divided by it), and the thread counts show how
/// generation scales. It uses the processor only, so it can be repeated as often as the statistics need.
/// </summary>
[MemoryDiagnoser]
public class PipelineBenchmarks
{
    private const long Size = 256L * 1024 * 1024;

    private readonly BlockGenerator _generator = new(new ContentOptions { Seed = 1 });

    [Params(1, 2, 4, 8, 16)]
    public int Threads { get; set; }

    [Benchmark]
    public long GenerateAndDiscard()
    {
        return GenerationPipeline.Run(_generator, Size, Threads, new DiscardingSink(), CancellationToken.None).Bytes;
    }
}
