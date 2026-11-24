using FileSort.Engine.Lines;
using FileSort.Engine.Lines.Parsing;
using FileSort.Generator.Content;

namespace FileSort.Benchmarks.Micro.Sorter;

/// <summary>
/// Lines of the generator's default content in memory, with the line feed of every line, and their index entries as
/// the engine's parser makes them: what a chunk holds once its lines are parsed. The lines serve as the chunk buffer
/// that the entries point into.
/// </summary>
internal sealed class ParsedContent
{
    private ParsedContent(byte[] lines, IndexEntry[] entries)
    {
        Lines = lines;
        Entries = entries;
    }

    public byte[] Lines { get; }

    public IndexEntry[] Entries { get; }

    /// <summary>At least <paramref name="size"/> bytes of default content made with seed 1, and its entries.</summary>
    public static ParsedContent Create(long size)
    {
        byte[] lines = GeneratedContent.Create(size, EdgeCases.None);
        List<IndexEntry> entries = new((int)(size / 20));
        int start = 0;
        while (start < lines.Length)
        {
            int end = start + lines.AsSpan(start).IndexOf((byte)'\n');
            if (LineParser.Parse(lines.AsSpan(start, end - start), (uint)start, out IndexEntry entry) != LineFault.None)
            {
                throw new InvalidDataException($"The generated line at offset {start} is not valid.");
            }

            entries.Add(entry);
            start = end + 1;
        }

        return new ParsedContent(lines, [.. entries]);
    }
}
