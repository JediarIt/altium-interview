using System.Text;

using BenchmarkDotNet.Attributes;

using FileSort.Generator.Content.Words;
using FileSort.Generator.Random;

namespace FileSort.Benchmarks.Micro;

/// <summary>
/// Two ways to make the first letter of a word upper case, as the generator does once per String: the framework's
/// <see cref="Ascii.ToUpperInPlace(Span{byte}, out int)"/> on a span of one byte, or one exclusive or with 0x20,
/// the bit that tells a lower-case ASCII letter from an upper-case one. Each operation copies a word as the
/// generator does and then changes its first letter, so the ratio shows what the change costs per String.
/// </summary>
public class CapitalLetterBenchmarks
{
    // Far more words than a branch predictor can memorize, as in the generator, where no series repeats.
    private const int Words = 1 << 20;

    // The output restarts after this many words, so it stays in the processor's cache like a block being filled.
    private const int Batch = 4096;

    private readonly int[] _indexes = new int[Words];
    private readonly byte[] _buffer = new byte[(Batch * (Dictionary.MaxWordLength + 1)) + Dictionary.SlotSize];

    [GlobalSetup]
    public void Setup()
    {
        Xoshiro256 random = Xoshiro256.Create(1, 0);
        for (int i = 0; i < Words; i++)
        {
            _indexes[i] = RandomBits.Bits(random.Next(), 0, Dictionary.IndexBits);
        }
    }

    [Benchmark(OperationsPerInvoke = Words, Baseline = true)]
    public int CaseBit()
    {
        Span<byte> buffer = _buffer;
        ReadOnlySpan<byte> slots = Dictionary.Slots;
        ReadOnlySpan<byte> lengths = Dictionary.Lengths;
        int position = 0;
        for (int start = 0; start < Words; start += Batch)
        {
            position = 0;
            foreach (int index in _indexes.AsSpan(start, Batch))
            {
                slots.Slice(index * Dictionary.SlotSize, Dictionary.SlotSize).CopyTo(buffer.Slice(position, Dictionary.SlotSize));
                buffer[position] ^= 0x20;
                position += lengths[index];
                buffer[position++] = (byte)' ';
            }
        }

        return position;
    }

    [Benchmark(OperationsPerInvoke = Words)]
    public int FrameworkAsciiToUpper()
    {
        Span<byte> buffer = _buffer;
        ReadOnlySpan<byte> slots = Dictionary.Slots;
        ReadOnlySpan<byte> lengths = Dictionary.Lengths;
        int position = 0;
        for (int start = 0; start < Words; start += Batch)
        {
            position = 0;
            foreach (int index in _indexes.AsSpan(start, Batch))
            {
                slots.Slice(index * Dictionary.SlotSize, Dictionary.SlotSize).CopyTo(buffer.Slice(position, Dictionary.SlotSize));
                Ascii.ToUpperInPlace(buffer.Slice(position, 1), out _);
                position += lengths[index];
                buffer[position++] = (byte)' ';
            }
        }

        return position;
    }
}
