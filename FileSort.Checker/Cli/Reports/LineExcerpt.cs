using System.Buffers;
using System.Globalization;
using System.Text;

using FileSort.Checker.Checks.Scans;

namespace FileSort.Checker.Cli.Reports;

/// <summary>
/// A line as a report shows it: its first bytes as text on one line of the console, whatever the bytes are.
/// Bytes that would not show, or would move the cursor, are written as escapes: <c>\r</c>, <c>\n</c>, <c>\t</c>,
/// <c>\\</c> for a backslash, and <c>\xNN</c> for any other control byte, for the bytes of a character that takes
/// no room, such as a byte order mark, and for bytes that are not well-formed UTF-8.
/// </summary>
internal static class LineExcerpt
{
    public static string Format(LineSample line)
    {
        StringBuilder text = new();
        ReadOnlySpan<byte> bytes = line.Start;
        while (!bytes.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf8(bytes, out Rune character, out int length);
            if (status == OperationStatus.NeedMoreData && line.IsCut)
            {
                // A character cut by the excerpt, not by the line: it is left out instead of shown as broken.
                break;
            }

            Append(text, character, bytes[..length], wellFormed: status == OperationStatus.Done);
            bytes = bytes[length..];
        }

        return line.IsCut
            ? string.Create(CultureInfo.InvariantCulture, $"{text}... ({line.Length} bytes)")
            : text.ToString();
    }

    private static void Append(StringBuilder text, Rune character, ReadOnlySpan<byte> bytes, bool wellFormed)
    {
        string? escape = wellFormed ? character.Value switch
        {
            '\r' => @"\r",
            '\n' => @"\n",
            '\t' => @"\t",
            '\\' => @"\\",
            _ => null,
        }
        : null;

        if (escape is not null)
        {
            text.Append(escape);
        }
        else if (wellFormed && IsVisible(character))
        {
            text.Append(character.ToString());
        }
        else
        {
            AppendBytes(text, bytes);
        }
    }

    // Control characters and format characters, the byte order mark among them, show nothing on a console.
    private static bool IsVisible(Rune character)
    {
        return Rune.GetUnicodeCategory(character) is not (UnicodeCategory.Control or UnicodeCategory.Format
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator);
    }

    private static void AppendBytes(StringBuilder text, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            text.Append(CultureInfo.InvariantCulture, $@"\x{value:X2}");
        }
    }
}
