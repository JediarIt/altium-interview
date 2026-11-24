using System.Runtime.CompilerServices;
using System.Text.Unicode;

using FileSort.Common;

namespace FileSort.Engine.Lines.Parsing;

/// <summary>
/// Reads one line, given without its terminator, into its index entry, and checks it against the line format: one
/// or more ASCII digits, the separator <c>. </c>, and a String of well-formed UTF-8. The Number stays its digits: the
/// comparison reads them when two Strings are equal.
/// </summary>
internal static class LineParser
{
    /// <summary>
    /// Checks the line and, when it is valid, gives its entry. For <c>415. Apple</c> at offset 100 the entry holds the
    /// prefix of <c>Apple</c>, the offset 100 and the shape of a 10-byte line with a Number of 3 digits.
    /// </summary>
    public static LineFault Parse(ReadOnlySpan<byte> line, uint offset, out IndexEntry entry)
    {
        entry = default;
        if (line.IsEmpty)
        {
            return LineFault.Empty;
        }

        int digits = CountDigits(line);
        if (digits == 0)
        {
            return LineFault.NoNumber;
        }

        // Digits contain no period, so the separator right after them is the first ". " of the line: the String may
        // contain more of them.
        if (!line[digits..].StartsWith(LineFormat.Separator))
        {
            return LineFault.NoSeparator;
        }

        ReadOnlySpan<byte> text = line[(digits + LineFormat.SeparatorLength)..];
        if (!Utf8.IsValid(text))
        {
            return LineFault.IllFormedUtf8;
        }

        entry = Entry(line.Length, digits, text, offset);
        return LineFault.None;
    }

    /// <summary>
    /// The entry of a line that was found valid before, when its chunk was made: the merge reads such lines again
    /// from the runs and skips the checks, the check of UTF-8 above all, half the cost of parsing.
    /// </summary>
    public static IndexEntry ParseValid(ReadOnlySpan<byte> line, uint offset)
    {
        int digits = CountDigits(line);
        return Entry(line.Length, digits, line[(digits + LineFormat.SeparatorLength)..], offset);
    }

    /// <summary>The count of digits at the start of a line, by a vectorized search: 3 for <c>415. Apple</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDigits(ReadOnlySpan<byte> line)
    {
        int digits = line.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
        return digits < 0 ? line.Length : digits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IndexEntry Entry(int lineLength, int digits, ReadOnlySpan<byte> text, uint offset)
    {
        return new IndexEntry(StringPrefix.Read(text), offset, EntryShape.Pack(lineLength, digits));
    }
}
