using System.Globalization;

using FileSort.Cli;
using FileSort.Cli.Reports;
using FileSort.Common;
using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Sorter.Cli;
using FileSort.Tests.Cli;
using FileSort.Tests.Engine.InMemory;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Sorter.Cli;

/// <summary>
/// What the sorter prints and what its options do to the output, through the command line: the scenarios of
/// "Settings, progress and summary" of the <c>external-sorter</c> spec, except the drawing of the progress, and
/// "Same output for every setting".
/// </summary>
public sealed class SettingsAndSummaryTests : IDisposable
{
    private const string Input =
        "415. Apple\n30432. Something something something\n1. Apple\n32. Cherry is the best\n2. Banana is yellow\n";

    private static readonly string[] SettingsNames =
        ["Sorting", "Output", "Temporary folder", "Memory", "Threads", "Line ending", "Overwrite"];

    private static readonly string[] SummaryNames =
    [
        "Output", "Lines", "Size", "Line ending", "Byte order mark", "Runs", "Merge passes", "Chunks", "Merge",
        "Elapsed", "Throughput", "Peak working set",
    ];

    private readonly SorterRun _run = new();

    public void Dispose()
    {
        _run.Dispose();
    }

    // Requirement: Settings, progress and summary

    [Fact]
    public void Options_that_were_not_given()
    {
        File.WriteAllText(_run.InputPath, Input);

        Assert.Equal(ExitCode.Success, _run.Run("--input", _run.InputPath, "--output", _run.OutputPath));

        // The settings come first on the error stream, every option with its value, given or not.
        Assert.StartsWith("Sorting:", _run.Error, StringComparison.Ordinal);
        Assert.Equal(_run.InputPath, _run.SettingsField("Sorting"));
        Assert.Equal(_run.OutputPath, _run.SettingsField("Output"));
        Assert.Equal($"in {_run.DirectoryPath}", _run.SettingsField("Temporary folder"));
        Assert.Equal(ByteSize.Describe(1L << 30), _run.SettingsField("Memory"));
        string threads = Math.Min(Environment.ProcessorCount, 64).ToString(CultureInfo.InvariantCulture);
        Assert.Equal(threads, _run.SettingsField("Threads"));
        Assert.Equal("auto", _run.SettingsField("Line ending"));
        Assert.Equal("no", _run.SettingsField("Overwrite"));
    }

    [Fact]
    public void Options_that_were_given()
    {
        using TempDirectory temp = new();

        Assert.Equal(
            ExitCode.Success,
            _run.Sort(
                Input,
                "--temp", temp.Path,
                "--memory", "300MB",
                "--threads", "3",
                "--line-ending", "lf",
                "--overwrite"));

        Assert.Equal($"in {temp.Path}", _run.SettingsField("Temporary folder"));
        Assert.Equal(ByteSize.Describe(300L * 1024 * 1024), _run.SettingsField("Memory"));
        Assert.Equal("3", _run.SettingsField("Threads"));
        Assert.Equal("lf", _run.SettingsField("Line ending"));
        Assert.Equal("yes", _run.SettingsField("Overwrite"));

        // The engine gets them.
        Assert.Equal(temp.Path, _run.EngineSettings!.TempDirectory);
        Assert.Equal((300L * 1024 * 1024) - SorterApp.Reserve, _run.EngineSettings.MemoryBytes);
        Assert.Equal(3, _run.EngineSettings.Threads);
        Assert.Equal(LineEnding.Lf, _run.EngineSettings.LineEnding);
        Assert.Empty(temp.FileNames());
    }

    [Fact]
    public void Paths_are_printed_in_full()
    {
        File.WriteAllText(_run.InputPath, Input);
        string input = Path.GetRelativePath(Environment.CurrentDirectory, _run.InputPath);
        string output = Path.GetRelativePath(Environment.CurrentDirectory, _run.OutputPath);

        Assert.Equal(ExitCode.Success, _run.Run("--input", input, "--output", output));

        Assert.Equal(_run.InputPath, _run.SettingsField("Sorting"));
        Assert.Equal(_run.OutputPath, _run.SettingsField("Output"));
        Assert.Equal(_run.OutputPath, _run.SummaryField("Output"));
    }

    [Fact]
    public void Summary_after_a_successful_sort()
    {
        Assert.Equal(ExitCode.Success, _run.Sort(Input));

        // Standard output holds the summary and nothing else.
        Assert.Equal(SummaryNames, _run.SummaryNames());
        Assert.Equal(_run.OutputPath, _run.SummaryField("Output"));
        Assert.Equal("5", _run.SummaryField("Lines"));
        Assert.Equal(ByteSize.Describe(Input.Length), _run.SummaryField("Size"));
        Assert.Equal("lf", _run.SummaryField("Line ending"));
        Assert.Equal("no", _run.SummaryField("Byte order mark"));

        // The input fitted in one chunk, which was written as the output.
        Assert.Equal("0", _run.SummaryField("Runs"));
        Assert.Equal("0", _run.SummaryField("Merge passes"));
        Assert.Matches(@"^\d+\.\d\d s$", _run.SummaryField("Chunks"));
        Assert.Matches(@"^\d+\.\d\d s$", _run.SummaryField("Merge"));
        Assert.Matches(@"^\d+\.\d\d s$", _run.SummaryField("Elapsed"));
        Assert.Matches(@"^\d+ MiB/s$", _run.SummaryField("Throughput"));
        Assert.Matches(@"^[1-9]\d* MiB$", _run.SummaryField("Peak working set"));
    }

    [Fact]
    public void The_summary_names_the_line_ending_and_the_byte_order_mark()
    {
        Assert.Equal(ExitCode.Success, _run.Sort([0xEF, 0xBB, 0xBF, .. "2. Banana\r\n1. Apple\r\n"u8]));

        Assert.Equal("1. Apple\r\n2. Banana\r\n"u8.ToArray(), _run.Output);
        Assert.Equal("crlf", _run.SummaryField("Line ending"));
        Assert.Equal("yes, in the input; not written", _run.SummaryField("Byte order mark"));
    }

    [Fact]
    public void Redirected_error_stream()
    {
        // The console of the tests counts its error stream as redirected.
        Assert.Equal(ExitCode.Success, _run.Sort(Input));

        // The settings are written and nothing about progress: no other line, no carriage return, no escape code.
        string[] lines = [.. _run.Error.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];
        Assert.Equal(SettingsNames, lines.Select(line => line.Split(':')[0]));
        string withLineFeeds = _run.Error.Replace(Environment.NewLine, "\n", StringComparison.Ordinal);
        Assert.DoesNotContain("\r", withLineFeeds, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b", _run.Error, StringComparison.Ordinal);
    }

    // Requirement: Lines with equal keys

    [Fact]
    public void Same_output_for_every_setting()
    {
        ContentOptions options = TestContent.Options(seed: 11, edgeCases: EdgeCases.All);
        byte[] input = TestContent.Generate(options, 256 * 1024);
        File.WriteAllBytes(_run.InputPath, input);

        // The smallest memory of the command line holds this input in one chunk, so the engine is given buffers small
        // enough that the input is cut into many runs, as a large input would be.
        int maxLineLength = new BlockGenerator(options).MaxLineLength;
        _run.Tune = settings => settings with
        {
            MemoryBytes = (64 * 1024) + 8192,
            WindowBytes = 8192,
            MaxLineLength = maxLineLength,
        };
        Assert.Equal(
            ExitCode.Success,
            _run.Run("--input", _run.InputPath, "--output", _run.OutputPath, "--threads", "1", "--memory", "256MB"));
        byte[] first = _run.Output;
        Assert.True(int.Parse(_run.SummaryField("Runs"), CultureInfo.InvariantCulture) > 2);

        _run.Tune = settings => settings;
        _run.Console = new FakeConsole { IsErrorRedirected = true };
        Assert.Equal(ExitCode.Success, _run.Run("--input", _run.InputPath, "--output", _run.OutputPath, "--overwrite"));

        Assert.Equal(first, _run.Output);
        Assert.Equal(TestSort.Expected(input, LineEnding.Lf), first);
    }

    [Fact]
    public void Equal_keys_different_bytes()
    {
        Assert.Equal(ExitCode.Success, _run.Sort("7. Apple\n007. Apple\n07. Apple\n00. Apple\n0. Apple\n"));

        Assert.Equal("0. Apple\n00. Apple\n007. Apple\n07. Apple\n7. Apple\n", File.ReadAllText(_run.OutputPath));
    }
}
