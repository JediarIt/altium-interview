namespace FileSort.Generator.Cli;

/// <summary>The exit codes of the generator.</summary>
internal static class ExitCode
{
    public const int Success = 0;

    /// <summary>No file was produced: a runtime failure or a declined overwrite.</summary>
    public const int Failure = 1;

    public const int InvalidArguments = 2;

    /// <summary>Cancelled with Ctrl+C or a termination signal: 128 plus the number of SIGINT.</summary>
    public const int Cancelled = 130;
}
