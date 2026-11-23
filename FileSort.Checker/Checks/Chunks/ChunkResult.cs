using FileSort.Checker.Checks.Order;
using FileSort.Checker.Checks.Scans;

namespace FileSort.Checker.Checks.Chunks;

/// <summary>
/// What the check of one chunk found. The chunk does not know where it lies in the file, so line numbers count
/// from 1 for the first line of the chunk and offsets from the first byte of the chunk; the scan of the file, which
/// takes the chunks in their order, turns both into positions in the file. Counts are of the valid lines read
/// before the check of the chunk ended.
/// </summary>
internal sealed record ChunkResult
{
    public required ChunkEnd End { get; init; }

    /// <summary>
    /// The line that ended the check, when it did not end with <see cref="ChunkEnd.Completed"/>: its number and
    /// offset within the chunk, and for a line that is not valid the reason and the start of the line.
    /// </summary>
    public InvalidLine? FaultyLine { get; init; }

    public LineDigest Digest { get; init; }

    /// <summary>The lines followed by a terminator.</summary>
    public long TerminatedLines { get; init; }

    /// <summary>The lines followed by a terminator whose last byte is a carriage return.</summary>
    public long CarriageReturnEndings { get; init; }

    /// <summary>The order of the lines within the chunk; null when the order is not checked.</summary>
    public ChunkOrder? Order { get; init; }
}
