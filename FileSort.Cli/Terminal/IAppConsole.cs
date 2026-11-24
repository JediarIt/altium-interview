using System.Diagnostics.CodeAnalysis;

namespace FileSort.Cli.Terminal;

/// <summary>
/// The part of the console that the programs use. Tests replace it to drive a confirmation prompt and to read the
/// progress line, the reports and the errors.
/// </summary>
public interface IAppConsole
{
    /// <summary>Standard output: what a run reports as its result, and the help text.</summary>
    TextWriter Out { get; }

    /// <summary>Standard error: settings, errors, the progress line and a confirmation prompt.</summary>
    [SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "Named after Console.Error, which it stands for; the library has no callers in other "
            + "languages.")]
    TextWriter Error { get; }

    bool IsInputRedirected { get; }

    bool IsErrorRedirected { get; }

    /// <summary>Reads one line of the user's input; null when the input has ended.</summary>
    string? ReadLine();
}
