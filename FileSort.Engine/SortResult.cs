using FileSort.Common;

namespace FileSort.Engine;

/// <summary>What a sort that finished did (D17 of the design).</summary>
public sealed record SortResult
{
    /// <summary>The lines of the input, and of the output.</summary>
    public required long Lines { get; init; }

    /// <summary>The bytes of the output.</summary>
    public required long Bytes { get; init; }

    /// <summary>The line ending that the input was read with and the output was written with.</summary>
    public required LineEnding LineEnding { get; init; }

    /// <summary>Whether the input started with a byte order mark, which the output does not.</summary>
    public required bool ByteOrderMark { get; init; }

    /// <summary>The runs that the chunks were written as; 0 when the input fitted in one chunk.</summary>
    public required int Runs { get; init; }

    /// <summary>The merges that ran: every group of runs and the final merge; 0 when the input fitted in one
    /// chunk.</summary>
    public required int MergePasses { get; init; }

    /// <summary>The time of the first phase: reading, sorting and writing the chunks.</summary>
    public required TimeSpan ChunksTime { get; init; }

    /// <summary>The time of the second phase: the merges.</summary>
    public required TimeSpan MergeTime { get; init; }
}
