using System.CommandLine;
using System.Diagnostics;
using System.Globalization;

using FileSort.Benchmarks.Sorting;
using FileSort.Checker.Checks;
using FileSort.Checker.Cli.Options;
using FileSort.Checker.Lines;

namespace FileSort.Benchmarks.Experiments.Checker;

/// <summary>
/// Writes a sorted copy of a file with a sort in memory, so that the checker can be measured on a pair of real files
/// before a sorter exists. The copy serves only to measure time: it is ordered by the checker's own comparison and
/// proves nothing about the checker.
/// </summary>
internal static class SortedCopyExperiment
{
    private const int WriteBufferSize = 1024 * 1024;

    public static Command CreateCommand()
    {
        Option<string> input = new("--input")
        {
            Description = "The file to sort: at most 2 GB, because it is read into memory whole.",
            HelpName = "path",
            Required = true,
        };
        Option<string> output = new("--output")
        {
            Description = "The sorted copy to write. An existing file is replaced.",
            HelpName = "path",
            Required = true,
        };

        Command command = new(
            "sorted-copy",
            "Writes a sorted copy of a file of up to 2 GB with a sort in memory, for the checker's measurements. It "
            + "writes as many bytes as the input has: mind the disk.")
        {
            input, output,
        };
        command.SetAction(result => Run(Path.GetFullPath(result.GetValue(input)!), Path.GetFullPath(result.GetValue(output)!)));
        return command;
    }

    private static void Run(string inputPath, string outputPath)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        byte[] file = File.ReadAllBytes(inputPath);
        LineEnding lineEnding = DetectLineEnding(file);
        LineEntry[] entries = InMemorySort.SortedIndex(file, lineEnding);

        using (FileStream output = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, WriteBufferSize))
        {
            InMemorySort.Write(output, file, entries, lineEnding);
        }

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{outputPath}: {entries.Length} lines, line ending {LineEndingNames.Format(lineEnding)}, "
            + $"{Measure.Size(new FileInfo(outputPath).Length)} written in {stopwatch.Elapsed.TotalSeconds:F1} s"));
    }

    // The way the checker finds it: from the first line feed of the file.
    private static LineEnding DetectLineEnding(byte[] file)
    {
        using MemoryStream stream = new(file, writable: false);
        return LineEndingDetector.Detect(stream, file.Length);
    }
}
