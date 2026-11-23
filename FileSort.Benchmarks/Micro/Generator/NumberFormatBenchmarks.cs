using System.Buffers.Text;

using BenchmarkDotNet.Attributes;

using FileSort.Common;
using FileSort.Generator.Content.Lines;
using FileSort.Generator.Random;

namespace FileSort.Benchmarks.Micro.Generator;

/// <summary>
/// Two ways to write a Number as decimal digits: the generator's loop-free writer and the framework's
/// <see cref="Utf8Formatter"/>, which the generator uses for the rare Numbers that the first cannot write. The
/// values have the generator's distribution: every bit length from 1 to 32 is equally likely, so the number of
/// digits is unpredictable.
/// </summary>
public class NumberFormatBenchmarks
{
    // Far more values than a branch predictor can memorize: a short repeating series would hide the cost of the
    // unpredictable digit count, which the generator pays on every line.
    private const int Values = 1 << 20;

    // The output restarts after this many values, so it stays in the processor's cache like a block being filled.
    private const int Batch = 4096;

    private readonly uint[] _numbers = new uint[Values];
    private readonly byte[] _buffer = new byte[(Batch * LineFormat.MaxNumberDigits) + PlainNumberWriter.RequiredLength];

    [GlobalSetup]
    public void Setup()
    {
        Xoshiro256 random = Xoshiro256.Create(1, 0);
        for (int i = 0; i < Values; i++)
        {
            // The way the generator draws a plain Number: a bit length from 1 to 32, then that many of the highest bits.
            ulong value = random.Next();
            _numbers[i] = (uint)RandomBits.Highest(value, RandomBits.Bits(value, 0, 5) + 1);
        }
    }

    [Benchmark(OperationsPerInvoke = Values, Baseline = true)]
    public int LoopFree()
    {
        Span<byte> buffer = _buffer;
        int position = 0;
        for (int start = 0; start < Values; start += Batch)
        {
            position = 0;
            foreach (uint number in _numbers.AsSpan(start, Batch))
            {
                position += PlainNumberWriter.Write(buffer[position..], number);
            }
        }

        return position;
    }

    [Benchmark(OperationsPerInvoke = Values)]
    public int FrameworkUtf8Formatter()
    {
        Span<byte> buffer = _buffer;
        int position = 0;
        for (int start = 0; start < Values; start += Batch)
        {
            position = 0;
            foreach (uint number in _numbers.AsSpan(start, Batch))
            {
                Utf8Formatter.TryFormat(number, buffer[position..], out int written);
                position += written;
            }
        }

        return position;
    }
}
