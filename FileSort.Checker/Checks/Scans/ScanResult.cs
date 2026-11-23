using FileSort.Checker.Checks.Order;

namespace FileSort.Checker.Checks.Scans;

/// <summary>What the scan of one file found. Counts are of the valid lines read before the scan ended.</summary>
internal sealed record ScanResult
{
    public required ScanEnd End { get; init; }

    /// <summary>The line that is not valid, when the scan ended with <see cref="ScanEnd.InvalidLine"/>.</summary>
    public InvalidLine? InvalidLine { get; init; }

    /// <summary>What went wrong, when the scan ended with <see cref="ScanEnd.Failed"/>.</summary>
    public string? Failure { get; init; }

    public LineDigest Digest { get; init; }

    public bool HasByteOrderMark { get; init; }

    /// <summary>The lines followed by a terminator: all of them, or all but the last.</summary>
    public long TerminatedLines { get; init; }

    /// <summary>The lines followed by a terminator whose last byte is a carriage return.</summary>
    public long CarriageReturnEndings { get; init; }

    /// <summary>The order check; only the scan of the output makes one.</summary>
    public OrderResult Order { get; init; }

    /// <summary>The file has lines followed by a terminator, and every one of them ends with a carriage return.</summary>
    public bool EveryTerminatedLineEndsWithCarriageReturn => TerminatedLines > 0 && CarriageReturnEndings == TerminatedLines;
}
