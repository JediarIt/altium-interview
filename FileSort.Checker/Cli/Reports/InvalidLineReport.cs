using System.Globalization;

using FileSort.Checker.Checks;
using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Lines;

namespace FileSort.Checker.Cli.Reports;

/// <summary>
/// The report of a line that is not valid: the file, the number of the line, the offset of its first byte, the
/// reason and the start of the line. It also names the line ending that the file was read with, because a wrong
/// line ending is the usual cause, and says how to give another one when it was detected.
/// </summary>
internal static class InvalidLineReport
{
    /// <param name="writer">Where the report goes.</param>
    /// <param name="file">Which of the two files: <c>input</c> or <c>output</c>.</param>
    /// <param name="path">The path of that file.</param>
    /// <param name="line">The line that is not valid.</param>
    /// <param name="result">The check, for its line ending.</param>
    public static void Write(TextWriter writer, string file, string path, InvalidLine line, CheckResult result)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        string hint = result.LineEndingDetected ? "; give --line-ending if that is wrong" : "";

        writer.WriteLine($"Invalid line in the {file}: {path}");
        writer.WriteLine(string.Create(invariant, $"  Line:           {line.LineNumber}"));
        writer.WriteLine(string.Create(invariant, $"  Offset:         {line.Offset} bytes from the start of the file"));
        writer.WriteLine($"  Reason:         {Reason(line.Fault, result.LineEnding)}");
        writer.WriteLine($"  Content:        {LineExcerpt.Format(line.Line)}");
        writer.WriteLine($"  Line ending:    {LineEndingText.Describe(result)}{hint}");
    }

    private static string Reason(LineFault fault, LineEnding lineEnding)
    {
        return fault switch
        {
            LineFault.BareLineFeed =>
                "the line contains a line feed without a carriage return before it, which is not a line of a file "
                + $@"with the line ending {LineEndingText.Bytes(lineEnding)}",
            LineFault.Empty => "the line is empty",
            LineFault.NoNumber => "the line does not start with a digit",
            LineFault.NoSeparator => "the digits of the Number are not followed by a period and a space",
            LineFault.IllFormedString => "the String is not well-formed UTF-8",
            _ => throw new ArgumentOutOfRangeException(nameof(fault), fault, "A valid line has no report."),
        };
    }
}
