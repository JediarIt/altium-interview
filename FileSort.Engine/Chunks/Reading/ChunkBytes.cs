namespace FileSort.Engine.Chunks.Reading;

/// <summary>
/// The bytes of a chunk in the chunk buffer, from its start: whole lines, each ending with a line feed, except the
/// last line of a file without a terminator.
/// </summary>
/// <param name="FileOffset">Where the chunk starts in the input.</param>
/// <param name="Length">The bytes of the chunk.</param>
/// <param name="EndsFile">The chunk reaches the end of the input.</param>
/// <param name="LastLineUnterminated">The last line has no terminator, which only the last line of a file may
/// lack.</param>
internal readonly record struct ChunkBytes(long FileOffset, int Length, bool EndsFile, bool LastLineUnterminated);
