using FileSort.Cli.Terminal;

using Spectre.Console;

namespace FileSort.Sorter.Cli.Terminal;

/// <summary>Chooses how the progress of a sort is shown on a console.</summary>
internal static class ProgressViews
{
    /// <summary>
    /// Nothing when the error stream is redirected, so a log file gets no progress updates and Spectre.Console is not
    /// used at all; otherwise the bars of the two phases on the error stream, which Spectre.Console draws as the
    /// terminal allows.
    /// </summary>
    public static IProgressView For(IAppConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);
        if (console.IsErrorRedirected)
        {
            return new NoProgressView();
        }

        AnsiConsoleSettings settings = new() { Out = new AnsiConsoleOutput(console.Error) };
        return new BarsProgressView(AnsiConsole.Create(settings));
    }
}