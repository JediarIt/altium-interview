using FileSort.Checker.Checks.Chunks;
using FileSort.Checker.Lines;

namespace FileSort.Tests.Checker.Lines;

/// <summary>
/// Everything the checker's reading gives for a file: its lines up to the end, or up to a bare line feed or a line
/// that is too long, and the mark. The file is read as chunks and every chunk is split into lines, the way a scan
/// does it; numbers and offsets are those of the file.
/// </summary>
internal sealed record ReadFile(List<ReadLine> Lines, bool HasByteOrderMark, int Chunks)
{
    public static ReadFile From(byte[] file, LineEnding lineEnding, int chunkSize = 4096, int maxLineLength = 1 << 20)
    {
        using MemoryStream stream = new(file, writable: false);
        return From(stream, lineEnding, chunkSize, maxLineLength);
    }

    public static ReadFile From(Stream stream, LineEnding lineEnding, int chunkSize = 4096, int maxLineLength = 1 << 20)
    {
        ChunkReader reader = new(stream, lineEnding, chunkSize, maxLineLength);
        List<ReadLine> lines = [];
        int chunks = 0;
        bool more = true;
        while (more && reader.Read() is Chunk chunk)
        {
            chunks++;
            more = Split(chunk, lineEnding, maxLineLength, lines);
            ChunkReader.Release(chunk);
        }

        return new ReadFile(lines, reader.HasByteOrderMark, chunks);
    }

    // Splits a chunk into lines. Returns false where reading cannot go on.
    private static bool Split(Chunk chunk, LineEnding lineEnding, int maxLineLength, List<ReadLine> lines)
    {
        int linesBefore = lines.Count;
        if (chunk.IsLineWithoutEnd)
        {
            lines.Add(new ReadLine(LineRead.TooLong, [], linesBefore + 1, chunk.Offset, false));
            return false;
        }

        LineSplitter splitter = new(chunk.Bytes, lineEnding, chunk.EndsFile, maxLineLength);
        while (true)
        {
            LineRead read = splitter.TryRead(out ReadOnlySpan<byte> line);
            if (read == LineRead.None)
            {
                return true;
            }

            lines.Add(new ReadLine(
                read, line.ToArray(), linesBefore + splitter.LineNumber, chunk.Offset + splitter.LineOffset, splitter.LineHasTerminator));
            if (read != LineRead.Line)
            {
                return false;
            }
        }
    }
}
