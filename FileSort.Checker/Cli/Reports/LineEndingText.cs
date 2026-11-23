using FileSort.Checker.Checks;
using FileSort.Checker.Cli.Options;
using FileSort.Checker.Lines;

namespace FileSort.Checker.Cli.Reports;

/// <summary>The line ending of a check as the reports name it, with where it came from.</summary>
internal static class LineEndingText
{
    /// <summary>For example <c>lf (from the input)</c> or <c>crlf (given)</c>.</summary>
    public static string Describe(CheckResult result)
    {
        string source = result.LineEndingDetected ? "from the input" : "given";
        return $"{LineEndingNames.Format(result.LineEnding)} ({source})";
    }

    /// <summary>The bytes of the line ending as they are written in text: <c>\n</c> or <c>\r\n</c>.</summary>
    public static string Bytes(LineEnding lineEnding)
    {
        return lineEnding == LineEnding.CrLf ? @"\r\n" : @"\n";
    }
}
