using System.Security.Cryptography;

using FileSort.Cli;
using FileSort.Common;
using FileSort.Generator.Cli;
using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Generator.Pipeline;
using FileSort.Tests.Cli;

namespace FileSort.Tests.Generator.Content;

/// <summary>
/// The guard of "Deterministic output": the SHA-256 of two reference files, recorded per content version. A change
/// that alters the bytes for an existing seed fails these tests. The fix is to raise
/// <see cref="ContentFormat.Version"/> and to add the hashes of the new version; a hash recorded for an existing
/// version is never edited.
/// </summary>
public sealed class ReferenceFileTests : IDisposable
{
    // Seed 1, size 1MB, default options. Version 2 added edge cases only, so plain content kept its bytes.
    private static readonly Dictionary<int, string> DefaultOptionsHash = new()
    {
        [1] = "1CF4ADB5E34ABC7CEFF7069BE44265B4ACE2917F5B7C16E802F7D6534287C692",
        [2] = "1CF4ADB5E34ABC7CEFF7069BE44265B4ACE2917F5B7C16E802F7D6534287C692",
    };

    // Seed 1, size 1MB, every content option changed:
    // --duplicates 50 --words 2-7 --edge-cases all --line-ending crlf.
    private static readonly Dictionary<int, string> ChangedOptionsHash = new()
    {
        [1] = "4BC5790DC1BA84D706EA14517FB07136F93A45DFB780CBA5717EC73D5814D4D2",
        [2] = "A358C2ECE658628D04066684A1D7EDC6DD1274DA4F753A57F0128EA42C4FEB73",
    };

    private readonly TempDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static string Hash(ContentOptions options)
    {
        using HashingSink sink = new();
        GenerationPipeline.Run(new BlockGenerator(options), 1024 * 1024, 4, sink, CancellationToken.None);
        return sink.GetHashHex();
    }

    private string HashOfGeneratedFile(string name, params string[] options)
    {
        string output = _directory.File(name);
        FakeConsole console = new();
        int exitCode = GeneratorApp.Run(["--output", output, .. options], console, CancellationToken.None);
        Assert.True(exitCode == ExitCode.Success, console.ErrorText);

        using FileStream stream = File.OpenRead(output);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    [Fact]
    public void Pinned_reference_file_with_default_options()
    {
        Assert.Equal(DefaultOptionsHash[ContentFormat.Version], Hash(new ContentOptions { Seed = 1 }));
    }

    [Fact]
    public void Pinned_reference_file_with_every_content_option_changed()
    {
        ContentOptions options = new()
        {
            Seed = 1,
            DuplicatesPercent = 50,
            Words = new WordRange(2, 7),
            EdgeCases = EdgeCases.All,
            LineEnding = LineEnding.CrLf,
        };

        Assert.Equal(ChangedOptionsHash[ContentFormat.Version], Hash(options));
    }

    [Fact]
    public void The_command_line_writes_the_pinned_reference_files()
    {
        Assert.Equal(
            DefaultOptionsHash[ContentFormat.Version],
            HashOfGeneratedFile("default.txt", "--size", "1MB", "--seed", "1"));
        Assert.Equal(
            ChangedOptionsHash[ContentFormat.Version],
            HashOfGeneratedFile(
                "changed.txt", "--size", "1MB", "--seed", "1", "--duplicates", "50", "--words", "2-7",
                "--edge-cases", "all", "--line-ending", "crlf"));
    }

    [Fact]
    public void Different_thread_counts_and_write_modes()
    {
        // Small on purpose: the thread counts are compared on 40 MB in memory by the pipeline tests; here only the
        // path through real files and both write modes is checked.
        string[] content = ["--size", "3MB", "--seed", "1", "--edge-cases", "all"];

        string oneThread = HashOfGeneratedFile("one.txt", [.. content, "--threads", "1"]);
        string sixteenThreads = HashOfGeneratedFile("sixteen.txt", [.. content, "--threads", "16"]);
        string buffered = HashOfGeneratedFile("buffered.txt", [.. content, "--buffered-io"]);

        Assert.Equal(oneThread, sixteenThreads);
        Assert.Equal(oneThread, buffered);
    }

    [Fact]
    public void The_block_capacity_is_part_of_the_content()
    {
        // The pinned hashes hold for the frozen capacity only: a different capacity gives different bytes.
        BlockGenerator generator = new(new ContentOptions { Seed = 1 });
        using HashingSink frozen = new();
        using HashingSink other = new();

        GenerationPipeline.Run(generator, 4 * 1024 * 1024, 4, frozen, CancellationToken.None);
        int halfCapacity = ContentFormat.BlockCapacity / 2;
        GenerationPipeline.Run(
            generator, 4 * 1024 * 1024, 4, other, CancellationToken.None, blockCapacity: halfCapacity);

        Assert.Equal(1024 * 1024, ContentFormat.BlockCapacity);
        Assert.NotEqual(frozen.GetHashHex(), other.GetHashHex());
    }
}
