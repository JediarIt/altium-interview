using FileSort.Common;

namespace FileSort.Generator.Content;

/// <summary>
/// Everything the bytes of a generated file depend on, besides its size and the <see cref="ContentFormat.Version"/>.
/// </summary>
internal sealed record ContentOptions
{
    public const int DefaultDuplicatesPercent = 30;

    public required ulong Seed { get; init; }

    /// <summary>The probability, in percent, that a line takes its String from the pool of repeated Strings.</summary>
    public int DuplicatesPercent
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
            field = value;
        }
    } = DefaultDuplicatesPercent;

    public WordRange Words { get; init; } = WordRange.Default;

    public EdgeCases EdgeCases { get; init; } = EdgeCases.None;

    public LineEnding LineEnding { get; init; } = LineEnding.Lf;
}
