namespace FileSort.Cli;

/// <summary>The exit codes of the console programs. A code means the same in every program.</summary>
public static class ExitCode
{
    /// <summary>The program did its work; for the checker, the output is correct.</summary>
    public const int Success = 0;

    /// <summary>
    /// The program could not do its work: a file that cannot be read or written, a declined overwrite, a line longer
    /// than supported.
    /// </summary>
    public const int Failure = 1;

    public const int InvalidArguments = 2;

    /// <summary>A file is not a valid <c>Number. String</c> file. Used by the checker.</summary>
    public const int InvalidFile = 3;

    /// <summary>
    /// The checker's negative answer: both files are valid and the output is not the sorted input. It is not
    /// <see cref="Failure"/>, so a script can tell "checked, and it is wrong" from "could not check".
    /// </summary>
    public const int NotCorrect = 4;

    /// <summary>Cancelled with Ctrl+C or a termination signal: 128 plus the number of SIGINT.</summary>
    public const int Cancelled = 130;
}
