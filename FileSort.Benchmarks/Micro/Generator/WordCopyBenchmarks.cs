using BenchmarkDotNet.Attributes;

using FileSort.Generator.Content.Words;
using FileSort.Generator.Random;

namespace FileSort.Benchmarks.Micro.Generator;

/// <summary>
/// Two ways to copy a dictionary word into the output: by its exact length, which is known only at run time, or as
/// a whole 16-byte slot whose tail the next word overwrites.
/// </summary>
public class WordCopyBenchmarks
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
            _indexes[i] = (int)random.Next() & (Dictionary.WordCount - 1);
        }
    }

    [Benchmark(OperationsPerInvoke = Words, Baseline = true)]
    public int FixedSlot()
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
                ReadOnlySpan<byte> slot = slots.Slice(index * Dictionary.SlotSize, Dictionary.SlotSize);
                slot.CopyTo(buffer.Slice(position, Dictionary.SlotSize));
                position += lengths[index];
                buffer[position++] = (byte)' ';
            }
        }

        return position;
    }

    [Benchmark(OperationsPerInvoke = Words)]
    public int ExactLength()
    {
        Span<byte> buffer = _buffer;
        int position = 0;
        for (int start = 0; start < Words; start += Batch)
        {
            position = 0;
            foreach (int index in _indexes.AsSpan(start, Batch))
            {
                ReadOnlySpan<byte> word = Dictionary.Word(index);
                word.CopyTo(buffer[position..]);
                position += word.Length;
                buffer[position++] = (byte)' ';
            }
        }

        return position;
    }
}
