using System.CommandLine;

using FileSort.Cli;

namespace FileSort.Tests.Cli;

/// <summary>
/// The shared way to run a command line, on a small command of the tests: what is printed where, and the exit code.
/// </summary>
public class CommandRunnerTests
{
    private const int ActionExitCode = 7;

    // A required path, a mode limited to two names, and a number: one option of each kind that reports errors
    // in its own way.
    private static RootCommand Command(Action<ParseResult>? action = null)
    {
        Option<string> file = new("--file", "-f") { Required = true };
        Option<string> mode = new("--mode") { DefaultValueFactory = _ => "fast" };
        mode.AcceptOnlyFromAmong("fast", "slow");
        Option<int> count = new("--count");

        RootCommand root = new("A command of the tests.") { file, mode, count };
        root.SetAction(result =>
        {
            action?.Invoke(result);
            return ActionExitCode;
        });
        return root;
    }

    private static int Run(FakeConsole console, params string[] args)
    {
        return CommandRunner.Run(Command(), args, console);
    }

    [Fact]
    public void A_valid_command_line_runs_the_action_and_returns_its_exit_code()
    {
        FakeConsole console = new();
        string? seen = null;

        int exitCode = CommandRunner.Run(
            Command(result => seen = result.GetValue<string>("--file")), ["--file", "a.txt"], console);

        Assert.Equal(ActionExitCode, exitCode);
        Assert.Equal("a.txt", seen);
        Assert.Empty(console.ErrorText);
    }

    [Fact]
    public void A_missing_required_option_is_named()
    {
        FakeConsole console = new();

        int exitCode = Run(console);

        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("--file", console.ErrorText);
        Assert.Contains("--help", console.ErrorText);
        Assert.Empty(console.OutText);
    }

    [Fact]
    public void A_value_that_is_not_accepted_gets_the_name_of_its_option()
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--file", "a.txt", "--mode", "medium");

        // The library says "Argument 'medium' not recognized" and does not say for which option.
        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("Option '--mode':", console.ErrorText);
        Assert.Contains("medium", console.ErrorText);
    }

    [Fact]
    public void A_value_of_the_wrong_type_gets_the_name_of_its_option()
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--file", "a.txt", "--count", "many");

        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("--count", console.ErrorText);
    }

    [Fact]
    public void A_message_that_already_names_its_option_is_not_prefixed()
    {
        FakeConsole console = new();

        Run(console);

        // "Option '--file' is required." names the option by itself.
        Assert.DoesNotContain("Option '--file': ", console.ErrorText);
    }

    [Fact]
    public void An_unknown_option_is_named()
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--file", "a.txt", "--filee", "b.txt");

        Assert.Equal(ExitCode.InvalidArguments, exitCode);
        Assert.Contains("--filee", console.ErrorText);
    }

    [Fact]
    public void The_same_message_is_printed_once()
    {
        FakeConsole console = new();

        Run(console, "--file", "a.txt", "extra", "extra");

        string[] lines = console.ErrorText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(lines.Length, lines.Distinct().Count());
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_goes_to_standard_output(string option)
    {
        FakeConsole console = new();

        int exitCode = Run(console, option);

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.All(["--file", "--mode", "--count", "[default: fast]"], text => Assert.Contains(text, console.OutText));
        Assert.Empty(console.ErrorText);
    }

    [Fact]
    public void The_version_goes_to_standard_output()
    {
        FakeConsole console = new();

        int exitCode = Run(console, "--version");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.NotEmpty(console.OutText.Trim());
        Assert.Empty(console.ErrorText);
    }
}
