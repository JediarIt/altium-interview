namespace FileSort.Benchmarks.Experiments.Sorter;

/// <summary>
/// The work of the garbage collector so far in this process: collections by generation and their total pause.
/// The difference of two of them is the work done between them.
/// </summary>
internal readonly record struct GcActivity(int Gen0, int Gen1, int Gen2, TimeSpan Pause)
{
    public static GcActivity Now()
    {
        return new GcActivity(
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            GC.GetTotalPauseDuration());
    }

    public GcActivity Since(GcActivity before)
    {
        return new GcActivity(Gen0 - before.Gen0, Gen1 - before.Gen1, Gen2 - before.Gen2, Pause - before.Pause);
    }
}
