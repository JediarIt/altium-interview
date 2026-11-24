using FileSort.Common;
using FileSort.Engine.Runs;

namespace FileSort.Engine;

/// <summary>What a sort works with (D17 of the design). The host checks the values it takes from a user.</summary>
public sealed record SortSettings
{
    /// <summary>The longest line that the sorter accepts, its terminator not counted: 1 MiB (D12 of the
    /// design).</summary>
    public const int DefaultMaxLineLength = 1024 * 1024;

    /// <summary>The window of the block buffers in flight, reused as the output buffer of the merge (D13).</summary>
    public const int DefaultWindowBytes = 32 * 1024 * 1024;

    public required string InputPath { get; init; }

    public required string OutputPath { get; init; }

    /// <summary>The directory in which the temporary folder is created; null for the directory of the output.</summary>
    public string? TempDirectory { get; init; }

    /// <summary>
    /// The bytes of memory that the engine's buffers may take: the chunk buffer and the window. The rest of the
    /// process, the runtime and its code, is the host's to keep apart.
    /// </summary>
    public required long MemoryBytes { get; init; }

    /// <summary>The bytes of the window, out of <see cref="MemoryBytes"/>.</summary>
    public int WindowBytes { get; init; } = DefaultWindowBytes;

    /// <summary>The most threads that parse, sort and merge lines at the same time.</summary>
    public required int Threads { get; init; }

    /// <summary>The line ending of the input and the output; null to take it from the input.</summary>
    public LineEnding? LineEnding { get; init; }

    /// <summary>The longest line accepted, its terminator not counted.</summary>
    public int MaxLineLength { get; init; } = DefaultMaxLineLength;

    /// <summary>Whether an existing output may be replaced: the user has agreed to it.</summary>
    public bool Overwrite { get; init; }

    /// <summary>The free space of the volume of a directory, or null when it cannot be asked.</summary>
    public Func<string, long?> FreeSpace { get; init; } = SpaceCheck.FreeSpaceOf;

    /// <summary>The most entries in a segment that is sorted on its own (D7, chosen by M1).</summary>
    internal int SegmentEntries { get; init; } = 1 << 20;

    /// <summary>The bytes of lines that a block of the block merge aims at (D8, chosen by M2).</summary>
    internal int BlockBytes { get; init; } = 1024 * 1024;

    /// <summary>The most bytes that one run gets for its buffer in the merge (D10).</summary>
    internal int LargestRunBuffer { get; init; } = 16 * 1024 * 1024;
}
