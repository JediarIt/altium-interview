using System.Runtime.CompilerServices;

using FileSort.Engine.Lines;

namespace FileSort.Benchmarks.Micro.Sorter.Candidates;

/// <summary>
/// The order of the engine for entries of 24 bytes: the same steps as <see cref="EntryComparer"/>, except that two
/// Numbers that both fit in 64 bits compare as integers from the entries, and a Number that fits comes before one that
/// does not.
/// </summary>
internal readonly struct WideEntryComparer(byte[] lines) : IComparer<WideEntry>
{
    private readonly byte[] _lines = lines;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(WideEntry x, WideEntry y)
    {
        if (x.Prefix != y.Prefix)
        {
            return x.Prefix < y.Prefix ? -1 : 1;
        }

        return CompareAfterPrefixes(x, y);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int CompareAfterPrefixes(WideEntry x, WideEntry y)
    {
        ReadOnlySpan<byte> xLine = _lines.AsSpan((int)x.Offset, EntryShape.LineLength(x.Shape));
        ReadOnlySpan<byte> yLine = _lines.AsSpan((int)y.Offset, EntryShape.LineLength(y.Shape));
        int xDigits = EntryShape.StoredNumberLength(x.Shape);
        int yDigits = EntryShape.StoredNumberLength(y.Shape);
        ReadOnlySpan<byte> xText = xLine[(xDigits + 2)..];
        ReadOnlySpan<byte> yText = yLine[(yDigits + 2)..];

        int strings = xText.Length <= StringPrefix.Length || yText.Length <= StringPrefix.Length
            ? xText.Length.CompareTo(yText.Length)
            : xText[StringPrefix.Length..].SequenceCompareTo(yText[StringPrefix.Length..]);
        if (strings != 0)
        {
            return strings;
        }

        int values = CompareValues(x, xLine[..xDigits], y, yLine[..yDigits]);
        return values != 0 ? values : xDigits == yDigits ? 0 : xLine[..xDigits].SequenceCompareTo(yLine[..yDigits]);
    }

    private static int CompareValues(WideEntry x, ReadOnlySpan<byte> xDigits, WideEntry y, ReadOnlySpan<byte> yDigits)
    {
        if (x.NumberFits && y.NumberFits)
        {
            return x.Number.CompareTo(y.Number);
        }

        if (x.NumberFits != y.NumberFits)
        {
            return x.NumberFits ? -1 : 1;
        }

        ReadOnlySpan<byte> xSignificant = xDigits.TrimStart((byte)'0');
        ReadOnlySpan<byte> ySignificant = yDigits.TrimStart((byte)'0');
        return xSignificant.Length != ySignificant.Length
            ? xSignificant.Length.CompareTo(ySignificant.Length)
            : xSignificant.SequenceCompareTo(ySignificant);
    }
}
