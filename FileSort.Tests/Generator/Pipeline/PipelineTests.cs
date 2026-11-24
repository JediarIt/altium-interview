using System.Diagnostics;

using FileSort.Common;
using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Generator.Pipeline;

namespace FileSort.Tests.Generator.Pipeline;

/// <summary>The pipeline, and the scenarios of "Output size" and "Deterministic output" that need no disk.</summary>
public class PipelineTests
{
    private const long Megabyte = TestContent.Megabyte;

    private static (string Hash, GenerationResult Result) Hash(ContentOptions options, long size, int threads)
    {
        using HashingSink sink = new();
        GenerationResult result = GenerationPipeline.Run(
            new BlockGenerator(options), size, threads, sink, CancellationToken.None);
        Assert.Equal(result.Bytes, sink.Length);
        return (sink.GetHashHex(), result);
    }

    // Requirement: Output size

    [Theory]
    [InlineData(1)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(Megabyte - 1)]
    [InlineData(Megabyte)]
    [InlineData(Megabyte + 1)]
    [InlineData((8 * Megabyte) - 1)]
    [InlineData(8 * Megabyte)]
    [InlineData((8 * Megabyte) + 1)]
    [InlineData(20 * Megabyte)]
    public void The_output_reaches_the_size_and_ends_with_its_last_line(long size)
    {
        foreach (LineEnding lineEnding in new[] { LineEnding.Lf, LineEnding.CrLf })
        {
            ContentOptions options = TestContent.Options(lineEnding: lineEnding);
            MemorySink sink = new();

            GenerationResult result = GenerationPipeline.Run(
                new BlockGenerator(options), size, 4, sink, CancellationToken.None);
            byte[] content = sink.ToArray();
            ReadOnlySpan<byte> terminator = lineEnding.Terminator();

            Assert.Equal(content.Length, result.Bytes);
            Assert.True(content.Length >= size);
            Assert.True(content.AsSpan().EndsWith(terminator));
            Assert.Equal(content.AsSpan().Count((byte)'\n'), result.Lines);

            // Without its last line the output would be smaller than the requested size.
            int lastLineStart = content.AsSpan(0, content.Length - terminator.Length).LastIndexOf((byte)'\n') + 1;
            Assert.True(lastLineStart < size);
        }
    }

    [Fact]
    public void Smallest_size()
    {
        byte[] content = TestContent.Generate(TestContent.Options(), 1, threads: 16);

        Assert.Single(TestContent.Parse(content, LineEnding.Lf));
    }

    [Fact]
    public void A_size_that_ends_exactly_on_a_line_boundary_adds_no_line()
    {
        ContentOptions options = TestContent.Options();
        byte[] content = TestContent.Generate(options, 3 * Megabyte);

        // Sizes equal to the end of a line, in the first block and across a block boundary.
        foreach (int lineEnd in new[] { content.AsSpan().IndexOf((byte)'\n') + 1, content.Length })
        {
            Assert.Equal(lineEnd, TestContent.Generate(options, lineEnd).Length);
            Assert.True(TestContent.Generate(options, lineEnd + 1).Length > lineEnd);
        }
    }

    // Requirement: Deterministic output

    [Fact]
    public void Same_seed_and_options_twice()
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.All);

        Assert.Equal(Hash(options, 20 * Megabyte, 4), Hash(options, 20 * Megabyte, 4));
    }

    [Fact]
    public void Different_seeds()
    {
        Assert.NotEqual(
            Hash(TestContent.Options(seed: 1), Megabyte, 4).Hash,
            Hash(TestContent.Options(seed: 2), Megabyte, 4).Hash);
    }

    [Fact]
    public void Size_only_truncates()
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.All);

        byte[] larger = TestContent.Generate(options, 5 * Megabyte);

        foreach (long size in new[] { 1, 100_000, Megabyte, (2 * Megabyte) + 12_345 })
        {
            byte[] smaller = TestContent.Generate(options, size);

            Assert.True(smaller.Length < larger.Length);
            Assert.True(larger.AsSpan().StartsWith(smaller));
        }
    }

    [Fact]
    public void The_thread_count_does_not_change_the_bytes()
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.All);

        (string Hash, GenerationResult Result) one = Hash(options, 40 * Megabyte, 1);

        Assert.Equal(one, Hash(options, 40 * Megabyte, 2));
        Assert.Equal(one, Hash(options, 40 * Megabyte, 16));
        Assert.Equal(one, Hash(options, 40 * Megabyte, 64));
    }

    [Fact]
    public void Progress_reports_the_bytes_written_so_far()
    {
        List<long> reports = [];

        BlockGenerator generator = new(TestContent.Options());
        GenerationResult result = GenerationPipeline.Run(
            generator, 5 * Megabyte, 4, new DiscardingSink(), CancellationToken.None, reports.Add);

        Assert.Equal(reports.Order(), reports);
        Assert.Equal(result.Bytes, reports[^1]);
        Assert.True(reports.Count >= 5);
    }

    // Requirement: No partial output (the part that the pipeline owns: it stops in time)

    [Fact]
    public void Cancellation_stops_the_pipeline_within_two_seconds()
    {
        using CancellationTokenSource cancellation = new();
        BlockGenerator generator = new(TestContent.Options());
        DiscardingSink sink = new();
        Stopwatch sinceCancel = new();

        // The sink cancels after some blocks, in the middle of a run that would take hours.
        Action<long> cancelAfterSomeBlocks = written =>
        {
            if (written > 64 * Megabyte && !cancellation.IsCancellationRequested)
            {
                sinceCancel.Start();
                cancellation.Cancel();
            }
        };

        Assert.ThrowsAny<OperationCanceledException>(() => GenerationPipeline.Run(
            generator, 1L << 50, 8, sink, cancellation.Token, cancelAfterSomeBlocks));

        Assert.True(sinceCancel.Elapsed < TimeSpan.FromSeconds(2));
        Assert.True(sink.Length < 1L << 32);
    }

    [Fact]
    public void A_cancelled_token_stops_the_pipeline_before_the_first_block()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        DiscardingSink sink = new();

        Assert.ThrowsAny<OperationCanceledException>(() => GenerationPipeline.Run(
            new BlockGenerator(TestContent.Options()), Megabyte, 4, sink, cancellation.Token));
        Assert.Equal(0, sink.Length);
    }

    [Fact]
    public void A_failing_sink_stops_the_pipeline_with_its_error()
    {
        Assert.Throws<IOException>(() => GenerationPipeline.Run(
            new BlockGenerator(TestContent.Options()), 1L << 40, 8, new FailingSink(), CancellationToken.None));
    }

    /// <summary>Counts what it receives and keeps none of it: no disk, and no memory for the output.</summary>
    private sealed class DiscardingSink : IBlockSink
    {
        public long Length { get; private set; }

        public void Write(ReadOnlySpan<byte> data)
        {
            Length += data.Length;
        }
    }

    private sealed class FailingSink : IBlockSink
    {
        private int _blocks;

        public void Write(ReadOnlySpan<byte> data)
        {
            if (++_blocks == 3)
            {
                throw new IOException("The disk is gone.");
            }
        }
    }
}
