using FileSort.Common;
using FileSort.Engine;
using FileSort.Engine.Chunks;
using FileSort.Engine.Chunks.Parsing;
using FileSort.Engine.Chunks.Reading;
using FileSort.Tests.Engine.InMemory;

namespace FileSort.Tests.Engine.Chunks;

/// <summary>Makes the chunks of an input in memory, the way the sort does, for the tests of chunks and
/// blocks.</summary>
internal static class TestChunks
{
    /// <summary>A maker of the chunks of the input with the given chunk buffer.</summary>
    public static ChunkMaker Maker(
        byte[] input,
        ChunkBuffer buffer,
        int threads,
        LineEnding lineEnding = LineEnding.Lf,
        int maxLineLength = TestSort.MaxLineLength,
        int segmentEntries = 8,
        CancellationToken cancellationToken = default)
    {
        MemoryInput source = new(input);
        InputStart start = InputStart.Read(source, lineEnding, maxLineLength, buffer.Bytes);
        ChunkReader reader = new(
            source,
            buffer,
            start.FirstLineOffset,
            lineEnding,
            maxLineLength,
            IndexLayout.MaxAlignment(threads),
            new SortProgress());
        ParallelWork work = new(threads, cancellationToken);
        return new ChunkMaker(reader, buffer, work, lineEnding, maxLineLength, segmentEntries);
    }

    /// <summary>Where every chunk of the input starts and how long it is.</summary>
    public static List<(long Offset, int Length)> Cuts(byte[] input, int chunkBuffer, int threads, int maxLineLength)
    {
        ChunkMaker maker = Maker(input, new ChunkBuffer(chunkBuffer), threads, maxLineLength: maxLineLength);
        List<(long Offset, int Length)> cuts = [];
        while (maker.Next() is SortedChunk chunk)
        {
            cuts.Add((chunk.Bytes.FileOffset, chunk.Bytes.Length));
        }

        return cuts;
    }
}
