using FileSort.Checker.Checks.Chunks;
using FileSort.Checker.Lines;

namespace FileSort.Benchmarks.Sorting;

/// <summary>
/// A sort of a whole file in memory, for measurements only: it gives the checker's benchmarks and experiments a
/// sorted file to read before a sorter exists. It splits lines with the checker's splitter and orders an index of
/// them with the checker's own comparison, so a file made by it proves nothing about the checker: the tests take
/// their sorted files from a naive sort that shares no code with it.
/// </summary>
internal static class InMemorySort
{
    // No real line comes near it; the checker has its own, lower limit.
    private const int MaxLineLength = 1 << 30;

    /// <summary>The lines of a valid file of up to 2 GB, in sorted order.</summary>
    /// <exception cref="InvalidDataException">A line of the file is not valid.</exception>
    public static LineEntry[] SortedIndex(byte[] file, LineEnding lineEnding)
    {
        LineEntry[] entries = [.. Index(file, lineEnding)];
        entries.AsSpan().Sort((x, y) => LineComparer.Compare(
            file.AsSpan(x.Offset, x.Length), x.NumberLength, file.AsSpan(y.Offset, y.Length), y.NumberLength));
        return entries;
    }

    /// <summary>Writes the lines in the order of the index, every line followed by the terminator.</summary>
    public static void Write(Stream output, byte[] file, LineEntry[] entries, LineEnding lineEnding)
    {
        ReadOnlySpan<byte> terminator = lineEnding == LineEnding.CrLf ? "\r\n"u8 : "\n"u8;
        foreach (LineEntry entry in entries)
        {
            output.Write(file.AsSpan(entry.Offset, entry.Length));
            output.Write(terminator);
        }
    }

    /// <summary>The sorted file as bytes, for a benchmark that reads it from memory.</summary>
    public static byte[] Sort(byte[] file, LineEnding lineEnding)
    {
        using MemoryStream sorted = new(file.Length + 2);
        Write(sorted, file, SortedIndex(file, lineEnding), lineEnding);
        return sorted.ToArray();
    }

    // The whole file is in memory, so it is split as one part that reaches the end of the file. A byte order mark
    // at its start is not part of the first line.
    private static List<LineEntry> Index(byte[] file, LineEnding lineEnding)
    {
        int start = file.AsSpan().StartsWith(ChunkReader.ByteOrderMark) ? ChunkReader.ByteOrderMark.Length : 0;
        LineSplitter lines = new(file.AsSpan(start), lineEnding, endsFile: true, MaxLineLength);
        List<LineEntry> entries = [];
        while (true)
        {
            LineRead read = lines.TryRead(out ReadOnlySpan<byte> line);
            if (read == LineRead.None)
            {
                return entries;
            }

            int numberLength = 0;
            LineFault fault = read == LineRead.Line ? LineParser.Parse(line, out numberLength) : LineFault.BareLineFeed;
            if (fault != LineFault.None)
            {
                throw new InvalidDataException($"Line {lines.LineNumber} is not valid ({read}, {fault}).");
            }

            entries.Add(new LineEntry(start + lines.LineOffset, line.Length, numberLength));
        }
    }
}
