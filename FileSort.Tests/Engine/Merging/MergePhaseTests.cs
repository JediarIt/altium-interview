using System.Text;

using FileSort.Common;
using FileSort.Engine;
using FileSort.Engine.Chunks;
using FileSort.Engine.Merging;
using FileSort.Engine.Runs;
using FileSort.Tests.Engine.InMemory;
using FileSort.Tests.Format;

namespace FileSort.Tests.Engine.Merging;

/// <summary>
/// The merge of runs: many runs with tiny buffers, exactly as many runs as one merge
/// takes and one more, and several passes. After every group only the runs that the plan keeps are left, and the
/// output is the naive sort of all the lines.
/// </summary>
public class MergePhaseTests
{
    private const int MaxLineLength = 40;

    // The runs of sorted lines, written to the storage as the chunk phase would.
    private static List<Run> Runs(MemoryRunStorage storage, int count, out byte[] allLines)
    {
        List<Run> runs = [];
        StringBuilder all = new();
        for (int run = 0; run < count; run++)
        {
            int linesOfRun = 3 + (run % 5);
            string text = string.Concat(Enumerable.Range(0, linesOfRun).Select(i => LineOfRun(run, i) + "\n"));
            byte[] bytes = NaiveSort.SortFile(Encoding.UTF8.GetBytes(text), "\n");
            using (Stream stream = storage.CreateRun(out int id))
            {
                stream.Write(bytes);
                runs.Add(new Run(id, bytes.Length));
            }

            all.Append(Encoding.UTF8.GetString(bytes));
        }

        allLines = Encoding.UTF8.GetBytes(all.ToString());
        return runs;
    }

    private static int Merge(MemoryRunStorage storage, List<Run> runs, int room)
    {
        // A chunk buffer of exactly as many smallest run buffers as the room.
        ChunkBuffer buffer = new(room * 2 * (MaxLineLength + 2));
        SortProgress progress = new();
        RunMerger merger = new(storage, buffer, new byte[4 * (MaxLineLength + 2)], LineEnding.Lf, 1 << 20, progress);
        SpaceCheck space = new(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "output.txt"), _ => null);
        MergePhase phase = new(storage, merger, space, progress, room);
        return phase.Merge(runs, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(5, 5, 1)]
    [InlineData(6, 5, 2)]
    [InlineData(1, 5, 1)]
    [InlineData(40, 5, 10)]
    [InlineData(200, 3, 100)]
    public void The_runs_merge_into_the_naive_sort_in_the_merges_of_the_plan(int count, int room, int merges)
    {
        MemoryRunStorage storage = new();
        List<Run> runs = Runs(storage, count, out byte[] allLines);
        int createdBefore = storage.RunsCreated;

        Assert.Equal(merges, Merge(storage, runs, room));

        Assert.Equal(NaiveSort.SortFile(allLines, "\n"), storage.Output);
        Assert.Empty(storage.Runs);
        Assert.Equal(merges - 1, storage.RunsCreated - createdBefore);
    }

    [Fact]
    public void After_every_group_only_the_runs_of_the_plan_are_left()
    {
        MemoryRunStorage storage = new();
        List<Run> runs = Runs(storage, 23, out _);

        Merge(storage, runs, room: 4);

        // Before every group the runs left are those of the plan: 23, then 23 - 4 + 1 = 20, and so on down to 4.
        List<int> expected = [];
        for (int left = 23; left > 4; left -= MergePlan.NextGroup(left, 4) - 1)
        {
            expected.Add(left);
        }

        Assert.Equal(expected, storage.RunsBeforeEachRun.Skip(23));
        Assert.Equal(4, storage.RunsBeforeOutput);
    }

    // Line i of a run: Numbers and Strings that differ from run to run.
    private static string LineOfRun(int run, int i)
    {
        return $"{(i * 31) + run}. Line {((i * 7) + (run * 3)) % 50:D2}";
    }
}
