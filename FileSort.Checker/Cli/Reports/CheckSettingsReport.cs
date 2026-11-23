using System.Globalization;

namespace FileSort.Checker.Cli.Reports;

/// <summary>
/// The settings a check works with, printed before the files are read: both paths in full, the value of
/// <c>--line-ending</c> and the number of threads, whether they were given or not. A check that is cancelled or
/// fails has no summary, so this is where its settings can be read.
/// </summary>
internal static class CheckSettingsReport
{
    public static void Write(TextWriter writer, string inputPath, string sortedPath, string lineEnding, int threads)
    {
        writer.WriteLine($"Checking:         {sortedPath}");
        writer.WriteLine($"Against input:    {inputPath}");
        writer.WriteLine($"Line ending:      {lineEnding}");
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Threads:          {threads}"));
        writer.WriteLine();
    }
}
