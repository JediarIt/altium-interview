namespace FileSort.Cli.Terminal;

/// <summary>Writes that every program does the same way.</summary>
public static class AppConsoleExtensions
{
    /// <summary>
    /// Writes a message to the error stream and never fails: when that stream itself is broken there is nowhere to
    /// report to, and the exit code still tells.
    /// </summary>
    public static void TryWriteError(this IAppConsole console, string message)
    {
        ArgumentNullException.ThrowIfNull(console);
        try
        {
            console.Error.WriteLine(message);
        }
        catch (IOException)
        {
            // Nothing more can be said: the stream for saying it is the one that failed.
        }
    }
}
