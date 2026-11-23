using BenchmarkDotNet.Attributes;

using FileSort.Benchmarks.Sorting;
using FileSort.Checker.Checks;
using FileSort.Checker.Checks.Chunks;
using FileSort.Checker.Lines;
using FileSort.Generator.Content;

namespace FileSort.Benchmarks.Micro.Checker;

/// <summary>
/// What one thread of the checker does, without a disk: 64 MiB of sorted generated lines are read from memory as
/// chunks, and every chunk is checked on the calling thread. The check of the input reads, parses and hashes every
/// line; the check of the output also compares every line with the one before it. The mean time gives the
/// throughput of one thread (64 MiB divided by it). The allocated bytes are a few objects per chunk: they must not
/// grow with the number of lines.
/// </summary>
[MemoryDiagnoser]
public class ScanBenchmarks
{
    private const long Size = 64L * 1024 * 1024;

    private byte[] _sorted = [];

    [Params("Default", "AllEdgeCases")]
    public string Content { get; set; } = "Default";

    [GlobalSetup]
    public void Setup()
    {
        EdgeCases edgeCases = Content == "AllEdgeCases" ? EdgeCases.All : EdgeCases.None;
        _sorted = InMemorySort.Sort(GeneratedContent.Create(Size, edgeCases), LineEnding.Lf);
    }

    [Benchmark]
    public long InputChunks()
    {
        return Check(order: false);
    }

    [Benchmark]
    public long OutputChunks()
    {
        return Check(order: true);
    }

    // The number of lines, so that the work cannot be optimized away.
    private long Check(bool order)
    {
        ScanSettings settings = ScanSettings.Default;
        using MemoryStream stream = new(_sorted, writable: false);
        ChunkReader reader = new(stream, LineEnding.Lf, settings.ChunkSize, settings.MaxLineLength, keepPreviousLine: order);
        long lines = 0;
        while (reader.Read() is Chunk chunk)
        {
            lines += new ChunkScan(chunk, LineEnding.Lf, settings.MaxLineLength, order).Run().Digest.Lines;
            ChunkReader.Release(chunk);
        }

        return lines;
    }
}
