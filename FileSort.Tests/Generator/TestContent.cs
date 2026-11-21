using FileSort.Common;
using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Generator.Pipeline;
using FileSort.Tests.Format;

namespace FileSort.Tests.Generator;

/// <summary>Generates content in memory and reads it back with the naive parser.</summary>
internal static class TestContent
{
    public const long Megabyte = 1024 * 1024;

    public static ContentOptions Options(
        ulong seed = 1,
        int duplicates = ContentOptions.DefaultDuplicatesPercent,
        string words = "1-5",
        EdgeCases edgeCases = EdgeCases.None,
        LineEnding lineEnding = LineEnding.Lf)
    {
        string[] bounds = words.Split('-');
        return new ContentOptions
        {
            Seed = seed,
            DuplicatesPercent = duplicates,
            Words = new WordRange(int.Parse(bounds[0]), int.Parse(bounds[^1])),
            EdgeCases = edgeCases,
            LineEnding = lineEnding,
        };
    }

    public static byte[] Generate(ContentOptions options, long size, int threads = 4)
    {
        MemorySink sink = new();
        GenerationPipeline.Run(new BlockGenerator(options), size, threads, sink, CancellationToken.None);
        return sink.ToArray();
    }

    /// <summary>The lines of generated content. Fails the test when a line is not valid.</summary>
    public static List<NaiveLine> Lines(ContentOptions options, long size)
    {
        return Parse(Generate(options, size), options.LineEnding);
    }

    public static List<NaiveLine> Parse(byte[] content, LineEnding lineEnding)
    {
        return NaiveLineParser.ParseValidFile(content, lineEnding == LineEnding.CrLf ? "\r\n" : "\n");
    }

    /// <summary>The share of lines whose String also occurs on another line.</summary>
    public static double RepeatedShare(List<NaiveLine> lines)
    {
        Dictionary<string, int> counts = [];
        foreach (NaiveLine line in lines)
        {
            string text = line.TextAsString;
            counts[text] = counts.GetValueOrDefault(text) + 1;
        }

        return lines.Count(line => counts[line.TextAsString] > 1) / (double)lines.Count;
    }
}
