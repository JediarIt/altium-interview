namespace FileSort.Checker.Checks.Scans;

/// <summary>The start of a line, kept for a report after the buffer it was read into has moved on.</summary>
internal sealed record LineSample(byte[] Start, int Length)
{
    /// <summary>How many bytes of a line a report shows.</summary>
    public const int MaxBytes = 200;

    /// <summary>The line is longer than what was kept of it.</summary>
    public bool IsCut => Start.Length < Length;

    public static LineSample Of(ReadOnlySpan<byte> line)
    {
        return new LineSample(line[..Math.Min(line.Length, MaxBytes)].ToArray(), line.Length);
    }
}
