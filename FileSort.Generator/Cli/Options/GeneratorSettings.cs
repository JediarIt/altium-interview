using FileSort.Generator.Content;

namespace FileSort.Generator.Cli.Options;

/// <summary>One run of the generator, as the command line describes it.</summary>
internal sealed record GeneratorSettings
{
    public const int MaxThreads = 256;

    public required string OutputPath { get; init; }

    /// <summary>The requested size in bytes.</summary>
    public required long Size { get; init; }

    public required ContentOptions Content { get; init; }

    /// <summary>The seed was chosen by the generator, because the command line gave none.</summary>
    public bool SeedIsRandom { get; init; }

    /// <summary>The upper limit on blocks generated at the same time.</summary>
    public required int Threads { get; init; }

    /// <summary>Replace an existing output file without asking.</summary>
    public bool Overwrite { get; init; }

    /// <summary>Write through the file cache of the operating system.</summary>
    public bool BufferedIo { get; init; }

    /// <summary>The default of <c>--threads</c>: every logical processor, up to the largest accepted value.</summary>
    public static int DefaultThreads => Math.Min(Environment.ProcessorCount, MaxThreads);
}
