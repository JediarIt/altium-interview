using System.Runtime.CompilerServices;

using FileSort.Engine.Lines;

namespace FileSort.Engine.Merging;

/// <summary>
/// The order of lines, compared through the heads of a merge: the order of <see cref="EntryComparer"/>, decided
/// without reading a line wherever the 24 bytes of String and the Number of a head are enough.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>The three parts of the key, each compared as a number. A String shorter than 24 bytes is padded with zero
/// bytes, and a String that is the beginning of another comes first, so wherever two padded keys differ, they differ
/// the way the Strings do.</item>
/// <item>Equal keys of Strings of the same length, 24 bytes or less, are equal Strings: the Numbers decide, as
/// numbers, when both fit in 64 bits and differ.</item>
/// <item>Everything else goes to the comparison of the entries, which reads the lines: longer Strings that agree in
/// 24 bytes, Numbers above 64 bits, equal Numbers, where the bytes of the lines decide.</item>
/// </list>
/// </remarks>
/// <param name="lines">The buffer that the offsets of the entries point into.</param>
internal readonly struct MergeHeadComparer(byte[] lines) : ILineOrder<MergeHead>
{
    private readonly EntryComparer _entries = new(lines);

    /// <summary>Negative when <paramref name="x"/> comes first, positive when <paramref name="y"/> does, 0 for
    /// identical lines.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(in MergeHead x, in MergeHead y)
    {
        if (x.Entry.Prefix != y.Entry.Prefix)
        {
            return x.Entry.Prefix < y.Entry.Prefix ? -1 : 1;
        }

        if (x.Second != y.Second)
        {
            return x.Second < y.Second ? -1 : 1;
        }

        if (x.Third != y.Third)
        {
            return x.Third < y.Third ? -1 : 1;
        }

        return CompareEqualKeys(in x, in y);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int CompareEqualKeys(in MergeHead x, in MergeHead y)
    {
        bool equalStrings = x.StringLength == y.StringLength && x.StringLength <= MergeHead.KeyLength;
        if (equalStrings && x.NumberFits && y.NumberFits && x.Number != y.Number)
        {
            return x.Number < y.Number ? -1 : 1;
        }

        IndexEntry xEntry = x.Entry;
        IndexEntry yEntry = y.Entry;
        return _entries.Compare(in xEntry, in yEntry);
    }
}
