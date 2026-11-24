using System.Globalization;

using FileSort.Cli.Reports;
using FileSort.Generator.Cli.Options;
using FileSort.Generator.Content;

namespace FileSort.Generator.Cli.Reports;

/// <summary>
/// The settings a run works with, printed before generation starts: every option with its value, whether the user
/// gave it or not, and the seed that was chosen when none was given. A run that is cancelled or fails has no
/// summary, so this is where its settings can be read.
/// </summary>
internal static class RunSettings
{
    public static void Write(TextWriter writer, GeneratorSettings settings, string outputPath, bool unbuffered)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        ContentOptions content = settings.Content;
        string edgeCases = content.EdgeCases == EdgeCases.None ? "none" : EdgeCaseNames.Format(content.EdgeCases);

        writer.WriteLine($"Generating:       {outputPath}");
        writer.WriteLine($"Requested size:   {ByteSize.Describe(settings.Size)}");
        string chosen = settings.SeedIsRandom ? " (chosen at random)" : "";
        writer.WriteLine(string.Create(invariant, $"Seed:             {content.Seed}{chosen}"));
        writer.WriteLine(string.Create(invariant, $"Duplicates:       {content.DuplicatesPercent}%"));
        writer.WriteLine($"Words:            {content.Words}");
        writer.WriteLine($"Edge cases:       {edgeCases}");
        writer.WriteLine($"Line ending:      {LineEndingNames.Format(content.LineEnding)}");
        writer.WriteLine(string.Create(invariant, $"Threads:          {settings.Threads}"));
        writer.WriteLine($"Write mode:       {(unbuffered ? "unbuffered" : "buffered")}");
        writer.WriteLine($"Overwrite:        {(settings.Overwrite ? "yes" : "no")}");
        writer.WriteLine(string.Create(invariant, $"Content version:  {ContentFormat.Version}"));
        writer.WriteLine();
    }
}
