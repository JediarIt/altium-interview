using FileSort.Cli;
using FileSort.Cli.Terminal;

namespace FileSort.Tests.Cli;

/// <summary>What the entry point adds around an application, without the console and the signals of the process.</summary>
public class ProgramHostTests
{
    [Fact]
    public void The_application_gets_the_arguments_the_console_and_the_token()
    {
        FakeConsole console = new();
        using CancellationTokenSource cancellation = new();
        string[] args = ["--option", "value"];

        int exitCode = ProgramHost.Run(
            args,
            console,
            (seenArgs, seenConsole, seenToken) =>
            {
                Assert.Same(args, seenArgs);
                Assert.Same(console, seenConsole);
                Assert.Equal(cancellation.Token, seenToken);
                return 42;
            },
            cancellation.Token);

        Assert.Equal(42, exitCode);
    }

    [Fact]
    public void The_entry_point_runs_the_application_on_the_console_of_the_process()
    {
        IAppConsole? console = null;
        bool cancelled = true;

        int exitCode = ProgramHost.Run(
            ["--option"],
            (args, seenConsole, token) =>
            {
                console = seenConsole;
                cancelled = token.IsCancellationRequested;
                return args.Length;
            });

        // The console is the real one, and the token is the one that Ctrl+C would cancel: nobody pressed it.
        Assert.Equal(1, exitCode);
        Assert.IsType<SystemConsole>(console);
        Assert.False(cancelled);
    }

    [Fact]
    public void An_unexpected_exception_ends_with_a_message_and_no_stack_trace()
    {
        FakeConsole console = new();

        int exitCode = ProgramHost.Run(
            [], console, (_, _, _) => throw new InvalidOperationException("Something broke."), CancellationToken.None);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("Unexpected error: Something broke.", console.ErrorText);
        Assert.DoesNotContain("   at ", console.ErrorText);
        Assert.Empty(console.OutText);
    }

    [Fact]
    public void An_unexpected_exception_with_a_broken_error_stream_still_ends_with_an_exit_code()
    {
        FakeConsole console = new() { OnErrorWrite = _ => throw new IOException("The stream is broken.") };

        int exitCode = ProgramHost.Run(
            [], console, (_, _, _) => throw new InvalidOperationException("Something broke."), CancellationToken.None);

        Assert.Equal(ExitCode.Failure, exitCode);
    }
}
