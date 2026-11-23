using System.Globalization;
using System.Numerics;
using System.Text;

namespace FileSort.Tests.Format;

/// <summary>
/// A deliberately simple reader of the <c>Number. String</c> format, used as the oracle for what the production code
/// writes. It shares no code with <c>FileSort.Common</c>: a writer and a parser built on one misunderstanding of the
/// format would agree with each other.
/// </summary>
internal static class NaiveLineParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Splits a file into lines at the given terminator, <c>"\n"</c> or <c>"\r\n"</c>. A byte order mark at the start
    /// is skipped; a last line without a terminator is a line.
    /// </summary>
    public static List<byte[]> SplitLines(byte[] file, string terminator)
    {
        byte[] end = Encoding.ASCII.GetBytes(terminator);
        int start = file.Length >= 3 && file[0] == 0xEF && file[1] == 0xBB && file[2] == 0xBF ? 3 : 0;

        List<byte[]> lines = [];
        while (start < file.Length)
        {
            int found = file.AsSpan(start).IndexOf(end);
            if (found < 0)
            {
                lines.Add(file[start..]);
                break;
            }

            lines.Add(file[start..(start + found)]);
            start += found + end.Length;
        }

        return lines;
    }

    /// <summary>Parses one line without its terminator. Returns null when the line is not valid.</summary>
    public static NaiveLine? ParseLine(byte[] line)
    {
        int separator = line.AsSpan().IndexOf(". "u8);
        if (separator < 1)
        {
            return null;
        }

        string numberText = Encoding.ASCII.GetString(line, 0, separator);
        if (!numberText.All(c => c is >= '0' and <= '9'))
        {
            return null;
        }

        // A Number has no limit on its digits or its value, so the value is a BigInteger.
        BigInteger number = BigInteger.Parse(numberText, CultureInfo.InvariantCulture);

        byte[] text = line[(separator + 2)..];
        if (text.Contains((byte)'\n'))
        {
            return null;
        }

        try
        {
            _ = StrictUtf8.GetString(text);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        return new NaiveLine(number, numberText, text);
    }

    /// <summary>Parses a whole file; an invalid line is a null entry.</summary>
    public static List<NaiveLine?> ParseFile(byte[] file, string terminator)
    {
        return SplitLines(file, terminator).ConvertAll(ParseLine);
    }

    /// <summary>Parses a file that must be valid, and fails the test with the first invalid line otherwise.</summary>
    public static List<NaiveLine> ParseValidFile(byte[] file, string terminator)
    {
        List<byte[]> lines = SplitLines(file, terminator);
        List<NaiveLine> parsed = new(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            NaiveLine? line = ParseLine(lines[i]);
            Assert.True(line is not null, $"Line {i + 1} is not valid: {Encoding.UTF8.GetString(lines[i])}");
            parsed.Add(line!);
        }

        return parsed;
    }
}
