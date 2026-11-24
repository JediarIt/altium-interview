using System.Globalization;

using FileSort.Cli.Reports;
using FileSort.Sorter.Cli.Options;

namespace FileSort.Sorter.Cli.Reports;

/// <summary>
/// The settings a sort works with, printed to the error stream before the input is read: both paths in full, the
/// temporary directory, the memory, the threads, the line ending option and whether <c>--overwrite</c> was given,
/// whether they were given or not. A sort that is cancelled or fails has no summary, so this is where its settings
/// can be read.
/// </summary>
internal static class SortSettingsReport
{
    public static void Write(
        TextWriter writer, SorterSettings settings, string inputPath, string outputPath, string tempDirectory)
    {
        writer.WriteLine($"Sorting:          {inputPath}");
        writer.WriteLine($"Output:           {outputPath}");
        writer.WriteLine($"Temporary folder: in {tempDirectory}");
        writer.WriteLine($"Memory:           {ByteSize.Describe(settings.Memory)}");
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Threads:          {settings.Threads}"));
        writer.WriteLine($"Line ending:      {settings.LineEndingName}");
        writer.WriteLine($"Overwrite:        {(settings.Overwrite ? "yes" : "no")}");
        writer.WriteLine();
    }
}
