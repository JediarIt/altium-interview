using System.Globalization;
using System.Text;

using FileSort.Cli;
using FileSort.Engine;
using FileSort.Sorter.Cli;
using FileSort.Sorter.Cli.Options;
using FileSort.Tests.Cli;

namespace FileSort.Tests.Sorter.Cli;

/// <summary>
/// The sorter run in-process through its command line: the scenarios of "Command line", "Parallelism" and "Exit codes
/// and error messages" of the <c>external-sorter</c> spec, with the messages about an input that is not accepted.
/// </summary>
public sealed class SorterAppTests : IDisposable
{
    private const string Input =
        "415. Apple\n30432. Something something something\n1. Apple\n32. Cherry is the best\n2. Banana is yellow\n";

    private const string Sorted =
        "1. Apple\n415. Apple\n2. Banana is yellow\n32. Cherry is the best\n30432. Something something something\n";

    private readonly SorterRun _run = new();

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
        string[] names =
        [
            "--input", "-i", "--output", "-o", "--temp", "--memory", "--threads", "--line-ending", "--overwrite",
            "--help", "--version",
        ];
        Assert.All(names, name => Assert.Contains(name, _run.Out));
        Assert.Empty(_run.Error);
    }

    [Fact]
    public void Help_shows_the_defaults()
    {
        _run.Run("--help");

        // The memory as it is typed, not as a number of bytes.
        Assert.Contains("[default: 1GB]", _run.Out);
        Assert.Contains("[default: auto]", _run.Out);
        Assert.Contains($"[default: {SorterSettings.DefaultThreads}]", _run.Out);
    }

    [Fact]
    public void Help_names_the_exit_codes()
    {
        _run.Run("--help");

        string[] codes =
        [
            "0 sorted", "3 the input is not valid", "2 invalid arguments", "1 the sort could not be done",
            "130 cancelled",
        ];
        Assert.All(codes, text => Assert.Contains(text, _run.Out));
    }

    [Fact]
    public void Missing_required_option()
    {
        File.WriteAllText(_run.InputPath, Input);

        int exitCode = _run.Run("--input", _run.InputPath);

        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("--output", _run.Error);
        Assert.Empty(_run.Out);
    }

    [Theory]
    [InlineData("--inputt", "--inputt", "data/test.txt")]
    [InlineData("--line-ending", "--line-ending", "cr")]
    [InlineData("--line-ending", "--line-ending", "LF")]
    [InlineData("--memory", "--memory", "lots")]
    [InlineData("--memory", "--memory", "255MB")]
    [InlineData("--memory", "--memory", "0")]
    [InlineData("--memory", "--memory", "1,5GB")]
    [InlineData("--threads", "--threads", "0")]
    [InlineData("--threads", "--threads", "65")]
    [InlineData("--threads", "--threads", "many")]
    [InlineData("--threads", "--threads", "1.5")]
    [InlineData("--input", "--input", "another.txt")]
    [InlineData("--temp", "--temp", "")]
    [InlineData("--sorted", "--sorted", "sorted.txt")]
    [InlineData("extra", "extra")]
    public void Invalid_arguments(string named, params string[] args)
    {
        int exitCode = _run.Sort(Input, args);

        // Nothing is read or written: the settings are not printed, and no file appears.
        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains(named, _run.Error);
        Assert.Empty(_run.Out);
        Assert.DoesNotContain("Sorting:", _run.Error);
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    [Fact]
    public void An_error_about_the_memory_states_the_smallest_value()
    {
        _run.Sort(Input, "--memory", "255MB");

        Assert.Contains("at least 256MB", _run.Error);
    }

    [Fact]
    public void An_error_about_the_threads_states_the_accepted_range()
    {
        _run.Sort(Input, "--threads", "0");

        Assert.Contains("from 1 to 64", _run.Error);
    }

    [Fact]
    public void An_error_about_the_line_ending_states_the_accepted_values()
    {
        _run.Sort(Input, "--line-ending", "cr");

        Assert.All(["auto", "lf", "crlf"], value => Assert.Contains(value, _run.Error));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_path_is_an_invalid_argument(string path)
    {
        Assert.Equal(ExitCode.InvalidArguments, _run.Run("--input", path, "--output", _run.OutputPath));
        Assert.Contains("--input", _run.Error);

        _run.Console = new FakeConsole { IsErrorRedirected = true };
        Assert.Equal(ExitCode.InvalidArguments, _run.Run("--input", _run.InputPath, "--output", path));
        Assert.Contains("--output", _run.Error);
    }

    [Theory]
    [InlineData("256MB")]
    [InlineData("0.25GB")]
    [InlineData("268435456")]
    public void The_smallest_memory_is_accepted_and_the_engine_gets_it_less_the_reserve(string memory)
    {
        Assert.Equal(ExitCode.Success, _run.Sort(Input, "--memory", memory));

        Assert.Equal(Sorted, File.ReadAllText(_run.OutputPath));
        Assert.Equal((256L * 1024 * 1024) - SorterApp.Reserve, _run.EngineSettings!.MemoryBytes);
    }

    [Fact]
    public void The_default_memory_is_1_GB()
    {
        Assert.Equal(ExitCode.Success, _run.Sort(Input));

        Assert.Equal((1L << 30) - SorterApp.Reserve, _run.EngineSettings!.MemoryBytes);
    }

    [Fact]
    public void The_same_file_twice()
    {
        File.WriteAllText(_run.InputPath, Input);

        int exitCode = _run.Run("--input", _run.InputPath, "--output", _run.InputPath);

        // A sort in place would lose the input if it failed during the final merge. Nothing is read or written: no
        // settings are printed, and the input keeps its bytes.
        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("--input", _run.Error);
        Assert.Contains("--output", _run.Error);
        Assert.Contains("same file", _run.Error);
        Assert.DoesNotContain("Sorting:", _run.Error);
        Assert.Empty(_run.Out);
        Assert.Equal(Input, File.ReadAllText(_run.InputPath));
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    [Fact]
    public void The_same_file_under_two_spellings_of_its_path()
    {
        string directory = Path.GetDirectoryName(_run.InputPath)!;
        string detour = Path.Combine(directory, "..", Path.GetFileName(directory), "input.txt");

        Assert.Equal(ExitCode.InvalidArguments, _run.Run("-i", _run.InputPath, "-o", detour));
        Assert.Contains("same file", _run.Error);
    }

    [Fact]
    public void The_same_file_under_another_letter_case_where_the_file_system_ignores_case()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "File names differ by letter case on this file system.");

        string otherCase = _run.InputPath.ToUpperInvariant();
        Assert.Equal(ExitCode.InvalidArguments, _run.Run("-i", _run.InputPath, "-o", otherCase));
        Assert.Contains("same file", _run.Error);
    }

    [Fact]
    public void Short_names_of_the_options()
    {
        File.WriteAllText(_run.InputPath, Input);

        Assert.Equal(ExitCode.Success, _run.Run("-i", _run.InputPath, "-o", _run.OutputPath));
        Assert.Equal(Sorted, File.ReadAllText(_run.OutputPath));
    }

    [Fact]
    public void The_entry_of_the_program_gives_the_engine_its_settings_unchanged()
    {
        File.WriteAllText(_run.InputPath, Input);
        FakeConsole console = new() { IsErrorRedirected = true };

        string[] args = ["-i", _run.InputPath, "-o", _run.OutputPath];
        int exitCode = SorterApp.Run(args, console, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal(Sorted, File.ReadAllText(_run.OutputPath));
    }

    // Requirement: Parallelism

    [Fact]
    public void Default_number_of_threads()
    {
        _run.Sort(Input);

        int expected = Math.Min(Environment.ProcessorCount, 64);
        Assert.Equal(expected.ToString(CultureInfo.InvariantCulture), _run.SettingsField("Threads"));
        Assert.Equal(expected, _run.EngineSettings!.Threads);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("64")]
    public void The_smallest_and_the_largest_number_of_threads_are_accepted(string threads)
    {
        Assert.Equal(ExitCode.Success, _run.Sort(Input, "--threads", threads));

        Assert.Equal(threads, _run.SettingsField("Threads"));
        Assert.Equal(Sorted, File.ReadAllText(_run.OutputPath));
    }

    [Fact]
    public void Number_of_threads_out_of_range()
    {
        int exitCode = _run.Sort(Input, "--threads", "0");

        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("--threads", _run.Error);
    }

    // Requirement: Exit codes and error messages

    [Fact]
    public void Exit_code_of_a_sorted_output()
    {
        Assert.Equal(0, _run.Sort(Input));
        Assert.Equal(Sorted, File.ReadAllText(_run.OutputPath));
    }

    [Fact]
    public void Missing_input_file()
    {
        int exitCode = _run.Run("--input", _run.InputPath, "--output", _run.OutputPath);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains($"Cannot read '{_run.InputPath}'", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Empty(_run.Out);
        Assert.Empty(_run.FileNames);
    }

    [Fact]
    public void Unwritable_output_path_that_is_a_directory()
    {
        File.WriteAllText(_run.InputPath, Input);
        string directory = Directory.CreateDirectory(Path.Combine(_run.DirectoryPath, "folder")).FullName;

        int exitCode = _run.Run("--input", _run.InputPath, "--output", directory);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains($"Cannot write '{directory}'", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Empty(_run.Out);
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    [Fact]
    public void Unwritable_output_path_in_a_directory_that_does_not_exist()
    {
        File.WriteAllText(_run.InputPath, Input);
        string output = Path.Combine(_run.DirectoryPath, "missing", "sorted.txt");

        int exitCode = _run.Run("--input", _run.InputPath, "--output", output);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains($"Cannot write '{output}'", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    [Fact]
    public void Temporary_directory_that_does_not_exist()
    {
        string temp = Path.Combine(_run.DirectoryPath, "missing");

        int exitCode = _run.Sort(Input, "--temp", temp);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains(temp, _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    [Fact]
    public void A_path_that_is_not_a_path()
    {
        File.WriteAllText(_run.InputPath, Input);

        int exitCode = _run.Run("--input", _run.InputPath, "--output", "sorted\0.txt");

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("is not a valid path", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    [Fact]
    public void A_device_as_the_input()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "NUL is the null device of Windows.");

        int exitCode = _run.Run("--input", "NUL", "--output", _run.OutputPath);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("NUL", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Empty(_run.FileNames);
    }

    [Fact]
    public void Not_enough_disk_space()
    {
        _run.Tune = settings => settings with { FreeSpace = _ => 10 };

        int exitCode = _run.Sort(Input);

        // The runs of the input, the first need of the sort, do not fit.
        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains($"{Input.Length} bytes are needed, 10 are free", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Empty(_run.Out);
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    // Requirement: Invalid input

    [Fact]
    public void Line_without_a_separator()
    {
        int exitCode = _run.Sort("1. Apple\nApple\n");

        Assert.Equal(ExitCode.InvalidFile, exitCode);
        Assert.Equal(3, exitCode);
        Assert.Contains($"Invalid line in the input: {_run.InputPath}", _run.Error);
        Assert.Contains("Line:           2", _run.Error);
        Assert.Contains("Offset:         9 bytes from the start of the file", _run.Error);
        Assert.Contains("Reason:         the line does not start with a digit", _run.Error);
        Assert.Contains("No output was written.", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Empty(_run.Out);
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    [Theory]
    [InlineData("1. Apple\n\n2. Banana\n", "the line is empty")]
    [InlineData("1. Apple\n2 Banana\n", "the digits of the Number are not followed by a period and a space")]
    public void The_reason_of_an_invalid_line(string input, string reason)
    {
        Assert.Equal(ExitCode.InvalidFile, _run.Sort(input));

        Assert.Contains("Line:           2", _run.Error);
        Assert.Contains($"Reason:         {reason}", _run.Error);
    }

    [Fact]
    public void A_String_that_is_not_well_formed_UTF_8()
    {
        Assert.Equal(ExitCode.InvalidFile, _run.Sort([.. "1. Apple\n2. "u8, 0xC3, 0x28, (byte)'\n']));

        Assert.Contains("Reason:         the String is not well-formed UTF-8", _run.Error);
    }

    [Fact]
    public void An_invalid_input_leaves_an_existing_output_untouched()
    {
        File.WriteAllText(_run.OutputPath, "old output");

        Assert.Equal(ExitCode.InvalidFile, _run.Sort("1. Apple\nApple\n", "--overwrite"));

        Assert.Equal("old output", File.ReadAllText(_run.OutputPath));
        Assert.Equal(["input.txt", "sorted.txt"], _run.FileNames);
    }

    // Requirement: Line ending

    [Fact]
    public void Line_ending_given_explicitly()
    {
        byte[] input = "1. Apple\r\n2. Banana\n"u8.ToArray();

        Assert.Equal(ExitCode.Success, _run.Sort(input, "--line-ending", "lf"));
        Assert.Equal("1. Apple\r\n2. Banana\n"u8.ToArray(), _run.Output);

        // Without the option the first line feed has a carriage return before it, and the next line feed has none.
        _run.Console = new FakeConsole { IsErrorRedirected = true };
        Assert.Equal(ExitCode.InvalidFile, _run.Sort(input, "--overwrite"));
        Assert.Contains("Line:           2", _run.Error);
        Assert.Contains(@"line feed without a carriage return before it", _run.Error);
        Assert.Contains(
            @"Line ending:    crlf (\r\n), from the input; give --line-ending if that is wrong", _run.Error);
    }

    [Fact]
    public void A_line_ending_that_was_given_is_named_as_given()
    {
        Assert.Equal(ExitCode.InvalidFile, _run.Sort("1. Apple\nApple\n", "--line-ending", "lf"));

        Assert.Contains(@"Line ending:    lf (\n), given", _run.Error);
    }

    // Requirement: Line length

    [Fact]
    public void Line_longer_than_supported()
    {
        // A line of 1 MB and one byte, after a line of 8 bytes and its terminator.
        string longLine = "2. " + new string('x', SortSettings.DefaultMaxLineLength - 2) + "\n";

        int exitCode = _run.Sort("1. Apple\n" + longLine);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains($"longer than {SortSettings.DefaultMaxLineLength} bytes: {_run.InputPath}", _run.Error);
        Assert.Contains("Line:           2", _run.Error);
        Assert.Contains("Offset:         9 bytes from the start of the file", _run.Error);
        Assert.Contains("the wrong line ending can look like one long line", _run.Error);
        Assert.Contains("No output was written.", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Empty(_run.Out);
        Assert.Equal(["input.txt"], _run.FileNames);
    }

    [Fact]
    public void Longest_line()
    {
        string longest = "2. " + new string('x', SortSettings.DefaultMaxLineLength - 3);
        string input = "3. Cherry\n" + longest + "\n1. Apple\n";

        Assert.Equal(ExitCode.Success, _run.Sort(input));

        Assert.Equal(Encoding.UTF8.GetBytes("1. Apple\n3. Cherry\n" + longest + "\n"), _run.Output);
    }
}
