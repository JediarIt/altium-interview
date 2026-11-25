using FileSort.Engine.Lines;

namespace FileSort.Engine.Chunks.Sorting;

/// <summary>
/// An index entry that compares itself, so that the framework's sort of <see cref="IComparable{T}"/> elements sorts a
/// segment: that sort calls the comparison directly and inlines it, where a comparison delegate is a call per
/// comparison. The comparison needs the lines, which an element cannot hold, so they come from a field of the thread
/// that sorts, read only when the prefixes are equal. The layout is that of <see cref="IndexEntry"/>, so a span of
/// entries is cast to a span of these. A benchmark chose it: the sort of a chunk took 10% less time than with a
/// delegate or a hand-written introsort (see the README of the sorter, Performance).
/// <para>
/// No code of the solution calls <see cref="CompareTo"/> or the constructor, so an IDE counts no references to them.
/// The framework's sort calls <see cref="CompareTo"/>, and the entries are cast, not built: the constructor only lets
/// the compiler see that the field is assigned.
/// </para>
/// </summary>
internal readonly struct ComparableEntry(IndexEntry entry) : IComparable<ComparableEntry>
{
    // One per thread: every thread that sorts sets the lines of the chunk it sorts, and several sorts can run at once
    // in one process, as the tests run them. A static shared by all threads would let one sort read the lines of
    // another.
    [ThreadStatic]
    private static byte[]? s_lines;

    private readonly IndexEntry _entry = entry;

    /// <summary>
    /// Sets the lines that the entries sorted on this thread point into; null when the thread is done, so that a
    /// thread of the pool keeps no buffer alive.
    /// </summary>
    public static void UseLines(byte[]? lines)
    {
        s_lines = lines;
    }

    public int CompareTo(ComparableEntry other)
    {
        if (_entry.Prefix != other._entry.Prefix)
        {
            return _entry.Prefix < other._entry.Prefix ? -1 : 1;
        }

        return new EntryComparer(s_lines!).Compare(in _entry, in other._entry);
    }
}
