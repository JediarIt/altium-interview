using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Generator.Pipeline;

namespace FileSort.Benchmarks;

/// <summary>Content of the generator in memory, for the benchmarks of the code that reads it.</summary>
internal static class GeneratedContent
{
    /// <summary>At least <paramref name="size"/> bytes of lines with the line ending <c>\n</c>, made with seed 1.</summary>
    public static byte[] Create(long size, EdgeCases edgeCases)
    {
        BlockGenerator generator = new(new ContentOptions { Seed = 1, EdgeCases = edgeCases });
        using MemorySink sink = new(size + generator.MaxLineLength);
        GenerationPipeline.Run(generator, size, Environment.ProcessorCount, sink, CancellationToken.None);
        return sink.ToArray();
    }
}
