using System.Security.Cryptography;

using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Generator.Output;
using FileSort.Generator.Pipeline;

namespace FileSort.Tests.Generator.Output;

public class FileSinkTests
{
    private const int SmallChunk = 64 * 1024;

    private static string HashOfFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void AssertFileEqualsHashingSink(long size, bool unbuffered, int? chunkSize)
    {
        using TempDirectory directory = new();
        string output = directory.File("out.txt");
        BlockGenerator generator = new(TestContent.Options(edgeCases: EdgeCases.All));

        using HashingSink expected = new();
        GenerationResult expectedResult = GenerationPipeline.Run(generator, size, 4, expected, CancellationToken.None);

        using (FileSink sink = new(output, OutputSpace.Required(size, generator.MaxLineLength), unbuffered, chunkSize))
        {
            GenerationResult result = GenerationPipeline.Run(generator, size, 4, sink, CancellationToken.None);
            sink.Commit();
            Assert.Equal(expectedResult, result);
        }

        Assert.Equal(expected.Length, new FileInfo(output).Length);
        Assert.Equal(expected.GetHashHex(), HashOfFile(output));
        Assert.Equal(["out.txt"], directory.FileNames());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_file_equals_the_content_for_sizes_around_a_sector(bool unbuffered)
    {
        foreach (long size in new long[] { 1, 4095, 4096, 4097, 8191, 8192, 8193 })
        {
            AssertFileEqualsHashingSink(size, unbuffered, SmallChunk);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_file_equals_the_content_for_sizes_around_a_chunk(bool unbuffered)
    {
        foreach (long size in new long[] { SmallChunk - 1, SmallChunk, SmallChunk + 1, (3 * SmallChunk) - 40, 3 * SmallChunk })
        {
            AssertFileEqualsHashingSink(size, unbuffered, SmallChunk);
        }

        int chunk = unbuffered ? FileSink.UnbufferedChunkSize : FileSink.BufferedChunkSize;
        // One file with the real chunk size is enough: the boundaries are covered above with a small chunk, and
        // tests should write as little to the disk as they can.
        foreach (long size in new long[] { chunk + 5000 })
        {
            AssertFileEqualsHashingSink(size, unbuffered, chunkSize: null);
        }
    }

    [Fact]
    public void Chunk_sizes_are_whole_sectors()
    {
        Assert.Equal(0, FileSink.UnbufferedChunkSize % FileSink.Alignment);
        Assert.Equal(0, FileSink.BufferedChunkSize % FileSink.Alignment);
        using TempDirectory directory = new();

        Assert.Throws<ArgumentException>(() => new FileSink(directory.File("out.txt"), 0, unbuffered: true, chunkSize: 5000));
        Assert.Empty(directory.FileNames());
    }

    [Fact]
    public void Unbuffered_writes_are_used_only_where_the_system_supports_them()
    {
        using TempDirectory directory = new();

        using FileSink unbuffered = new(directory.File("a.txt"), 0, unbuffered: true);
        using FileSink buffered = new(directory.File("b.txt"), 0, unbuffered: false);

        Assert.Equal(OperatingSystem.IsWindows(), unbuffered.Unbuffered);
        Assert.False(buffered.Unbuffered);
    }

    [Fact]
    public void The_content_goes_to_a_temporary_file_until_it_is_committed()
    {
        using TempDirectory directory = new();
        string output = directory.File("out.txt");

        using FileSink sink = new(output, 0, unbuffered: true, SmallChunk);
        sink.Write(new byte[3 * SmallChunk]);

        Assert.Equal(["out.txt.tmp"], directory.FileNames());
        Assert.Equal(output + ".tmp", FileSink.TemporaryPath(output));

        sink.Commit();

        Assert.Equal(["out.txt"], directory.FileNames());
        Assert.Equal(3 * SmallChunk, new FileInfo(output).Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_aborted_write_leaves_neither_file(bool unbuffered)
    {
        using TempDirectory directory = new();

        using (FileSink sink = new(directory.File("out.txt"), 1024 * 1024, unbuffered, SmallChunk))
        {
            sink.Write(new byte[(2 * SmallChunk) + 100]);
        }

        Assert.Empty(directory.FileNames());
    }

    [Fact]
    public void A_stale_temporary_file_is_replaced()
    {
        using TempDirectory directory = new();
        string output = directory.File("out.txt");
        File.WriteAllBytes(FileSink.TemporaryPath(output), new byte[100_000]);

        using (FileSink sink = new(output, 0, unbuffered: true, SmallChunk))
        {
            sink.Write("1. Apple\n"u8);
            sink.Commit();
        }

        Assert.Equal(["out.txt"], directory.FileNames());
        Assert.Equal("1. Apple\n"u8.ToArray(), File.ReadAllBytes(output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Checking_that_a_path_can_be_written_leaves_no_file(bool unbuffered)
    {
        using TempDirectory directory = new();
        string output = directory.File("out.txt");
        File.WriteAllText(FileSink.TemporaryPath(output), "stale");

        FileSink.EnsureWritable(output, unbuffered);

        Assert.Empty(directory.FileNames());
    }

    [Fact]
    public void Missing_directories_are_created()
    {
        using TempDirectory directory = new();
        string output = directory.File(Path.Combine("a", "b", "out.txt"));

        using (FileSink sink = new(output, 0, unbuffered: false))
        {
            sink.Write("1. Apple\n"u8);
            sink.Commit();
        }

        Assert.Equal("1. Apple\n"u8.ToArray(), File.ReadAllBytes(output));
    }

    [Fact]
    public void A_size_above_the_free_space_fails_before_any_content_is_written()
    {
        using TempDirectory directory = new();
        string output = directory.File("out.txt");
        long tooLarge = OutputSpace.Available(output)!.Value + (1L << 30);

        Assert.ThrowsAny<IOException>(() => new FileSink(output, tooLarge, unbuffered: true));

        Assert.Empty(directory.FileNames());
    }

    [Fact]
    public void A_commit_does_not_replace_a_file_that_appeared_at_the_output_path()
    {
        using TempDirectory directory = new();
        string output = directory.File("out.txt");

        using (FileSink sink = new(output, 0, unbuffered: false))
        {
            sink.Write("1. Apple\n"u8);
            File.WriteAllText(output, "someone else's file");

            Assert.ThrowsAny<IOException>(sink.Commit);
        }

        Assert.Equal(["out.txt"], directory.FileNames());
        Assert.Equal("someone else's file", File.ReadAllText(output));
    }

    // The free-space decision: the space a run can need, and whether it fits once the replaced files count as free.

    [Theory]
    [InlineData(1, 30, 4096)]
    [InlineData(4096 - 30, 30, 4096)]
    [InlineData(4096 - 29, 30, 8192)]
    [InlineData(1L << 30, 100, (1L << 30) + 4096)]
    public void The_required_space_is_the_size_plus_one_line_padded_to_whole_sectors(long size, int maxLine, long expected)
    {
        Assert.Equal(expected, OutputSpace.Required(size, maxLine));
    }

    [Fact]
    public void The_required_space_is_capped_instead_of_overflowing()
    {
        Assert.Equal(long.MaxValue, OutputSpace.Required(long.MaxValue, 30));
        Assert.Equal(long.MaxValue, OutputSpace.Required(long.MaxValue - 4096, 30));
        Assert.True(OutputSpace.Required(long.MaxValue / 2, 30) > long.MaxValue / 2);
    }

    [Fact]
    public void The_files_that_are_replaced_count_as_available()
    {
        // It fits only because the old files count as available.
        Assert.False(OutputSpace.Fits(required: 1000, free: 400, replaced: 0));
        Assert.True(OutputSpace.Fits(required: 1000, free: 400, replaced: 600));

        // It does not fit even then.
        Assert.False(OutputSpace.Fits(required: 1000, free: 400, replaced: 599));
        Assert.False(OutputSpace.Fits(required: long.MaxValue, free: 400, replaced: 600));
    }

    [Fact]
    public void The_volume_of_an_output_is_found_through_the_nearest_directory_that_exists()
    {
        using TempDirectory directory = new();
        Directory.CreateDirectory(directory.File("a"));

        // Outside Windows the free space is asked of this directory: the output's own directories may not exist yet.
        Assert.Equal(directory.Path, OutputSpace.NearestExistingDirectory(directory.File("out.txt")));
        Assert.Equal(directory.File("a"), OutputSpace.NearestExistingDirectory(directory.File(Path.Combine("a", "out.txt"))));
        Assert.Equal(directory.File("a"), OutputSpace.NearestExistingDirectory(directory.File(Path.Combine("a", "b", "c", "out.txt"))));
        Assert.True(string.IsNullOrEmpty(OutputSpace.NearestExistingDirectory("out.txt")));
    }

    [Fact]
    public void A_volume_that_cannot_be_queried_gives_no_answer()
    {
        // A network path whose server does not exist: the check is skipped and preallocation decides.
        if (OperatingSystem.IsWindows())
        {
            Assert.Null(OutputSpace.Available(@"\\filesort-no-such-server\share\out.txt"));
        }
    }

    [Fact]
    public void The_free_space_of_a_local_volume_can_be_queried()
    {
        using TempDirectory directory = new();

        Assert.True(OutputSpace.Available(directory.File("out.txt")) > 0);
        Assert.Equal(0, OutputSpace.LengthOrZero(directory.File("missing.txt")));
    }
}
