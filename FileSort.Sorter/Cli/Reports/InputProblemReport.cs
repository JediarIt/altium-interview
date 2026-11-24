using FileSort.Common;
using FileSort.Engine.Lines.Parsing;
using FileSort.Sorter.Cli.Options;

using static System.FormattableString;

namespace FileSort.Sorter.Cli.Reports;

/// <summary>
/// The messages of an input that the sorter does not accept: a line that is not valid, and a line that is too long.
/// Both name the input, the number of the line, its offset and the line ending, because a wrong line ending is the
/// usual cause of both, and say how to give another one when it was detected.
/// </summary>
internal static class InputProblemReport
{
    public static string InvalidLine(string inputPath, InvalidLineException e, bool lineEndingDetected)
    {
        ArgumentNullException.ThrowIfNull(e);
        return string.Join(
            Environment.NewLine,
            $"Invalid line in the input: {inputPath}",
            Invariant($"  Line:           {e.LineNumber}"),
            Invariant($"  Offset:         {e.Offset} bytes from the start of the file"),
            $"  Reason:         {Reason(e.Fault, e.LineEnding)}",
            $"  Line ending:    {DescribeLineEnding(e.LineEnding, lineEndingDetected)}",
            "No output was written.");
    }

    public static string LineTooLong(string inputPath, LineTooLongException e, bool lineEndingDetected)
    {
        ArgumentNullException.ThrowIfNull(e);
        return string.Join(
            Environment.NewLine,
            Invariant($"A line of the input is longer than {e.Limit} bytes: {inputPath}"),
            Invariant($"  Line:           {e.LineNumber}"),
            Invariant($"  Offset:         {e.Offset} bytes from the start of the file"),
            $"  Line ending:    {DescribeLineEnding(e.LineEnding, lineEndingDetected)}; a file read with the wrong "
            + "line ending can look like one long line",
            "No output was written.");
    }

    private static string Reason(LineFault fault, LineEnding lineEnding)
    {
        return fault switch
        {
            LineFault.Empty => "the line is empty",
            LineFault.NoNumber => "the line does not start with a digit",
            LineFault.NoSeparator => "the digits of the Number are not followed by a period and a space",
            LineFault.IllFormedUtf8 => "the String is not well-formed UTF-8",
            LineFault.BareLineFeed =>
                "the line contains a line feed without a carriage return before it, which is not a line of a file "
                + $"with the line ending {Bytes(lineEnding)}",
            _ => throw new ArgumentOutOfRangeException(nameof(fault), fault, "A valid line has no report."),
        };
    }

    // For example "lf (\n), from the input; give --line-ending if that is wrong".
    private static string DescribeLineEnding(LineEnding lineEnding, bool detected)
    {
        string name = LineEndingNames.Format(lineEnding);
        return detected
            ? $"{name} ({Bytes(lineEnding)}), from the input; give --line-ending if that is wrong"
            : $"{name} ({Bytes(lineEnding)}), given";
    }

    private static string Bytes(LineEnding lineEnding)
    {
        return lineEnding == LineEnding.CrLf ? @"\r\n" : @"\n";
    }
}
