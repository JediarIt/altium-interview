namespace FileSort.Engine.Chunks.Sorting;

/// <summary>
/// A part of the index of a chunk that is sorted on its own: <paramref name="Length"/> entries from
/// <paramref name="Start"/>, counted from the first entry of the chunk.
/// </summary>
internal readonly record struct Segment(int Start, int Length);
