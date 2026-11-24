using System.Diagnostics;
using System.Globalization;

using FileSort.Checker.Checks;
using FileSort.Checker.Checks.Order;
using FileSort.Checker.Checks.Scans;
using FileSort.Cli.Reports;

namespace FileSort.Checker.Cli.Reports;

/// <summary>
/// The summary printed after both files were read to the end: the two files, the line ending, the two checks, each
/// on its own line, the measurements of the run and the result.
/// </summary>
internal static class CheckSummary
{
    private const double Mebibyte = 1024 * 1024;

    public static void Write(TextWriter writer, PairFiles files, CheckResult result, TimeSpan elapsed)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        double seconds = Math.Max(elapsed.TotalSeconds, 0.001);
        using Process process = Process.GetCurrentProcess();

        WriteFile(writer, "Input: ", files.InputPath, files.InputSize, result.Input);
        WriteFile(writer, "Output:", files.SortedPath, files.SortedSize, result.Output);
        writer.WriteLine($"Line ending:      {LineEndingText.Describe(result)}");
        writer.WriteLine($"Order:            {Order(result.Output.Order)}");
        writer.WriteLine($"Same lines:       {SameLines(result)}");
        if (!result.SameLines && result.LineEndingsSeemToDiffer)
        {
            writer.WriteLine(
                @"Note:             every line of the output ends with a carriage return, and not every line of the "
                + @"input does. The output seems to be written with the line ending \r\n, and the input with \n.");
        }

        string equalKeyPairs = Count(result.Output.Order.EqualKeyPairs, "pair");
        writer.WriteLine(string.Create(invariant, $"Equal keys, different bytes: {equalKeyPairs}"));
        writer.WriteLine(string.Create(invariant, $"Elapsed:          {elapsed.TotalSeconds:F2} s"));
        double throughput = files.TotalSize / Mebibyte / seconds;
        writer.WriteLine(string.Create(invariant, $"Throughput:       {throughput:F0} MiB/s"));
        writer.WriteLine(string.Create(invariant, $"Peak working set: {process.PeakWorkingSet64 / Mebibyte:F0} MiB"));
        writer.WriteLine($"Result:           {(result.Outcome == CheckOutcome.Correct ? "CORRECT" : "NOT CORRECT")}");
    }

    private static void WriteFile(TextWriter writer, string name, string path, long size, ScanResult scan)
    {
        writer.WriteLine($"{name}           {path}");
        writer.WriteLine($"  Size:           {ByteSize.Describe(size)}");
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  Lines:          {scan.Digest.Lines}"));
        writer.WriteLine($"  Byte order mark: {(scan.HasByteOrderMark ? "yes" : "no")}");
    }

    private static string Order(OrderResult order)
    {
        return order.Violations == 0
            ? "OK"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"FAILED, {Count(order.Violations, "violation")}, the first at line {order.FirstViolationLine}");
    }

    // What is known when the digests differ: the difference in the number of lines, or only that the lines differ.
    private static string SameLines(CheckResult result)
    {
        long difference = result.Output.Digest.Lines - result.Input.Digest.Lines;
        if (result.SameLines)
        {
            return "OK";
        }

        return difference switch
        {
            0 => "FAILED, the same number of lines, different lines",
            < 0 => $"FAILED, the output has {Count(-difference, "line")} fewer",
            _ => $"FAILED, the output has {Count(difference, "line")} more",
        };
    }

    // "1 line", "2 lines".
    private static string Count(long count, string noun)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? "" : "s")}");
    }
}
