using FileSort.Engine.Runs;

namespace FileSort.Engine.Merging;

/// <summary>
/// The second phase of D3 of the design: the runs merged into the output, in one merge when one merge can take them
/// all, otherwise after merges of groups of the smallest runs into longer runs (see <see cref="MergePlan"/>). Each run
/// of a group is deleted as soon as the group is merged.
/// </summary>
internal sealed class MergePhase
{
    private readonly IRunStorage _storage;
    private readonly RunMerger _merger;
    private readonly SpaceCheck _space;
    private readonly SortProgress _progress;
    private readonly int _room;

    /// <param name="storage">The storage of the runs and of the output.</param>
    /// <param name="merger">The merge of runs.</param>
    /// <param name="space">The check of free space before every group.</param>
    /// <param name="progress">The counters of the passes.</param>
    /// <param name="room">The most runs that one merge takes.</param>
    public MergePhase(IRunStorage storage, RunMerger merger, SpaceCheck space, SortProgress progress, int room)
    {
        _storage = storage;
        _merger = merger;
        _space = space;
        _progress = progress;
        _room = room;
    }

    /// <summary>Merges the runs into the output and deletes them. Returns the merges that it ran.</summary>
    public int Merge(List<Run> runs, CancellationToken cancellationToken)
    {
        int merges = MergePlan.Merges(runs.Count, _room);
        int merge = 0;
        int group;
        while ((group = MergePlan.NextGroup(runs.Count, _room)) > 0)
        {
            List<Run> smallest = [.. runs.OrderBy(run => run.Length).Take(group)];
            long bytes = smallest.Sum(run => run.Length);
            _space.BeforeGroup(bytes);
            _progress.StartPass(++merge, merges, bytes);
            Run merged = MergeIntoRun(smallest, cancellationToken);
            Delete(smallest);
            runs.RemoveAll(smallest.Contains);
            runs.Add(merged);
        }

        _progress.StartPass(++merge, merges, runs.Sum(run => run.Length));
        using (Stream output = _storage.CreateOutput())
        {
            _merger.Merge(runs, output, cancellationToken);
        }

        Delete(runs);
        return merges;
    }

    private Run MergeIntoRun(List<Run> group, CancellationToken cancellationToken)
    {
        using Stream run = _storage.CreateRun(out int id);
        return new Run(id, _merger.Merge(group, run, cancellationToken));
    }

    private void Delete(List<Run> runs)
    {
        foreach (Run run in runs)
        {
            _storage.Delete(run.Id);
        }
    }
}
