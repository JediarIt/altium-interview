using System.Diagnostics;
using System.Globalization;

using FileSort.Cli.Reports;
using FileSort.Engine;
using FileSort.Sorter.Cli.Options;

namespace FileSort.Sorter.Cli.Reports;

/// <summary>
/// The summary printed to standard output after a successful sort, as <c>Name: value</c> lines that a script can
/// read; standard output gets nothing else.
/// </summary>
internal static class SortSummary
{
    private const double Mebibyte = 1024 * 1024;

    public static void Write(TextWriter writer, string outputPath, long inputBytes, SortResult result, TimeSpan elapsed)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        double seconds = Math.Max(elapsed.TotalSeconds, 0.001);
        using Process process = Process.GetCurrentProcess();

        writer.WriteLine($"Output:           {outputPath}");
        writer.WriteLine(string.Create(invariant, $"Lines:            {result.Lines}"));
        writer.WriteLine($"Size:             {ByteSize.Describe(result.Bytes)}");
        writer.WriteLine($"Line ending:      {LineEndingNames.Format(result.LineEnding)}");
        writer.WriteLine($"Byte order mark:  {(result.ByteOrderMark ? "yes, in the input; not written" : "no")}");
        writer.WriteLine(string.Create(invariant, $"Runs:             {result.Runs}"));
        writer.WriteLine(string.Create(invariant, $"Merge passes:     {result.MergePasses}"));
        writer.WriteLine(string.Create(invariant, $"Chunks:           {result.ChunksTime.TotalSeconds:F2} s"));
        writer.WriteLine(string.Create(invariant, $"Merge:            {result.MergeTime.TotalSeconds:F2} s"));
        writer.WriteLine(string.Create(invariant, $"Elapsed:          {elapsed.TotalSeconds:F2} s"));
        writer.WriteLine(string.Create(invariant, $"Throughput:       {inputBytes / Mebibyte / seconds:F0} MiB/s"));
        writer.WriteLine(string.Create(invariant, $"Peak working set: {process.PeakWorkingSet64 / Mebibyte:F0} MiB"));
    }
}
