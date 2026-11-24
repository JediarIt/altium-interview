namespace FileSort.Sorter.Cli.Options;

/// <summary>One run of the sorter, as the command line describes it.</summary>
internal sealed record SorterSettings
{
    /// <summary>The largest accepted value of <c>--threads</c>.</summary>
    public const int MaxThreads = 64;

    /// <summary>The smallest accepted value of <c>--memory</c>: 256 MB, of which the chunk buffer gets 96
    /// MiB.</summary>
    public const long MinMemory = 256L * 1024 * 1024;

    /// <summary>The default of <c>--memory</c> as it is typed: 1 GB, half of the memory of the task.</summary>
    public const string DefaultMemory = "1GB";

    public required string InputPath { get; init; }

    public required string OutputPath { get; init; }

    /// <summary>The directory of the temporary folder; null for the directory of the output.</summary>
    public string? TempDirectory { get; init; }

    /// <summary>The largest peak working set of the process, in bytes.</summary>
    public required long Memory { get; init; }

    public required int Threads { get; init; }

    /// <summary>The value of <c>--line-ending</c> as it was given or defaulted.</summary>
    public required string LineEndingName { get; init; }

    /// <summary>Replace an existing output without asking.</summary>
    public bool Overwrite { get; init; }

    /// <summary>The default of <c>--threads</c>: every logical processor, up to the largest accepted value.</summary>
    public static int DefaultThreads => Math.Min(Environment.ProcessorCount, MaxThreads);
}
