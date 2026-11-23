using System.Buffers.Binary;
using System.IO.Hashing;
using System.Security.Cryptography;

using BenchmarkDotNet.Attributes;

using FileSort.Generator.Content;

namespace FileSort.Benchmarks.Micro.Checker;

/// <summary>
/// The hash of one line, which the checker computes for every line of both files: the 128-bit hash it uses, the
/// 64-bit hash of the same family, and SHA-256 of the framework, which needs no package. Each benchmark hashes a
/// million generated lines and adds the hashes up, as the checker does; the mean time is the time per line.
/// </summary>
public class LineHashBenchmarks
{
    // Far more lines than a branch predictor can memorize: the lengths of the lines are random, and a short
    // repeating series would hide what that costs.
    private const int Lines = 1_000_000;

    private const byte LineFeed = (byte)'\n';

    private byte[] _content = [];
    private readonly int[] _starts = new int[Lines + 1];

    [GlobalSetup]
    public void Setup()
    {
        // A line of default content is about 25 bytes with its terminator; 32 MiB hold well over a million.
        _content = GeneratedContent.Create(32 * 1024 * 1024, EdgeCases.None);
        int position = 0;
        for (int line = 1; line <= Lines; line++)
        {
            position += _content.AsSpan(position).IndexOf(LineFeed) + 1;
            _starts[line] = position;
        }
    }

    // A line without its terminator, as the checker hashes it.
    private ReadOnlySpan<byte> Line(int index)
    {
        return _content.AsSpan(_starts[index], _starts[index + 1] - _starts[index] - 1);
    }

    [Benchmark(OperationsPerInvoke = Lines, Baseline = true)]
    public UInt128 XxHash128Sum()
    {
        UInt128 sum = 0;
        for (int i = 0; i < Lines; i++)
        {
            sum += XxHash128.HashToUInt128(Line(i));
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = Lines)]
    public ulong XxHash3Sum()
    {
        ulong sum = 0;
        for (int i = 0; i < Lines; i++)
        {
            sum += XxHash3.HashToUInt64(Line(i));
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = Lines)]
    public UInt128 Sha256Sum()
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        UInt128 sum = 0;
        for (int i = 0; i < Lines; i++)
        {
            SHA256.HashData(Line(i), hash);

            // The first 128 bits of the hash, to be added up like the others.
            sum += BinaryPrimitives.ReadUInt128LittleEndian(hash);
        }

        return sum;
    }
}
