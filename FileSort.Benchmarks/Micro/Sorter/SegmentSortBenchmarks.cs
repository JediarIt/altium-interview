using BenchmarkDotNet.Attributes;

using FileSort.Benchmarks.Micro.Sorter.Candidates;
using FileSort.Engine.Lines;

namespace FileSort.Benchmarks.Micro.Sorter;

/// <summary>
/// The sort of one segment of index entries of default content on one core, by the routine of the engine, for several
/// sizes of a segment. The segment size is the largest whose sort takes at most half a second, so
/// that a sort with one thread checks for Ctrl+C often enough. Every operation sorts a fresh copy of the same
/// unsorted entries. The routines are compared on a whole chunk, in <see cref="ChunkSortBenchmarks"/>.
/// </summary>
[MemoryDiagnoser]
public class SegmentSortBenchmarks
{
    private ParsedContent _content = null!;
    private IndexEntry[] _segment = [];

    [Params(SortRoutine.Comparable)]
    public string Routine { get; set; } = SortRoutine.Comparable;

    [Params(1 << 19, 1 << 20, 1 << 21)]
    public int Entries { get; set; } = 1 << 20;

    [GlobalSetup]
    public void Setup()
    {
        // About 25 bytes a line: enough content for the largest segment, taken from the start of the content.
        _content = ParsedContent.Create(Entries * 26L);
        _segment = new IndexEntry[Entries];
    }

    [IterationSetup]
    public void Unsort()
    {
        _content.Entries.AsSpan(0, Entries).CopyTo(_segment);
    }

    [IterationCleanup]
    public void Check()
    {
        SortRoutine.EnsureSorted<IndexEntry, EntryComparer>(_segment, new EntryComparer(_content.Lines));
    }

    [Benchmark]
    public void SortSegment()
    {
        SortRoutine.Sort(Routine, _segment, _content.Lines);
    }
}
