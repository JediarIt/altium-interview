using System.Runtime.InteropServices;

using FileSort.Engine.Lines;

namespace FileSort.Engine.Chunks.Sorting;

/// <summary>
/// Sorts the index of a chunk the way D7 of the design describes: the entries of every range are sorted by one
/// thread, in segments of a bounded size, and the cancellation is looked at between segments. A sort routine cannot
/// be stopped in the middle, so the size of a segment bounds how long a Ctrl+C waits.
/// </summary>
internal static class SegmentSorter
{
    /// <summary>
    /// The segments of a slice of the index: <paramref name="count"/> entries from <paramref name="sliceStart"/>, cut
    /// every <paramref name="segmentSize"/> entries. A size that is a whole number of cache lines of entries keeps
    /// the start of every segment on a cache line when the slice starts on one.
    /// </summary>
    public static IEnumerable<Segment> Plan(int sliceStart, int count, int segmentSize)
    {
        for (int start = 0; start < count; start += segmentSize)
        {
            yield return new Segment(sliceStart + start, Math.Min(segmentSize, count - start));
        }
    }

    /// <summary>Sorts the segments of one slice, one after another, on the calling thread.</summary>
    public static void Sort(
        ChunkBuffer buffer, int entriesOffset, IReadOnlyList<Segment> segments, CancellationToken cancellationToken)
    {
        ComparableEntry.UseLines(buffer.Bytes);
        try
        {
            foreach (Segment segment in segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int offset = entriesOffset + (segment.Start * ChunkBuffer.IndexEntrySize);
                Span<IndexEntry> entries = buffer.Entries(offset, segment.Length);
                MemoryMarshal.Cast<IndexEntry, ComparableEntry>(entries).Sort();
            }
        }
        finally
        {
            ComparableEntry.UseLines(null);
        }
    }
}
