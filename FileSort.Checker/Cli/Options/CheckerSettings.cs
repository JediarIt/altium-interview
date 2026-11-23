using FileSort.Checker.Lines;

namespace FileSort.Checker.Cli.Options;

/// <summary>One run of the checker, as the command line describes it.</summary>
internal sealed record CheckerSettings
{
    /// <summary>The input of the sort.</summary>
    public required string InputPath { get; init; }

    /// <summary>The sorted file, the output of the sort.</summary>
    public required string SortedPath { get; init; }

    /// <summary>The value of <c>--line-ending</c> as it was given or defaulted.</summary>
    public required string LineEndingName { get; init; }

    /// <summary>The largest number of chunks that are checked at the same time.</summary>
    public required int Threads { get; init; }

    /// <summary>The line ending to read both files with; null to take it from the input.</summary>
    public LineEnding? LineEnding => LineEndingNames.Parse(LineEndingName);
}
