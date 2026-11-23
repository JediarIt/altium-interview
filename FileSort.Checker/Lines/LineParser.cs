using System.Text.Unicode;

namespace FileSort.Checker.Lines;

/// <summary>
/// The checker's own reading of one line, in the plainest way that is correct. It computes no value: the Number
/// stays the digits as they were written, and the String stays its bytes. It allocates nothing.
/// </summary>
internal static class LineParser
{
    /// <summary>The bytes between the Number and the String: a period and one space.</summary>
    public static ReadOnlySpan<byte> Separator => ". "u8;

    /// <summary>
    /// Checks a line, given without its terminator, and finds where its Number ends. For <c>415. Apple</c> the
    /// result is <see cref="LineFault.None"/> and <paramref name="numberLength"/> is 3.
    /// </summary>
    public static LineFault Parse(ReadOnlySpan<byte> line, out int numberLength)
    {
        numberLength = 0;
        if (line.IsEmpty)
        {
            return LineFault.Empty;
        }

        // The Number is the run of digits at the start. Digits contain no period, so the separator right after
        // them is the first ". " of the line: the String may contain more of them.
        int digits = line.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
        if (digits == 0)
        {
            return LineFault.NoNumber;
        }

        // A line of digits only has no separator: the search above found no other byte.
        if (digits < 0 || !line[digits..].StartsWith(Separator))
        {
            return LineFault.NoSeparator;
        }

        if (!Utf8.IsValid(line[(digits + Separator.Length)..]))
        {
            return LineFault.IllFormedString;
        }

        numberLength = digits;
        return LineFault.None;
    }

    /// <summary>The digits of the Number of a valid line whose Number has <paramref name="numberLength"/> digits.</summary>
    public static ReadOnlySpan<byte> Number(ReadOnlySpan<byte> line, int numberLength)
    {
        return line[..numberLength];
    }

    /// <summary>The String of a valid line whose Number has <paramref name="numberLength"/> digits.</summary>
    public static ReadOnlySpan<byte> Text(ReadOnlySpan<byte> line, int numberLength)
    {
        return line[(numberLength + Separator.Length)..];
    }
}
