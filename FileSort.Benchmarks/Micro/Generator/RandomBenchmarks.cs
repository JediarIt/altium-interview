using BenchmarkDotNet.Attributes;

using FileSort.Generator.Random;

namespace FileSort.Benchmarks.Micro.Generator;

/// <summary>
/// The generator's own PRNG against <see cref="Random"/> with a seed: the cost of one 64-bit value, and the cost of
/// a new generator, which every block pays for its own stream.
/// </summary>
public class RandomBenchmarks
{
    private const int Values = 1024;

    private readonly Random _systemRandom = new(1);
    private readonly ulong _streamSeed = 1;
    private int _seed;

    [Benchmark(OperationsPerInvoke = Values, Baseline = true)]
    public ulong Xoshiro256Next()
    {
        Xoshiro256 random = Xoshiro256.Create(_streamSeed, 0);
        ulong sum = 0;
        for (int i = 0; i < Values; i++)
        {
            sum += random.Next();
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = Values)]
    public ulong SplitMix64Next()
    {
        SplitMix64 random = new(_streamSeed);
        ulong sum = 0;
        for (int i = 0; i < Values; i++)
        {
            sum += random.Next();
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = Values)]
    public long SystemRandomNextInt64()
    {
        long sum = 0;
        for (int i = 0; i < Values; i++)
        {
            sum += _systemRandom.NextInt64();
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = Values)]
    public long SystemRandomNext()
    {
        long sum = 0;
        for (int i = 0; i < Values; i++)
        {
            sum += _systemRandom.Next();
        }

        return sum;
    }

    [Benchmark]
    public ulong NewXoshiro256()
    {
        return Xoshiro256.Create(1, (ulong)_seed++).Next();
    }

    [Benchmark]
    public long NewSystemRandom()
    {
        return new Random(_seed++).NextInt64();
    }
}
