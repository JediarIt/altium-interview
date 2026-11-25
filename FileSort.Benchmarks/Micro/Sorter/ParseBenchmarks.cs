using BenchmarkDotNet.Attributes;

using FileSort.Engine.Lines;
using FileSort.Engine.Lines.Parsing;
using FileSort.Engine.Merging;

namespace FileSort.Benchmarks.Micro.Sorter;

/// <summary>
/// The parsing of lines of default content on one core, a million lines (2^20) per operation, so the mean is the time
/// per line. <c>Parse</c> is what a chunk does: it checks the line and makes its entry. <c>ParseValid</c> is what the
/// merge does with lines that were checked before: no check of UTF-8. <c>ParseHead</c> is all that the merge of runs
/// does with a line: the entry, and the head with more of the key.
/// </summary>
[MemoryDiagnoser]
public class ParseBenchmarks
{
    private const int Lines = 1 << 20;

    private byte[] _content = [];
    private int[] _starts = [];
    private IndexEntry[] _entries = [];

    [GlobalSetup]
    public void Setup()
    {
        ParsedContent content = ParsedContent.Create(32L * 1024 * 1024);
        _content = content.Lines;
        _starts = [.. content.Entries.Take(Lines + 1).Select(entry => (int)entry.Offset)];
        _entries = new IndexEntry[Lines];
    }

    [Benchmark(OperationsPerInvoke = Lines)]
    public int Parse()
    {
        int faults = 0;
        for (int i = 0; i < Lines; i++)
        {
            // The line without its line feed.
            ReadOnlySpan<byte> line = _content.AsSpan(_starts[i], _starts[i + 1] - _starts[i] - 1);
            faults += LineParser.Parse(line, (uint)_starts[i], out _entries[i]) == LineFault.None ? 0 : 1;
        }

        return faults;
    }

    [Benchmark(OperationsPerInvoke = Lines)]
    public ulong ParseValid()
    {
        ulong sum = 0;
        for (int i = 0; i < Lines; i++)
        {
            ReadOnlySpan<byte> line = _content.AsSpan(_starts[i], _starts[i + 1] - _starts[i] - 1);
            _entries[i] = LineParser.ParseValid(line, (uint)_starts[i]);
            sum += _entries[i].Prefix;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = Lines)]
    public ulong ParseHead()
    {
        ulong sum = 0;
        for (int i = 0; i < Lines; i++)
        {
            ReadOnlySpan<byte> line = _content.AsSpan(_starts[i], _starts[i + 1] - _starts[i] - 1);
            IndexEntry entry = LineParser.ParseValid(line, (uint)_starts[i]);
            MergeHead head = new(in entry, line);
            sum += head.Second + head.Number;
        }

        return sum;
    }
}
