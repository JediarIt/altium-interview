using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

using FileSort.Benchmarks.Micro.Sorter.Candidates;
using FileSort.Engine.Chunks.Sorting;
using FileSort.Engine.Lines;

namespace FileSort.Benchmarks.Micro.Sorter;

/// <summary>
/// The sort of one segment of 2^20 entries of default content on one core, with the engine's entry of 16 bytes and
/// with the entry of 24 bytes that also holds the value of the Number. Both are sorted by the
/// routine the engine uses, the framework's sort of elements that compare themselves.
/// </summary>
[MemoryDiagnoser]
public class EntrySizeBenchmarks
{
    private const int Entries = 1 << 20;

    private ParsedContent _content = null!;
    private IndexEntry[] _compact = [];
    private WideEntry[] _wideSource = [];
    private WideEntry[] _wide = [];

    [GlobalSetup]
    public void Setup()
    {
        _content = ParsedContent.Create(Entries * 26L);
        _compact = new IndexEntry[Entries];
        _wideSource = [.. _content.Entries.Take(Entries).Select(entry => WideEntry.From(entry, _content.Lines))];
        _wide = new WideEntry[Entries];
    }

    [IterationSetup]
    public void Unsort()
    {
        _content.Entries.AsSpan(0, Entries).CopyTo(_compact);
        _wideSource.CopyTo(_wide, 0);
    }

    [IterationCleanup(Target = nameof(Entry16Bytes))]
    public void CheckCompact()
    {
        SortRoutine.EnsureSorted<IndexEntry, EntryComparer>(_compact, new EntryComparer(_content.Lines));
    }

    [IterationCleanup(Target = nameof(Entry24Bytes))]
    public void CheckWide()
    {
        SortRoutine.EnsureSorted<WideEntry, WideEntryComparer>(_wide, new WideEntryComparer(_content.Lines));
    }

    [Benchmark(Baseline = true)]
    public void Entry16Bytes()
    {
        ComparableEntry.UseLines(_content.Lines);
        MemoryMarshal.Cast<IndexEntry, ComparableEntry>(_compact.AsSpan()).Sort();
    }

    [Benchmark]
    public void Entry24Bytes()
    {
        ComparableWideEntry.UseLines(_content.Lines);
        MemoryMarshal.Cast<WideEntry, ComparableWideEntry>(_wide.AsSpan()).Sort();
    }
}
