using System.Globalization;

namespace FileSort.Generator.Content;

/// <summary>How many words a String has: a whole number from <see cref="Min"/> to <see cref="Max"/>.</summary>
internal readonly record struct WordRange
{
    /// <summary>The largest number of words that a String can have.</summary>
    public const int Limit = 1024;

    public static readonly WordRange Default = new(1, 5);

    public WordRange(int min, int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(min, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(max, Limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(min, max);
        Min = min;
        Max = max;
    }

    public int Min { get; }

    public int Max { get; }

    /// <summary>The form that <c>--words</c> accepts: <c>4</c> or <c>1-5</c>.</summary>
    public override string ToString()
    {
        return Min == Max
            ? Min.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{Min}-{Max}");
    }
}
