using BenchmarkDotNet.Attributes;

using FileSort.Benchmarks.Micro.Sorter.Candidates;
using FileSort.Engine.Lines;

namespace FileSort.Benchmarks.Micro.Sorter;

/// <summary>
/// The sort of the index of a chunk of default content, as the engine sorts it: the entries are cut into one range per
/// thread, and every thread sorts its range in segments of 2^20 entries. The chunk has 64 MiB of lines and 8 threads
/// sort it, so that a run of the benchmark keeps half of the processors free; a chunk of the default memory holds
/// about 510 MiB. The rule of the design: the hand-written routine is used only if it sorts a chunk at least
/// 10% faster than the framework.
/// </summary>
[MemoryDiagnoser]
public class ChunkSortBenchmarks
{
    private const long ChunkLines = 64L * 1024 * 1024;
    private const int SegmentEntries = 1 << 20;

    private ParsedContent _content = null!;
    private IndexEntry[] _index = [];

    [Params(SortRoutine.Delegate, SortRoutine.Comparable, SortRoutine.Introsort)]
    public string Routine { get; set; } = SortRoutine.Introsort;

    [Params(8)]
    public int Threads { get; set; } = 8;

    [GlobalSetup]
    public void Setup()
    {
        _content = ParsedContent.Create(ChunkLines);
        _index = new IndexEntry[_content.Entries.Length];
    }

    [IterationSetup]
    public void Unsort()
    {
        _content.Entries.CopyTo(_index, 0);
    }

    [IterationCleanup]
    public void Check()
    {
        EntryComparer comparer = new(_content.Lines);
        InSegments((start, length) =>
            SortRoutine.EnsureSorted<IndexEntry, EntryComparer>(_index.AsSpan(start, length), comparer));
    }

    [Benchmark]
    public void SortChunk()
    {
        InSegments((start, length) => SortRoutine.Sort(Routine, _index.AsSpan(start, length), _content.Lines));
    }

    // One range per thread, sorted in segments, as the engine sorts a chunk.
    private void InSegments(Action<int, int> body)
    {
        int length = _index.Length;
        Parallel.For(0, Threads, new ParallelOptions { MaxDegreeOfParallelism = Threads }, range =>
        {
            int start = (int)((long)length * range / Threads);
            int end = (int)((long)length * (range + 1) / Threads);
            for (int segment = start; segment < end; segment += SegmentEntries)
            {
                body(segment, Math.Min(SegmentEntries, end - segment));
            }
        });
    }
}
