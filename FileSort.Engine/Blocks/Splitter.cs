using FileSort.Engine.Lines;

namespace FileSort.Engine.Blocks;

/// <summary>
/// An entry of a sorted segment together with where it stands: the number of the segment and its index in the sorted
/// segment. Two such keys compare by the order of lines first, then by the segment, then by the index: a strict order,
/// in which no two entries of a chunk are equal, so that lines with one key can be split between blocks (D8 of the
/// design, "Repeated lines").
/// </summary>
internal readonly record struct Splitter(IndexEntry Entry, int Segment, int Index)
{
    /// <summary>Negative when this key comes before the other in the strict order, positive when after.</summary>
    public int CompareTo(in Splitter other, EntryComparer comparer)
    {
        return CompareTo(other.Entry, other.Segment, other.Index, comparer);
    }

    /// <summary>
    /// Negative when this key comes before the entry at <paramref name="index"/> of <paramref name="segment"/>,
    /// positive when after, 0 when it is that entry.
    /// </summary>
    public int CompareTo(in IndexEntry entry, int segment, int index, EntryComparer comparer)
    {
        IndexEntry own = Entry;
        int lines = comparer.Compare(in own, in entry);
        if (lines != 0)
        {
            return lines;
        }

        return Segment != segment ? Segment.CompareTo(segment) : Index.CompareTo(index);
    }
}
