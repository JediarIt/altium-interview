using System.Runtime.InteropServices;

using FileSort.Engine.Chunks.Sorting;
using FileSort.Engine.Lines;

namespace FileSort.Benchmarks.Micro.Sorter.Candidates;

/// <summary>
/// The candidate routines for the sort of a segment of index entries (D7 of the design): the framework's sort with a
/// comparison delegate, the framework's sort of elements that compare themselves (the engine's choice), and a
/// hand-written introsort over a comparer struct.
/// </summary>
internal static class SortRoutine
{
    public const string Delegate = "Delegate";
    public const string Comparable = "Comparable";
    public const string Introsort = "Introsort";

    public static void Sort(string routine, Span<IndexEntry> entries, byte[] lines)
    {
        EntryComparer comparer = new(lines);
        switch (routine)
        {
            case Delegate:
                entries.Sort((x, y) => comparer.Compare(in x, in y));
                break;
            case Comparable:
                ComparableEntry.UseLines(lines);
                MemoryMarshal.Cast<IndexEntry, ComparableEntry>(entries).Sort();
                ComparableEntry.UseLines(null);
                break;
            case Introsort:
                Candidates.Introsort.Sort(entries, comparer);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(routine), routine, "Not a sort routine.");
        }
    }

    /// <summary>
    /// Fails when the entries are not in order: a sort that gets the order wrong can look fast, so every measured sort
    /// is checked after its measurement.
    /// </summary>
    public static void EnsureSorted<T, TComparer>(ReadOnlySpan<T> entries, TComparer comparer)
        where TComparer : IComparer<T>
    {
        for (int i = 1; i < entries.Length; i++)
        {
            if (comparer.Compare(entries[i - 1], entries[i]) > 0)
            {
                throw new InvalidOperationException($"The entries are out of order at {i}.");
            }
        }
    }
}
