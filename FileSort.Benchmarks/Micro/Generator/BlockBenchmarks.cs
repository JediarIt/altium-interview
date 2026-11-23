using BenchmarkDotNet.Attributes;

using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;

namespace FileSort.Benchmarks.Micro.Generator;

/// <summary>
/// Generation of one block of 1 MiB on one core: the mean time is the time per mebibyte, and the allocated bytes
/// must be zero. <c>Words1</c> is the shortest lines (the Number dominates), <c>AllEdgeCases</c> takes the rare paths.
/// </summary>
[MemoryDiagnoser]
public class BlockBenchmarks
{
    private readonly byte[] _buffer = new byte[ContentFormat.BlockCapacity];
    private BlockGenerator _generator = null!;
    private long _block;

    [Params("Default", "Words1", "AllEdgeCases")]
    public string Content { get; set; } = "Default";

    [GlobalSetup]
    public void Setup()
    {
        ContentOptions options = new() { Seed = 1 };
        _generator = new BlockGenerator(Content switch
        {
            "Words1" => options with { Words = new WordRange(1, 1) },
            "AllEdgeCases" => options with { EdgeCases = EdgeCases.All },
            _ => options,
        });
    }

    [Benchmark]
    public int GenerateBlock()
    {
        return _generator.Generate(_block++ & 1023, _buffer).Bytes;
    }
}
