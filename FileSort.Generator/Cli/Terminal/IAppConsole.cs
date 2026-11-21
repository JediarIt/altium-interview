namespace FileSort.Generator.Cli.Terminal;

/// <summary>
/// The part of the console that the generator uses. Tests replace it to drive the confirmation prompt and to read
/// the progress line, the summary and the errors.
/// </summary>
internal interface IAppConsole
{
    /// <summary>Standard output: the summary and the help text.</summary>
    TextWriter Out { get; }

    /// <summary>Standard error: errors, the progress line and the confirmation prompt.</summary>
    TextWriter Error { get; }

    bool IsInputRedirected { get; }

    bool IsErrorRedirected { get; }

    /// <summary>Reads one line of the user's input; null when the input has ended.</summary>
    string? ReadLine();
}
