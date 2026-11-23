namespace FileSort.Checker.Checks;

/// <summary>
/// How the files are read and checked. The number of threads is an option of the command line. The other two are
/// settings of the application with no option: the defaults are chosen by measurement, and tests lower them to
/// reach the limits with small files.
/// </summary>
internal sealed record ScanSettings
{
    /// <summary>
    /// The largest number of threads that <c>--threads</c> accepts. Every chunk handed to the workers holds a buffer of
    /// its own, so the threads bound the memory: 64 keep the peak working set near 100 MiB even when the chunks queue
    /// up on two cores, and 64 threads check far faster than any disk reads.
    /// </summary>
    public const int MaxThreads = 64;

    /// <summary>
    /// The size of a chunk in bytes: a file is read in parts of this size, each cut at the end of a line and
    /// checked on its own. A line longer than a chunk gets a chunk as large as the line.
    /// </summary>
    public int ChunkSize { get; init; } = 1024 * 1024;

    /// <summary>
    /// The longest line that is read, in bytes without its terminator. A longer line ends the check: this is a
    /// limit of the tool, not of the format, and it protects memory when a file is read with the wrong line ending.
    /// </summary>
    public int MaxLineLength { get; init; } = 64 * 1024 * 1024;

    /// <summary>
    /// The largest number of chunks, of both files together, that are checked at the same time. It does not change
    /// what the check finds.
    /// </summary>
    public int Threads { get; init; } = DefaultThreads;

    /// <summary>The default of <c>--threads</c>: every logical processor, up to the largest accepted value.</summary>
    public static int DefaultThreads => Math.Min(Environment.ProcessorCount, MaxThreads);

    public static ScanSettings Default { get; } = new();
}
