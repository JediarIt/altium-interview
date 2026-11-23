using System.CommandLine.Parsing;
using System.Globalization;
using System.Security.Cryptography;

using FileSort.Cli;
using FileSort.Common;
using FileSort.Generator.Cli;
using FileSort.Generator.Cli.Options;
using FileSort.Generator.Content;
using FileSort.Generator.Output;
using FileSort.Tests.Cli;

namespace FileSort.Tests.Generator.Cli;

/// <summary>
/// The generator run in-process through its command line: the scenarios of "Command line", "Exit codes and error
/// messages", "Existing output file", "Seed", "Progress and summary" and "No partial output".
/// </summary>
public sealed class GeneratorAppTests : IDisposable
{
    private static readonly string[] SummaryFields =
    [
        "Output", "Size", "Lines", "Average line", "Seed", "Content version", "Write mode", "Elapsed", "Throughput",
        "Peak working set", "Reproduce",
    ];

    private readonly TempDirectory _directory = new();

    private string Output => _directory.File("out.txt");

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static int Run(FakeConsole console, params string[] args)
    {
        return GeneratorApp.Run(args, console, CancellationToken.None);
    }

    private static string Hash(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    }

    // A command line as a shell would split it: spaces separate arguments, double quotes keep them together.
    private static string[] SplitCommandLine(string commandLine)
    {
        return [.. CommandLineParser.SplitCommandLine(commandLine)];
    }

    // Requirement: Command line

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help(string option)
    {
        FakeConsole console = new();

        int exitCode = Run(console, option);

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.All(
            ["--output", "--size", "--seed", "--duplicates", "--words", "--edge-cases", "--line-ending", "--threads",
                "--overwrite", "--buffered-io", "--help", "--version"],
            name => Assert.Contains(name, console.OutText));
        Assert.Empty(console.ErrorText);
        Assert.Empty(_directory.FileNames());
    }

    [Fact]
    public void Help_shows_the_defaults()
    {
        FakeConsole console = new();

        Run(console, "--help");

        string threads = GeneratorSettings.DefaultThreads.ToString(CultureInfo.InvariantCulture);
        Assert.All(
            ["[default: 30]", "[default: 1-5]", "[default: lf]", $"[default: {threads}]"],
            text => Assert.Contains(text, console.OutText));
    }

    [Fact]
    public void Missing_required_option()
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--output", Output);

        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("--size", console.ErrorText);
        Assert.Empty(console.OutText);
        Assert.Empty(_directory.FileNames());
    }

    [Theory]
    [InlineData("--sizee", "--sizee", "1GB")]
    [InlineData("--size", "--size", "10XB")]
    [InlineData("--size", "--size", "0")]
    [InlineData("--size", "--size", "1,5GB")]
    [InlineData("--duplicates", "--size", "1MB", "--duplicates", "101")]
    [InlineData("--edge-cases", "--size", "1MB", "--edge-cases", "emojis")]
    [InlineData("--words", "--size", "1MB", "--words", "5-1")]
    [InlineData("--line-ending", "--size", "1MB", "--line-ending", "cr")]
    [InlineData("--threads", "--size", "1MB", "--threads", "0")]
    [InlineData("--threads", "--size", "1MB", "--threads", "257")]
    [InlineData("--seed", "--size", "1MB", "--seed", "18446744073709551616")]
    [InlineData("--seed", "--size", "1MB", "--seed", "abc")]
    [InlineData("--size", "--size", "1MB", "--size", "2MB")]
    [InlineData("extra", "--size", "1MB", "extra")]
    public void Invalid_arguments(string named, params string[] args)
    {
        FakeConsole console = new();

        int exitCode = Run(console, ["--output", Output, .. args]);

        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains(named, console.ErrorText);
        Assert.Empty(console.OutText);
        Assert.Empty(_directory.FileNames());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_output_path_is_an_invalid_argument(string output)
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--output", output, "--size", "1MB");

        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("--output", console.ErrorText);
        Assert.Empty(console.OutText);
    }

    [Fact]
    public void An_error_about_a_value_states_the_accepted_form()
    {
        FakeConsole size = new();
        FakeConsole edgeCases = new();
        FakeConsole lineEnding = new();

        Run(size, "--output", Output, "--size", "10XB");
        Run(edgeCases, "--output", Output, "--size", "1MB", "--edge-cases", "emojis");
        Run(lineEnding, "--output", Output, "--size", "1MB", "--line-ending", "cr");

        Assert.Contains("1.5GB", size.ErrorText);
        Assert.Contains("leading-zeros", edgeCases.ErrorText);
        Assert.Contains("crlf", lineEnding.ErrorText);
    }

    [Fact]
    public void Every_option_reaches_the_content()
    {
        FakeConsole console = new();

        int exitCode = Run(
            console, "-o", Output, "-s", "200KB", "--seed", "7", "--duplicates", "50", "--words", "2-7",
            "--edge-cases", "all", "--line-ending", "crlf", "--threads", "3", "--buffered-io");

        Assert.Equal(ExitCode.Success, exitCode);
        byte[] expected = TestContent.Generate(
            TestContent.Options(7, 50, "2-7", EdgeCases.All, LineEnding.CrLf), 200 * 1024);
        Assert.Equal(expected, File.ReadAllBytes(Output));
        Assert.Equal("buffered, 3 threads", console.SummaryField("Write mode"));
    }

    // Requirement: Exit codes and error messages

    [Fact]
    public void Not_enough_disk_space()
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--output", Output, "--size", "8000000TB");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("Not enough free space", console.ErrorText);
        Assert.Empty(console.OutText);
        Assert.Empty(_directory.FileNames());
    }

    [Fact]
    public void Not_enough_disk_space_leaves_an_existing_file_untouched()
    {
        File.WriteAllText(Output, "old");
        FakeConsole console = new();

        int exitCode = Run(console, "--output", Output, "--size", "8000000TB", "--overwrite");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("Not enough free space", console.ErrorText);
        Assert.Equal("old", File.ReadAllText(Output));
    }

    [Fact]
    public void Unwritable_output_path()
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--output", _directory.Path, "--size", "1MB");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains(_directory.Path, console.ErrorText);
        Assert.DoesNotContain("   at ", console.ErrorText);
        Assert.Empty(console.OutText);
    }

    [Fact]
    public void An_output_path_below_a_file_cannot_be_written()
    {
        File.WriteAllText(_directory.File("file"), "not a directory");
        string output = Path.Combine(_directory.File("file"), "out.txt");
        FakeConsole console = new();

        int exitCode = Run(console, "--output", output, "--size", "1MB");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains(output, console.ErrorText);
        Assert.DoesNotContain("   at ", console.ErrorText);
        Assert.Equal(["file"], _directory.FileNames());
    }

    // Requirement: Existing output file

    [Fact]
    public void Overwrite_option()
    {
        File.WriteAllText(Output, "old");
        FakeConsole console = new("n");

        int exitCode = Run(console, "--output", Output, "--size", "1MB", "--overwrite");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal(0, console.PromptsAnswered);
        Assert.True(new FileInfo(Output).Length >= 1024 * 1024);
        Assert.Equal(["out.txt"], _directory.FileNames());
    }

    [Theory]
    [InlineData("y")]
    [InlineData("Y")]
    [InlineData("yes")]
    [InlineData("YES")]
    [InlineData(" yes ")]
    public void Confirmed_at_the_prompt(string answer)
    {
        File.WriteAllText(Output, "old");
        FakeConsole console = new(answer);

        int exitCode = Run(console, "--output", Output, "--size", "1MB");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal(1, console.PromptsAnswered);
        Assert.Contains("[y/N]", console.ErrorText);
        Assert.True(new FileInfo(Output).Length >= 1024 * 1024);
    }

    [Theory]
    [InlineData("")]
    [InlineData("n")]
    [InlineData("no")]
    [InlineData("yep")]
    [InlineData("да")]
    [InlineData(null)]
    public void Declined_at_the_prompt(string? answer)
    {
        File.WriteAllText(Output, "old");
        FakeConsole console = answer is null ? new FakeConsole() : new FakeConsole(answer);

        int exitCode = Run(console, "--output", Output, "--size", "1MB");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Equal(1, console.PromptsAnswered);
        Assert.Contains("left untouched", console.ErrorText);
        Assert.Equal("old", File.ReadAllText(Output));
        Assert.Equal(["out.txt"], _directory.FileNames());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void No_console_to_ask(bool inputRedirected, bool errorRedirected)
    {
        File.WriteAllText(Output, "old");
        FakeConsole console = new("y") { IsInputRedirected = inputRedirected, IsErrorRedirected = errorRedirected };

        int exitCode = Run(console, "--output", Output, "--size", "1MB");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Equal(0, console.PromptsAnswered);
        Assert.DoesNotContain("[y/N]", console.ErrorText);
        Assert.Contains("--overwrite", console.ErrorText);
        Assert.Equal("old", File.ReadAllText(Output));
    }

    [Fact]
    public void A_stale_temporary_file_is_replaced_without_asking()
    {
        File.WriteAllText(FileSink.TemporaryPath(Output), "left by a run that was killed");
        FakeConsole console = new();

        int exitCode = Run(console, "--output", Output, "--size", "1MB");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal(0, console.PromptsAnswered);
        Assert.Equal(["out.txt"], _directory.FileNames());
    }

    [Fact]
    public void The_existing_file_survives_a_run_that_cannot_create_its_temporary_file()
    {
        File.WriteAllText(Output, "old");
        Directory.CreateDirectory(FileSink.TemporaryPath(Output));
        FakeConsole console = new();

        int exitCode = Run(console, "--output", Output, "--size", "1MB", "--overwrite");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("out.txt", console.ErrorText);
        Assert.DoesNotContain("   at ", console.ErrorText);
        Assert.Equal("old", File.ReadAllText(Output));
    }

    [Fact]
    public void An_existing_file_that_cannot_be_deleted_leaves_no_temporary_file()
    {
        // Windows refuses to delete a file that another program holds open; other systems delete it all the same.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to delete a file that is open.");
        File.WriteAllText(Output, "old");
        FakeConsole console = new();

        int exitCode;
        using (new FileStream(Output, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            exitCode = Run(console, "--output", Output, "--size", "1MB", "--overwrite");
        }

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("out.txt", console.ErrorText);
        Assert.Equal("old", File.ReadAllText(Output));
        Assert.Equal(["out.txt"], _directory.FileNames());
    }

    [Fact]
    public void An_output_path_that_ends_with_a_separator_is_a_directory()
    {
        string output = _directory.File("newdir") + Path.DirectorySeparatorChar;
        FakeConsole console = new();

        int exitCode = Run(console, "--output", output, "--size", "1MB");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("directory", console.ErrorText);
        Assert.Empty(_directory.FileNames());
        Assert.False(Directory.Exists(_directory.File("newdir")));
    }

    // Requirement: Seed

    [Fact]
    public void Seed_omitted()
    {
        FakeConsole first = new();
        FakeConsole second = new();
        string otherOutput = _directory.File("other.txt");

        Assert.Equal(ExitCode.Success, Run(first, "--output", Output, "--size", "1MB"));
        Assert.Equal(ExitCode.Success, Run(second, "--output", otherOutput, "--size", "1MB"));

        Assert.NotEqual(Hash(Output), Hash(otherOutput));
        Assert.NotEqual(first.SummaryField("Seed"), second.SummaryField("Seed"));
        Assert.True(ulong.TryParse(first.SummaryField("Seed"), out _));
    }

    [Theory]
    [InlineData]
    [InlineData("--seed", "18446744073709551615")]
    [InlineData("--seed", "5", "--duplicates", "0", "--words", "3", "--edge-cases", "empty,unicode", "--line-ending", "crlf")]
    [InlineData("--edge-cases", "all", "--threads", "2", "--buffered-io")]
    public void Reproducing_a_file(params string[] options)
    {
        FakeConsole first = new();
        FakeConsole second = new();
        string spaced = _directory.File("with space.txt");
        string otherOutput = _directory.File("other.txt");

        Assert.Equal(ExitCode.Success, Run(first, ["--output", spaced, "--size", "1MB", .. options]));

        // The printed command line, with another output path.
        string[] printed = SplitCommandLine(first.SummaryField("Reproduce"));
        Assert.Equal("FileSort.Generator", printed[0]);
        Assert.Equal(spaced, printed[2]);
        printed[2] = otherOutput;
        Assert.DoesNotContain("--threads", printed);
        Assert.DoesNotContain("--buffered-io", printed);
        Assert.DoesNotContain("--overwrite", printed);

        Assert.Equal(ExitCode.Success, Run(second, printed[1..]));

        Assert.Equal(Hash(spaced), Hash(otherOutput));
    }

    // Requirement: Progress and summary

    [Fact]
    public void Summary_after_a_successful_run()
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--output", Output, "--size", "3MB", "--seed", "42");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.All(SummaryFields, field => Assert.NotEmpty(console.SummaryField(field)));

        long size = new FileInfo(Output).Length;
        long lines = File.ReadAllBytes(Output).AsSpan().Count((byte)'\n');
        Assert.Equal(Output, console.SummaryField("Output"));
        Assert.StartsWith($"{size} bytes", console.SummaryField("Size"));
        Assert.Equal(lines.ToString(CultureInfo.InvariantCulture), console.SummaryField("Lines"));
        Assert.Equal("42", console.SummaryField("Seed"));
        Assert.Equal(ContentFormat.Version.ToString(CultureInfo.InvariantCulture), console.SummaryField("Content version"));
        Assert.EndsWith("MiB/s", console.SummaryField("Throughput"));
        Assert.EndsWith("MiB", console.SummaryField("Peak working set"));
    }

    [Fact]
    public void Settings_before_the_run()
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--output", Output, "--size", "1MB");

        // Only the output and the size were given: every other setting is shown with its default.
        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal(Output, console.SettingsField("Generating"));
        Assert.StartsWith("1048576 bytes", console.SettingsField("Requested size"));
        Assert.Equal("30%", console.SettingsField("Duplicates"));
        Assert.Equal("1-5", console.SettingsField("Words"));
        Assert.Equal("none", console.SettingsField("Edge cases"));
        Assert.Equal("lf", console.SettingsField("Line ending"));
        Assert.Equal(GeneratorSettings.DefaultThreads.ToString(CultureInfo.InvariantCulture), console.SettingsField("Threads"));
        Assert.Equal(OperatingSystem.IsWindows() ? "unbuffered" : "buffered", console.SettingsField("Write mode"));
        Assert.Equal("no", console.SettingsField("Overwrite"));
        Assert.Equal(ContentFormat.Version.ToString(CultureInfo.InvariantCulture), console.SettingsField("Content version"));

        // The seed was chosen by the generator: it is shown before the run and is the one in the summary.
        Assert.EndsWith("(chosen at random)", console.SettingsField("Seed"));
        Assert.StartsWith(console.SummaryField("Seed"), console.SettingsField("Seed"));

        // The settings go to the error stream, before any progress; standard output holds the summary only.
        Assert.Contains(" left", console.ErrorText);
        Assert.True(
            console.ErrorText.IndexOf("Content version:", StringComparison.Ordinal)
                < console.ErrorText.IndexOf(" left", StringComparison.Ordinal));
        Assert.DoesNotContain("Generating:", console.OutText);
    }

    [Fact]
    public void Settings_before_the_run_show_the_given_values()
    {
        FakeConsole console = new();

        Run(
            console, "--output", Output, "--size", "200KB", "--seed", "7", "--duplicates", "50", "--words", "2-7",
            "--edge-cases", "empty,unicode", "--line-ending", "crlf", "--threads", "3", "--buffered-io", "--overwrite");

        Assert.StartsWith("204800 bytes", console.SettingsField("Requested size"));
        Assert.Equal("7", console.SettingsField("Seed"));
        Assert.Equal("50%", console.SettingsField("Duplicates"));
        Assert.Equal("2-7", console.SettingsField("Words"));
        Assert.Equal("unicode,empty", console.SettingsField("Edge cases"));
        Assert.Equal("crlf", console.SettingsField("Line ending"));
        Assert.Equal("3", console.SettingsField("Threads"));
        Assert.Equal("buffered", console.SettingsField("Write mode"));
        Assert.Equal("yes", console.SettingsField("Overwrite"));
    }

    [Fact]
    public void Settings_are_printed_when_the_error_stream_is_redirected()
    {
        FakeConsole console = new() { IsErrorRedirected = true };

        Run(console, "--output", Output, "--size", "1MB", "--seed", "7");

        // A log file gets the settings, and no progress updates.
        Assert.Equal("7", console.SettingsField("Seed"));
        Assert.DoesNotContain(" left", console.ErrorText);
    }

    [Fact]
    public void A_cancelled_run_has_shown_its_seed()
    {
        using CancellationTokenSource cancellation = new();
        FakeConsole console = new()
        {
            OnErrorWrite = text =>
            {
                if (text.Contains(" left", StringComparison.Ordinal))
                {
                    cancellation.Cancel();
                }
            },
        };

        int exitCode = GeneratorApp.Run(["--output", Output, "--size", "32MB"], console, cancellation.Token);

        // No summary is printed for a cancelled run: the settings before it are the only record of the seed.
        Assert.Equal(ExitCode.Cancelled, exitCode);
        Assert.Empty(console.OutText);
        Assert.True(ulong.TryParse(console.SettingsField("Seed").Split(' ')[0], out _));
    }

    [Fact]
    public void Settings_are_not_printed_when_the_run_does_not_start()
    {
        File.WriteAllText(Output, "old");
        FakeConsole console = new("n");

        Run(console, "--output", Output, "--size", "1MB");

        Assert.DoesNotContain("Generating:", console.ErrorText);
    }

    [Fact]
    public void Progress_is_shown_on_the_error_stream()
    {
        FakeConsole console = new();

        Run(console, "--output", Output, "--size", "3MB");

        Assert.Contains(" left", console.ErrorText);
        Assert.Contains("MiB/s", console.ErrorText);
        Assert.DoesNotContain(" left", console.OutText);
    }

    [Fact]
    public void Redirected_error_stream()
    {
        FakeConsole console = new() { IsErrorRedirected = true };

        int exitCode = Run(console, "--output", Output, "--size", "3MB");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.DoesNotContain(" left", console.ErrorText);
        Assert.DoesNotContain("MiB/s", console.ErrorText);
    }

    // Requirement: No partial output

    [Fact]
    public void Successful_run()
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--output", _directory.File(Path.Combine("new", "directory", "out.txt")), "--size", "1MB");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal([Path.Combine("new", "directory", "out.txt")], _directory.FileNames());
    }

    [Fact]
    public void Failure_during_generation()
    {
        File.WriteAllText(Output, "old");
        bool failed = false;
        FakeConsole console = new()
        {
            // The first progress update fails like a broken stream would, after the first block was generated.
            OnErrorWrite = text =>
            {
                if (!failed && text.Contains(" left", StringComparison.Ordinal))
                {
                    failed = true;
                    throw new IOException("The stream is broken.");
                }
            },
        };

        int exitCode = Run(console, "--output", Output, "--size", "32MB", "--overwrite");

        Assert.True(failed);
        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("Cannot write to the console, so no file was written: The stream is broken.", console.ErrorText);
        Assert.DoesNotContain("   at ", console.ErrorText);
        Assert.Empty(_directory.FileNames());

        // A run that fails has no summary: its settings, printed before it, are the record of the seed.
        Assert.True(ulong.TryParse(console.SettingsField("Seed").Split(' ')[0], out _));
    }

    [Fact]
    public void A_summary_that_cannot_be_printed_keeps_the_file()
    {
        FakeConsole console = new()
        {
            // Standard output fails like a stream redirected to a file on a full disk, after the file was written.
            OnOutWrite = _ => throw new IOException("There is not enough space on the disk."),
        };

        int exitCode = Run(console, "--output", Output, "--size", "1MB", "--seed", "1");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Contains("The file was written, but the summary could not be printed", console.ErrorText);
        Assert.True(new FileInfo(Output).Length >= 1024 * 1024);
        Assert.Equal(["out.txt"], _directory.FileNames());
    }

    [Fact]
    public void A_progress_line_that_cannot_be_removed_keeps_the_summary()
    {
        FakeConsole console = new()
        {
            // Fails the write that blanks the progress line once the file is in place: a carriage return followed
            // by spaces only. Progress updates have digits, and line breaks are not followed by spaces.
            OnErrorWrite = text =>
            {
                if (text.Length > 1 && text[0] == '\r' && text[1..].All(c => c == ' '))
                {
                    throw new IOException("The terminal has gone.");
                }
            },
        };

        int exitCode = Run(console, "--output", Output, "--size", "1MB", "--seed", "1");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal("1", console.SummaryField("Seed"));
        Assert.Equal(["out.txt"], _directory.FileNames());
    }

    [Fact]
    public void Settings_that_cannot_be_printed_stop_the_run()
    {
        FakeConsole console = new()
        {
            OnErrorWrite = text =>
            {
                if (text.StartsWith("Generating:", StringComparison.Ordinal))
                {
                    throw new IOException("The stream is broken.");
                }
            },
        };

        int exitCode = Run(console, "--output", Output, "--size", "1MB");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("Cannot write to the console, so no file was written", console.ErrorText);
        Assert.Empty(console.OutText);
        Assert.Empty(_directory.FileNames());
    }

    [Fact]
    public void A_broken_error_stream_still_ends_with_an_exit_code()
    {
        // Every write to the error stream fails, the message about the failure included.
        FakeConsole console = new() { OnErrorWrite = _ => throw new IOException("The stream is broken.") };

        int exitCode = Run(console, "--output", Output, "--size", "1MB");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Empty(console.OutText);
        Assert.Empty(_directory.FileNames());
    }

    [Fact]
    public void Cancellation_during_generation()
    {
        using CancellationTokenSource cancellation = new();
        FakeConsole console = new()
        {
            // Cancels like Ctrl+C would, at the first progress update.
            OnErrorWrite = text =>
            {
                if (text.Contains(" left", StringComparison.Ordinal))
                {
                    cancellation.Cancel();
                }
            },
        };

        int exitCode = GeneratorApp.Run(["--output", Output, "--size", "32MB"], console, cancellation.Token);

        Assert.Equal(ExitCode.Cancelled, exitCode);
        Assert.Equal(130, exitCode);
        Assert.Contains("Cancelled", console.ErrorText);
        Assert.Empty(console.OutText);
        Assert.Empty(_directory.FileNames());
    }

    [Fact]
    public void Cancellation_with_a_broken_error_stream_still_ends_with_130()
    {
        using CancellationTokenSource cancellation = new();
        FakeConsole console = new()
        {
            // Cancels at the first progress update; from then on every write to the error stream fails, the removal
            // of the progress line and the message about the cancellation included.
            OnErrorWrite = text =>
            {
                if (cancellation.IsCancellationRequested)
                {
                    throw new IOException("The stream is broken.");
                }

                if (text.Contains(" left", StringComparison.Ordinal))
                {
                    cancellation.Cancel();
                }
            },
        };

        int exitCode = GeneratorApp.Run(["--output", Output, "--size", "32MB"], console, cancellation.Token);

        Assert.Equal(ExitCode.Cancelled, exitCode);
        Assert.Empty(_directory.FileNames());
    }

    [Fact]
    public void Cancellation_at_the_prompt_leaves_the_existing_file()
    {
        File.WriteAllText(Output, "old");
        using CancellationTokenSource cancellation = new();
        FakeConsole console = new();
        console.OnErrorWrite = text =>
        {
            if (text.Contains("[y/N]", StringComparison.Ordinal))
            {
                cancellation.Cancel();
            }
        };

        int exitCode = GeneratorApp.Run(["--output", Output, "--size", "1MB"], console, cancellation.Token);

        Assert.Equal(ExitCode.Cancelled, exitCode);
        Assert.Equal("old", File.ReadAllText(Output));
    }
}
