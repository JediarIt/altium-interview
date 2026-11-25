namespace FileSort.Engine.Merging;

/// <summary>
/// How many runs one merge takes, and how many merges a sort runs. One merge takes at
/// most as many runs as there are smallest run buffers in the chunk buffer. With more runs, groups of the smallest
/// runs are merged into longer runs first, each group just large enough that the runs left fit into one final merge:
/// with k runs and room for F, a group of k - F + 1 runs when that is at most F, otherwise a group of F and the rule
/// again. Merging no more than that rewrites the fewest bytes: for 700 runs and room for 430, one group of 271 runs,
/// not 430.
/// </summary>
internal static class MergePlan
{
    /// <summary>The runs of the next group, or 0 when the runs fit into the final merge.</summary>
    public static int NextGroup(int runs, int room)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(room, 2);
        if (runs <= room)
        {
            return 0;
        }

        return runs - room + 1 <= room ? runs - room + 1 : room;
    }

    /// <summary>The merges that a sort of so many runs runs: every group and the final merge.</summary>
    public static int Merges(int runs, int room)
    {
        if (runs == 0)
        {
            return 0;
        }

        int merges = 1;
        for (int group = NextGroup(runs, room); group > 0; group = NextGroup(runs, room))
        {
            runs -= group - 1;
            merges++;
        }

        return merges;
    }
}
