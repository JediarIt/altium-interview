using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

using FileSort.Benchmarks.Micro.Sorter.Candidates;
using FileSort.Engine.Chunks.Sorting;
using FileSort.Engine.Lines;

namespace FileSort.Benchmarks.Micro.Sorter;

/// <summary>
/// The sort of the index of a chunk of default content (64 MiB of lines) on 8 threads, as in
/// <see cref="ChunkSortBenchmarks"/>, with the engine's entry of 16 bytes and with the entry of 24 bytes that also
/// holds the value of the Number (D6 of the design), both by the routine the engine uses. On one segment the smaller
/// entries may gain only from fitting into the processor's last cache, which eight segments sorted at once do not.
/// </summary>
[MemoryDiagnoser]
public class ChunkEntrySizeBenchmarks
{
    private const long ChunkLines = 64L * 1024 * 1024;
    private const int SegmentEntries = 1 << 20;
    private const int Threads = 8;

    private ParsedContent _content = null!;
    private IndexEntry[] _compact = [];
    private WideEntry[] _wideSource = [];
    private WideEntry[] _wide = [];

    [GlobalSetup]
    public void Setup()
    {
        _content = ParsedContent.Create(ChunkLines);
        _compact = new IndexEntry[_content.Entries.Length];
        _wideSource = [.. _content.Entries.Select(entry => WideEntry.From(entry, _content.Lines))];
        _wide = new WideEntry[_wideSource.Length];
    }

    [IterationSetup]
    public void Unsort()
    {
        _content.Entries.CopyTo(_compact, 0);
        _wideSource.CopyTo(_wide, 0);
    }

    [IterationCleanup(Target = nameof(Entry16Bytes))]
    public void CheckCompact()
    {
        EnsureSegmentsSorted<IndexEntry, EntryComparer>(_compact, new EntryComparer(_content.Lines));
    }

    [IterationCleanup(Target = nameof(Entry24Bytes))]
    public void CheckWide()
    {
        EnsureSegmentsSorted<WideEntry, WideEntryComparer>(_wide, new WideEntryComparer(_content.Lines));
    }

    [Benchmark(Baseline = true)]
    public void Entry16Bytes()
    {
        byte[] lines = _content.Lines;
        InSegments(_compact.Length, (start, length) =>
        {
            ComparableEntry.UseLines(lines);
            MemoryMarshal.Cast<IndexEntry, ComparableEntry>(_compact.AsSpan(start, length)).Sort();
        });
    }

    [Benchmark]
    public void Entry24Bytes()
    {
        byte[] lines = _content.Lines;
        InSegments(_wide.Length, (start, length) =>
        {
            ComparableWideEntry.UseLines(lines);
            MemoryMarshal.Cast<WideEntry, ComparableWideEntry>(_wide.AsSpan(start, length)).Sort();
        });
    }

    // One range per thread, sorted in segments, as the engine sorts a chunk.
    private static void InSegments(int length, Action<int, int> sort)
    {
        Parallel.For(0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, range =>
        {
            int start = (int)((long)length * range / Threads);
            int end = (int)((long)length * (range + 1) / Threads);
            for (int segment = start; segment < end; segment += SegmentEntries)
            {
                sort(segment, Math.Min(SegmentEntries, end - segment));
            }
        });
    }

    private static void EnsureSegmentsSorted<T, TComparer>(T[] entries, TComparer comparer)
        where TComparer : IComparer<T>
    {
        InSegments(
            entries.Length,
            (start, length) => SortRoutine.EnsureSorted<T, TComparer>(entries.AsSpan(start, length), comparer));
    }
}
