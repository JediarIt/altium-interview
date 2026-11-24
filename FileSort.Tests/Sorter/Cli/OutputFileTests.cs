using FileSort.Cli;
using FileSort.Tests.Cli;
using FileSort.Tests.Format;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Sorter.Cli;

/// <summary>
/// The output file and the end of a sort through the command line: the scenarios of "Output file" and "Cancellation"
/// of the <c>external-sorter</c> spec, the question before an output is replaced, and a console that fails.
/// </summary>
public sealed class OutputFileTests : IDisposable
{
    private const string Input = "2. Banana\n1. Apple\n";

    private const string Sorted = "1. Apple\n2. Banana\n";

    private readonly SorterRun _run = new();

    public void Dispose()
    {
        _run.Dispose();
    }

    // Requirement: Output file

    [Fact]
    public void Successful_sort()
    {
        Assert.Equal(ExitCode.Success, _run.Sort(Input));

        Assert.Equal(Sorted, File.ReadAllText(_run.OutputPath));
        Assert.Equal(["input.txt", "sorted.txt"], _run.FileNames);
        Assert.Empty(_run.Folders);
    }

    [Fact]
    public void Overwrite_option()
    {
        File.WriteAllText(_run.OutputPath, "old output");

        // A console that could ask, and would decline: the option means that it is not asked.
        _run.Console = new FakeConsole("n") { IsErrorRedirected = true };
        Assert.Equal(ExitCode.Success, _run.Sort(Input, "--overwrite"));

        Assert.Equal(Sorted, File.ReadAllText(_run.OutputPath));
        Assert.Equal(0, _run.Console.PromptsAnswered);
        Assert.Equal("yes", _run.SettingsField("Overwrite"));
    }

    [Theory]
    [InlineData("y")]
    [InlineData("yes")]
    [InlineData(" Y ")]
    public void Confirmed_at_the_prompt(string answer)
    {
        File.WriteAllText(_run.OutputPath, "old output");
        _run.Console = new FakeConsole(answer);

        Assert.Equal(ExitCode.Success, _run.Sort(Input));

        Assert.Equal(Sorted, File.ReadAllText(_run.OutputPath));
        Assert.Equal(1, _run.Console.PromptsAnswered);
        string question = $"'{_run.OutputPath}' already exists. Replace it? [y/N] ";
        Assert.StartsWith(question, _run.Error, StringComparison.Ordinal);
        Assert.Equal(["input.txt", "sorted.txt"], _run.FileNames);
    }

    [Theory]
    [InlineData("")]
    [InlineData("n")]
    [InlineData("no")]
    [InlineData("yes please")]
    [InlineData(null)]
    public void Declined_at_the_prompt(string? answer)
    {
        File.WriteAllText(_run.OutputPath, "old output");
        _run.Console = answer is null ? new FakeConsole() : new FakeConsole(answer);

        int exitCode = _run.Sort(Input);

        // Asked before anything is read: no settings are printed.
        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Equal("old output", File.ReadAllText(_run.OutputPath));
        Assert.Contains("was left untouched", _run.Error);
        Assert.DoesNotContain("Sorting:", _run.Error);
        Assert.Empty(_run.Out);
        Assert.Equal(["input.txt", "sorted.txt"], _run.FileNames);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void No_console_to_ask(bool inputRedirected, bool errorRedirected)
    {
        File.WriteAllText(_run.OutputPath, "old output");
        _run.Console = new FakeConsole("y")
        {
            IsInputRedirected = inputRedirected,
            IsErrorRedirected = errorRedirected,
        };

        int exitCode = _run.Sort(Input);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Equal(0, _run.Console.PromptsAnswered);
        Assert.Equal("old output", File.ReadAllText(_run.OutputPath));
        Assert.Contains("--overwrite", _run.Error);
        Assert.DoesNotContain("Replace it?", _run.Error);
        Assert.Equal(["input.txt", "sorted.txt"], _run.FileNames);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public void The_engine_may_replace_a_file_at_the_output_path_only_with_consent(
        bool outputExists, bool overwriteOption, bool replacementAllowed)
    {
        if (outputExists)
        {
            File.WriteAllText(_run.OutputPath, "old output");
            _run.Console = new FakeConsole("y");
        }

        Assert.Equal(ExitCode.Success, overwriteOption ? _run.Sort(Input, "--overwrite") : _run.Sort(Input));

        // Without an output at the start and without --overwrite, a file that appears while the sort runs is not
        // replaced.
        Assert.Equal(replacementAllowed, _run.EngineSettings!.Overwrite);
    }

    [Fact]
    public void A_temporary_file_left_by_an_earlier_sort_is_replaced()
    {
        File.WriteAllText(_run.OutputPath + ".tmp", "left by a sort that was killed");

        Assert.Equal(ExitCode.Success, _run.Sort(Input));

        Assert.Equal(Sorted, File.ReadAllText(_run.OutputPath));
        Assert.Equal(["input.txt", "sorted.txt"], _run.FileNames);
    }

    // Requirement: Cancellation

    [Fact]
    public void A_sort_run_with_a_cancelled_token_reports_the_cancellation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        int exitCode = _run.SortCancellable(Input, cancellation.Token);

        Assert.Equal(ExitCode.Cancelled, exitCode);
        Assert.Equal(130, exitCode);
        Assert.Contains("Cancelled", _run.Error);
        Assert.Empty(_run.Out);
        Assert.Equal(["input.txt"], _run.FileNames);
        Assert.Empty(_run.Folders);
    }

    [Fact]
    public void Cancelled_during_the_merge()
    {
        using CancellationTokenSource cancellation = new();
        int asked = 0;

        // Tiny memory: dozens of runs and merges of groups. The free space is asked again before the first group,
        // when the runs exist, and that is where the cancellation comes.
        _run.Tune = settings => settings with
        {
            MemoryBytes = 1600 + 4096,
            WindowBytes = 4096,
            MaxLineLength = 200,
            FreeSpace = _ =>
            {
                if (++asked == 2)
                {
                    cancellation.Cancel();
                }

                return null;
            },
        };
        byte[] input = TestContent.Generate(TestContent.Options(seed: 3), 64 * 1024);

        int exitCode = _run.SortCancellable(input, cancellation.Token, "--threads", "3");

        Assert.Equal(ExitCode.Cancelled, exitCode);
        Assert.True(asked >= 2, "The cancellation came before the merge.");
        Assert.Contains("Cancelled", _run.Error);
        Assert.Equal(["input.txt"], _run.FileNames);
        Assert.Empty(_run.Folders);
    }

    [Fact]
    public void Cancellation_with_a_broken_error_stream_still_ends_with_130()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        _run.Console = new FakeConsole
        {
            // The settings are printed; the message about the cancellation fails.
            IsErrorRedirected = true,
            OnErrorWrite = text =>
            {
                if (text.Contains("Cancelled", StringComparison.Ordinal))
                {
                    throw new IOException("The stream is broken.");
                }
            },
        };

        Assert.Equal(ExitCode.Cancelled, _run.SortCancellable(Input, cancellation.Token));
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    // A console that cannot be written to

    [Fact]
    public void A_console_that_fails_before_the_output_is_written_stops_the_sort()
    {
        _run.Console = new FakeConsole
        {
            IsErrorRedirected = true,
            OnErrorWrite = _ => throw new IOException("The stream is broken."),
        };

        int exitCode = _run.Sort(Input);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    [Fact]
    public void A_console_that_fails_after_the_output_is_written_keeps_the_output()
    {
        _run.Console = new FakeConsole
        {
            IsErrorRedirected = true,
            OnOutWrite = _ => throw new IOException("The stream is broken."),
        };

        int exitCode = _run.Sort(Input);

        // The output is in place, so the exit code says so; the error stream says that the summary was lost.
        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal(Sorted, File.ReadAllText(_run.OutputPath));
        Assert.Contains("the summary could not be printed", _run.Error);
    }

    [Fact]
    public void The_output_is_the_naive_sort_of_a_generated_input_with_runs_and_merges()
    {
        byte[] input = TestContent.Generate(TestContent.Options(seed: 3), 64 * 1024);
        _run.Tune = settings => settings with { MemoryBytes = 1600 + 4096, WindowBytes = 4096, MaxLineLength = 200 };

        Assert.Equal(ExitCode.Success, _run.Sort(input, "--threads", "3"));

        Assert.Equal(NaiveSort.SortFile(input, "\n"), _run.Output);
        Assert.True(SummaryNumber("Runs") > 2);
        Assert.True(SummaryNumber("Merge passes") > 1);
        Assert.Equal(["input.txt", "sorted.txt"], _run.FileNames);
        Assert.Empty(_run.Folders);
    }

    private int SummaryNumber(string name)
    {
        return int.Parse(_run.SummaryField(name), System.Globalization.CultureInfo.InvariantCulture);
    }
}
