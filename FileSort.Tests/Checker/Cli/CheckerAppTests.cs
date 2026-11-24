using FileSort.Checker.Checks;
using FileSort.Cli;
using FileSort.Tests.Cli;

namespace FileSort.Tests.Checker.Cli;

/// <summary>
/// The checker run in-process through its command line: the scenarios of "Command line", "Files are only read",
/// "Exit codes and error messages", "Cancellation" and "Line longer than supported" of the <c>output-checker</c>
/// spec.
/// </summary>
public sealed class CheckerAppTests : IDisposable
{
    private const string Input =
        "415. Apple\n30432. Something something something\n1. Apple\n32. Cherry is the best\n2. Banana is yellow\n";

    private const string Sorted =
        "1. Apple\n415. Apple\n2. Banana is yellow\n32. Cherry is the best\n30432. Something something something\n";

    private readonly CheckerRun _run = new();

    public void Dispose()
    {
        _run.Dispose();
    }

    // Requirement: Command line

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help(string option)
    {
        int exitCode = _run.Run(option);

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.All(
            ["--input", "-i", "--sorted", "-s", "--line-ending", "--threads", "--help", "--version"],
            name => Assert.Contains(name, _run.Out));
        Assert.Empty(_run.Error);
    }

    [Fact]
    public void Help_shows_the_default_of_the_line_ending()
    {
        _run.Run("--help");

        Assert.Contains("[default: auto]", _run.Out);
    }

    [Fact]
    public void Help_shows_the_default_of_the_threads()
    {
        _run.Run("--help");

        Assert.Contains($"[default: {ScanSettings.DefaultThreads}]", _run.Out);
    }

    [Fact]
    public void Help_names_the_exit_codes()
    {
        _run.Run("--help");

        string[] codes = ["0 correct", "4 not correct", "3 a file is not valid", "130 cancelled"];
        Assert.All(codes, text => Assert.Contains(text, _run.Out));
    }

    [Fact]
    public void Missing_required_option()
    {
        int exitCode = _run.Run("--input", _run.InputPath);

        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("--sorted", _run.Error);
        Assert.Empty(_run.Out);
    }

    [Theory]
    [InlineData("--inputt", "--inputt", "data/test.txt")]
    [InlineData("--line-ending", "--line-ending", "cr")]
    [InlineData("--line-ending", "--line-ending", "LF")]
    [InlineData("--input", "--input", "another.txt")]
    [InlineData("--threads", "--threads", "0")]
    [InlineData("--threads", "--threads", "65")]
    [InlineData("--threads", "--threads", "-1")]
    [InlineData("--threads", "--threads", "many")]
    [InlineData("--threads", "--threads", "1.5")]
    [InlineData("extra", "extra")]
    [InlineData("--output", "--output", "sorted.txt")]
    public void Invalid_arguments(string named, params string[] args)
    {
        int exitCode = _run.Check(Input, Sorted, args);

        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains(named, _run.Error);
        Assert.Empty(_run.Out);
        Assert.DoesNotContain("Checking:", _run.Error);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("64")]
    public void The_smallest_and_the_largest_number_of_threads_are_accepted(string threads)
    {
        Assert.Equal(ExitCode.Success, _run.Check(Input, Sorted, "--threads", threads));
        Assert.Equal(threads, _run.SettingsField("Threads"));
    }

    [Fact]
    public void An_error_about_the_threads_states_the_accepted_range()
    {
        _run.Check(Input, Sorted, "--threads", "0");

        Assert.Contains("from 1 to 64", _run.Error);
    }

    [Fact]
    public void An_error_about_the_line_ending_states_the_accepted_values()
    {
        _run.Check(Input, Sorted, "--line-ending", "cr");

        Assert.All(["auto", "lf", "crlf"], value => Assert.Contains(value, _run.Error));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_path_is_an_invalid_argument(string path)
    {
        Assert.Equal(ExitCode.InvalidArguments, _run.Run("--input", path, "--sorted", _run.SortedPath));
        Assert.Contains("--input", _run.Error);

        _run.Console = new FakeConsole();
        Assert.Equal(ExitCode.InvalidArguments, _run.Run("--input", _run.InputPath, "--sorted", path));
        Assert.Contains("--sorted", _run.Error);
    }

    [Fact]
    public void The_same_file_twice()
    {
        File.WriteAllText(_run.InputPath, Sorted);

        int exitCode = _run.Run("--input", _run.InputPath, "--sorted", _run.InputPath);

        // A file compared with itself would always have "the same lines". Nothing is read: no settings are
        // printed, and a file that does not exist gets the same answer.
        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("--input", _run.Error);
        Assert.Contains("--sorted", _run.Error);
        Assert.Contains("same file", _run.Error);
        Assert.DoesNotContain("Checking:", _run.Error);
        Assert.Empty(_run.Out);
    }

    [Fact]
    public void The_same_file_under_two_spellings_of_its_path()
    {
        string directory = Path.GetDirectoryName(_run.InputPath)!;
        string detour = Path.Combine(directory, "..", Path.GetFileName(directory), "input.txt");

        Assert.Equal(ExitCode.InvalidArguments, _run.Run("-i", _run.InputPath, "-s", detour));
        Assert.Contains("same file", _run.Error);
    }

    [Fact]
    public void The_same_file_under_another_letter_case_where_the_file_system_ignores_case()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "File names differ by letter case on this file system.");

        string otherCase = _run.InputPath.ToUpperInvariant();
        Assert.Equal(ExitCode.InvalidArguments, _run.Run("-i", _run.InputPath, "-s", otherCase));
        Assert.Contains("same file", _run.Error);
    }

    [Fact]
    public void Short_names_of_the_options()
    {
        File.WriteAllText(_run.InputPath, Input);
        File.WriteAllText(_run.SortedPath, Sorted);

        Assert.Equal(ExitCode.Success, _run.Run("-i", _run.InputPath, "-s", _run.SortedPath));
    }

    // Requirement: Files are only read

    [Theory]
    [InlineData(Sorted, ExitCode.Success)]
    [InlineData("1. Apple\n", ExitCode.NotCorrect)]
    [InlineData("Apple\n", ExitCode.InvalidFile)]
    public void Nothing_is_written(string sorted, int expectedExitCode)
    {
        int exitCode = _run.Check(Input, sorted);

        Assert.Equal(expectedExitCode, exitCode);
        Assert.Equal(Input, File.ReadAllText(_run.InputPath));
        Assert.Equal(sorted, File.ReadAllText(_run.SortedPath));
        Assert.Equal(["input.txt", "sorted.txt"], _run.FileNames);
    }

    [Fact]
    public void Files_that_another_program_is_reading_can_be_checked()
    {
        File.WriteAllText(_run.InputPath, Input);
        File.WriteAllText(_run.SortedPath, Sorted);
        using FileStream input = new(_run.InputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream sorted = new(_run.SortedPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.Equal(ExitCode.Success, _run.Run("--input", _run.InputPath, "--sorted", _run.SortedPath));
    }

    // Requirement: Exit codes and error messages

    [Fact]
    public void Exit_code_of_a_correct_output()
    {
        Assert.Equal(0, _run.Check(Input, Sorted));
    }

    [Theory]
    [InlineData("415. Apple\n1. Apple\n2. Banana is yellow\n32. Cherry is the best\n"
        + "30432. Something something something\n")]
    [InlineData("1. Apple\n415. Apple\n2. Banana is yellow\n32. Cherry is the best\n")]
    [InlineData("415. Apple\n1. Apple\n2. Banana is yellow\n32. Cherry is the best\n")]
    [InlineData("")]
    public void Incorrect_output(string sorted)
    {
        int exitCode = _run.Check(Input, sorted);

        // The order is violated, or a line is missing, or both; or the output is empty.
        Assert.Equal(4, exitCode);
        Assert.Equal("NOT CORRECT", _run.Field("Result"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Missing_file(bool inputIsMissing)
    {
        string existing = inputIsMissing ? _run.SortedPath : _run.InputPath;
        string missing = inputIsMissing ? _run.InputPath : _run.SortedPath;
        File.WriteAllText(existing, Sorted);

        int exitCode = _run.Run("--input", _run.InputPath, "--sorted", _run.SortedPath);

        Assert.Equal(1, exitCode);
        Assert.Contains($"Cannot read '{missing}'", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Empty(_run.Out);
        Assert.Equal([Path.GetFileName(existing)], _run.FileNames);
    }

    [Fact]
    public void A_directory_in_place_of_a_file()
    {
        File.WriteAllText(_run.InputPath, Input);
        string directory = Path.GetDirectoryName(_run.InputPath)!;

        int exitCode = _run.Run("--input", _run.InputPath, "--sorted", directory);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains($"Cannot read '{directory}'", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_device_in_place_of_a_file(bool asInput)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "NUL is the null device of Windows.");
        File.WriteAllText(_run.InputPath, Input);
        File.WriteAllText(_run.SortedPath, Sorted);

        int exitCode = asInput
            ? _run.Run("--input", "NUL", "--sorted", _run.SortedPath)
            : _run.Run("--input", _run.InputPath, "--sorted", "NUL");

        // A device cannot be read twice from its start, nor has it a size: the message names it, with no stack trace.
        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("Cannot read", _run.Error);
        Assert.Contains("NUL", _run.Error);
        Assert.Contains("not a regular file", _run.Error);
        Assert.DoesNotContain("Unexpected error", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Empty(_run.Out);
    }

    [Fact]
    public void A_path_that_is_not_a_path()
    {
        File.WriteAllText(_run.InputPath, Input);

        int exitCode = _run.Run("--input", _run.InputPath, "--sorted", "sorted\0.txt");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("Cannot read", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
    }

    // Requirement: Bounded memory, scenario "Line longer than supported"

    [Fact]
    public void Line_longer_than_supported()
    {
        ScanSettings scan = new() { MaxLineLength = 20 };
        string longLine = "2. " + new string('x', 30) + "\n";

        int exitCode = _run.Check("1. Apple\n" + longLine, "1. Apple\n", scan, TestContext.Current.CancellationToken);

        // The limit of the tests stands for the 64 MiB of the program: the message names the file and the line
        // and mentions the line ending as a possible cause.
        Assert.Equal(1, exitCode);
        Assert.Contains($"Cannot check '{_run.InputPath}'", _run.Error);
        Assert.Contains("line 2", _run.Error);
        Assert.Contains("longer than 20 bytes", _run.Error);
        Assert.Contains("--line-ending", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Empty(_run.Out);
    }

    [Fact]
    public void The_default_limit_on_a_line_is_64_MiB()
    {
        Assert.Equal(64 * 1024 * 1024, ScanSettings.Default.MaxLineLength);
    }

    // Requirement: Cancellation

    [Fact]
    public void A_check_run_with_a_cancelled_token_reports_the_cancellation()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        int exitCode = _run.Check(Input, Sorted, ScanSettings.Default, cancellation.Token);

        // The cancellation is reported, not a result.
        Assert.Equal(ExitCode.Cancelled, exitCode);
        Assert.Equal(130, exitCode);
        Assert.Contains("Cancelled", _run.Error);
        Assert.Empty(_run.Out);
        Assert.DoesNotContain("Result:", _run.Error);
    }

    [Fact]
    public void Cancellation_with_a_broken_error_stream_still_ends_with_130()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        _run.Console = new FakeConsole
        {
            // The settings are printed; every later write to the error stream fails, the message about the
            // cancellation included.
            IsErrorRedirected = true,
            OnErrorWrite = text =>
            {
                if (text.Contains("Cancelled", StringComparison.Ordinal))
                {
                    throw new IOException("The stream is broken.");
                }
            },
        };

        int exitCode = _run.Check(Input, Sorted, ScanSettings.Default, cancellation.Token);

        Assert.Equal(ExitCode.Cancelled, exitCode);
    }
}
