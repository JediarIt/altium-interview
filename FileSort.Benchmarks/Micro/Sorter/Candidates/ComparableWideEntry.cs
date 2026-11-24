namespace FileSort.Benchmarks.Micro.Sorter.Candidates;

/// <summary>
/// An entry of 24 bytes that compares itself, so that it is sorted by the same routine as the engine's entries: the
/// framework's sort of elements that compare themselves, with the lines in a field of the thread. The layout is that
/// of <see cref="WideEntry"/>.
/// </summary>
internal readonly struct ComparableWideEntry(WideEntry entry) : IComparable<ComparableWideEntry>
{
    // One per thread: every thread that sorts sets the lines of the chunk it sorts, and several sorts can run at once
    // in one process, as the tests run them. A static shared by all threads would let one sort read the lines of
    // another.
    [ThreadStatic]
    private static byte[]? s_lines;

    private readonly WideEntry _entry = entry;

    /// <summary>Sets the lines that the entries sorted on this thread point into; null when the thread is
    /// done.</summary>
    public static void UseLines(byte[]? lines)
    {
        s_lines = lines;
    }

    public int CompareTo(ComparableWideEntry other)
    {
        if (_entry.Prefix != other._entry.Prefix)
        {
            return _entry.Prefix < other._entry.Prefix ? -1 : 1;
        }

        return new WideEntryComparer(s_lines!).Compare(_entry, other._entry);
    }
}
