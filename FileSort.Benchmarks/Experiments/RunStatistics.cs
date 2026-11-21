namespace FileSort.Benchmarks.Experiments;

/// <summary>
/// The runs of one configuration, reduced to what a table can show: the median, which one slow run does not move,
/// and the spread from the slowest to the fastest run. One run has no spread, and a table has to say so: the
/// number of runs is part of the result.
/// </summary>
internal readonly record struct RunStatistics(double Median, double Min, double Max, int Runs)
{
    public static RunStatistics From(IReadOnlyCollection<double> values)
    {
        double[] sorted = [.. values.Order()];
        int middle = sorted.Length / 2;
        double median = sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        return new RunStatistics(median, sorted[0], sorted[^1], sorted.Length);
    }
}
