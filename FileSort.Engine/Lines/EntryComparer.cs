using System.Runtime.CompilerServices;

using FileSort.Common;
using FileSort.Engine.Lines.Parsing;

namespace FileSort.Engine.Lines;

/// <summary>
/// The order of lines, compared through their index entries: the order of the spec <c>sort-order</c>, by String and
/// then by the value of the Number, and for lines with equal keys the order of their bytes. Two entries compare
/// equal only when their lines are identical. The lines lie in one buffer, the one their offsets point into.
/// </summary>
/// <remarks>
/// The steps, each taken only when the one before finds the lines equal:
/// <list type="number">
/// <item>The prefixes, compared as numbers, which is the same as comparing the first 8 bytes of the Strings.</item>
/// <item>The rest of the Strings. When one String has 8 bytes or less, it is the beginning of the other one, so the
/// shorter String comes first: <c>AB</c> (2 bytes) before <c>AB</c> and a zero byte (3 bytes, U+0000 is valid
/// UTF-8), whose prefixes are equal.</item>
/// <item>The values of the Numbers, as digits, so that their length has no limit: without leading zeros, the longer
/// run of digits is the greater value, and runs of equal length compare digit by digit.</item>
/// <item>The bytes of the Numbers, like Strings: <c>0. Apple</c>, <c>00. Apple</c>, <c>007. Apple</c>,
/// <c>07. Apple</c>, <c>7. Apple</c>. Since the Strings are equal, that is the order of the bytes of the lines.</item>
/// </list>
/// </remarks>
internal readonly struct EntryComparer(byte[] lines) : IComparer<IndexEntry>
{
    private readonly byte[] _lines = lines;

    /// <summary>Negative when <paramref name="x"/> comes first, positive when <paramref name="y"/> does, 0 for
    /// identical lines.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(in IndexEntry x, in IndexEntry y)
    {
        // Most comparisons end here, without reading a line.
        if (x.Prefix != y.Prefix)
        {
            return x.Prefix < y.Prefix ? -1 : 1;
        }

        return CompareAfterPrefixes(in x, in y);
    }

    // For the sorts that take an IComparer: a struct comparer is called directly, and the call is inlined.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int IComparer<IndexEntry>.Compare(IndexEntry x, IndexEntry y)
    {
        return Compare(in x, in y);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int CompareAfterPrefixes(in IndexEntry x, in IndexEntry y)
    {
        ReadOnlySpan<byte> xLine = Line(in x);
        ReadOnlySpan<byte> yLine = Line(in y);
        int xDigits = NumberLength(in x, xLine);
        int yDigits = NumberLength(in y, yLine);

        ReadOnlySpan<byte> xString = xLine[(xDigits + LineFormat.SeparatorLength)..];
        ReadOnlySpan<byte> yString = yLine[(yDigits + LineFormat.SeparatorLength)..];
        int strings = CompareStrings(xString, yString);
        return strings != 0 ? strings : CompareNumbers(xLine[..xDigits], yLine[..yDigits]);
    }

    // Strings whose first 8 bytes are equal.
    private static int CompareStrings(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        return x.Length <= StringPrefix.Length || y.Length <= StringPrefix.Length
            ? x.Length.CompareTo(y.Length)
            : x[StringPrefix.Length..].SequenceCompareTo(y[StringPrefix.Length..]);
    }

    // The Numbers of lines whose Strings are equal: by value, then by bytes. Equal values with the same count of digits
    // have the same leading zeros, so the lines are identical.
    private static int CompareNumbers(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        ReadOnlySpan<byte> xSignificant = x.TrimStart((byte)'0');
        ReadOnlySpan<byte> ySignificant = y.TrimStart((byte)'0');
        int values = xSignificant.Length != ySignificant.Length
            ? xSignificant.Length.CompareTo(ySignificant.Length)
            : xSignificant.SequenceCompareTo(ySignificant);
        if (values != 0)
        {
            return values;
        }

        return x.Length == y.Length ? 0 : x.SequenceCompareTo(y);
    }

    private ReadOnlySpan<byte> Line(in IndexEntry entry)
    {
        return _lines.AsSpan((int)entry.Offset, entry.LineLength);
    }

    // The count of digits that the entry stores, or, for a Number of 1023 digits or more, the count read again.
    private static int NumberLength(in IndexEntry entry, ReadOnlySpan<byte> line)
    {
        int stored = EntryShape.StoredNumberLength(entry.Shape);
        return stored < EntryShape.SaturatedNumberLength ? stored : LineParser.CountDigits(line);
    }
}
