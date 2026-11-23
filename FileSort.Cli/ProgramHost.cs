using FileSort.Cli.Terminal;

namespace FileSort.Cli;

/// <summary>What the entry point of every program does around its application.</summary>
public static class ProgramHost
{
    /// <summary>
    /// Runs the application on the console of the process, with Ctrl+C and the termination signals wired to its
    /// cancellation token.
    /// </summary>
    public static int Run(string[] args, ConsoleApp app)
    {
        using CancellationSignals signals = new();
        return Run(args, new SystemConsole(), app, signals.Token);
    }

    /// <summary>
    /// Runs the application. Anything unexpected still ends with a message and an exit code, never with a stack
    /// trace.
    /// </summary>
    public static int Run(string[] args, IAppConsole console, ConsoleApp app, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        try
        {
            return app(args, console, cancellationToken);
        }
#pragma warning disable CA1031 // The last line of defence of a program: whatever was thrown, the user gets one line.
        catch (Exception e)
#pragma warning restore CA1031
        {
            console.TryWriteError($"Unexpected error: {e.Message}");
            return ExitCode.Failure;
        }
    }
}
