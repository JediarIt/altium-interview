using System.Diagnostics;
using System.Globalization;

using FileSort.Cli.Reports;
using FileSort.Common;
using FileSort.Generator.Cli.Options;
using FileSort.Generator.Content;
using FileSort.Generator.Pipeline;

namespace FileSort.Generator.Cli.Reports;

/// <summary>The summary printed after a successful run, and the command line that reproduces the output.</summary>
internal static class RunSummary
{
    private const double Mebibyte = 1024 * 1024;

    public static void Write(
        TextWriter writer, GeneratorSettings settings, string outputPath, GenerationResult result, bool unbuffered, TimeSpan elapsed)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        double seconds = Math.Max(elapsed.TotalSeconds, 0.001);
        using Process process = Process.GetCurrentProcess();

        writer.WriteLine($"Output:           {outputPath}");
        writer.WriteLine($"Size:             {ByteSize.Describe(result.Bytes)}");
        writer.WriteLine(string.Create(invariant, $"Lines:            {result.Lines}"));
        writer.WriteLine(string.Create(invariant, $"Average line:     {result.Bytes / (double)result.Lines:F1} bytes"));
        writer.WriteLine(string.Create(invariant, $"Seed:             {settings.Content.Seed}"));
        writer.WriteLine(string.Create(invariant, $"Content version:  {ContentFormat.Version}"));
        writer.WriteLine(string.Create(invariant, $"Write mode:       {(unbuffered ? "unbuffered" : "buffered")}, {settings.Threads} threads"));
        writer.WriteLine(string.Create(invariant, $"Elapsed:          {elapsed.TotalSeconds:F2} s"));
        writer.WriteLine(string.Create(invariant, $"Throughput:       {result.Bytes / Mebibyte / seconds:F0} MiB/s"));
        writer.WriteLine(string.Create(invariant, $"Peak working set: {process.PeakWorkingSet64 / Mebibyte:F0} MiB"));
        writer.WriteLine($"Reproduce:        {ReproducingCommandLine(settings)}");
    }

    /// <summary>
    /// The command line that writes the same bytes again: the output path, the size, the seed and every option that
    /// affects the content, each with its value. Options that do not change the bytes are left out.
    /// </summary>
    public static string ReproducingCommandLine(GeneratorSettings settings)
    {
        ContentOptions content = settings.Content;
        string edgeCases = content.EdgeCases == EdgeCases.None
            ? ""
            : $" --edge-cases {EdgeCaseNames.Format(content.EdgeCases)}";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"FileSort.Generator --output {Quote(settings.OutputPath)} --size {settings.Size} --seed {content.Seed}"
            + $" --duplicates {content.DuplicatesPercent} --words {content.Words}{edgeCases}"
            + $" --line-ending {LineEndingNames.Format(content.LineEnding)}");
    }

    private static string Quote(string path)
    {
        return path.Contains(' ', StringComparison.Ordinal) ? $"\"{path}\"" : path;
    }
}
