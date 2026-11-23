namespace FileSort.Checker.Lines;

/// <summary>
/// The order of lines as the checker understands it: by String, then by Number. Each result is negative when the
/// first argument comes first, positive when the second does, and zero when the two are equal.
/// </summary>
internal static class LineComparer
{
    /// <summary>
    /// Compares two Strings byte by byte, as unsigned bytes: the String with the smaller byte at the first
    /// difference comes first, and a String that is the beginning of a longer one comes first.
    /// </summary>
    public static int CompareStrings(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        return x.SequenceCompareTo(y);
    }

    /// <summary>
    /// Compares two Numbers by value, as digits, without converting them, so their length has no limit. Leading
    /// zeros are dropped; then the longer run of digits is the greater value, and runs of equal length compare
    /// digit by digit. <c>007</c> and <c>7</c> are equal, <c>9</c> comes before <c>10</c>, and <c>0</c> and
    /// <c>000</c> both become an empty run and are equal.
    /// </summary>
    public static int CompareNumbers(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        x = x.TrimStart((byte)'0');
        y = y.TrimStart((byte)'0');
        return x.Length != y.Length ? x.Length.CompareTo(y.Length) : x.SequenceCompareTo(y);
    }

    /// <summary>
    /// Compares two valid lines, each given with the length of its Number: by String first, and by Number when the
    /// Strings are equal.
    /// </summary>
    public static int Compare(ReadOnlySpan<byte> x, int xNumberLength, ReadOnlySpan<byte> y, int yNumberLength)
    {
        int strings = CompareStrings(LineParser.Text(x, xNumberLength), LineParser.Text(y, yNumberLength));
        return strings != 0
            ? strings
            : CompareNumbers(LineParser.Number(x, xNumberLength), LineParser.Number(y, yNumberLength));
    }
}
