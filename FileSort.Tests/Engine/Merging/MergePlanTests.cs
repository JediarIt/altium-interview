using FileSort.Engine.Merging;

namespace FileSort.Tests.Engine.Merging;

/// <summary>The sizes of the groups of runs and the count of merges.</summary>
public class MergePlanTests
{
    [Theory]
    [InlineData(1, 430, 0)]
    [InlineData(430, 430, 0)]
    [InlineData(431, 430, 2)]
    [InlineData(700, 430, 271)]
    [InlineData(1000, 430, 430)]
    [InlineData(10, 2, 2)]
    public void The_next_group(int runs, int room, int group)
    {
        Assert.Equal(group, MergePlan.NextGroup(runs, room));
    }

    [Theory]
    [InlineData(0, 430, 0)]
    [InlineData(1, 430, 1)]
    [InlineData(430, 430, 1)]
    [InlineData(431, 430, 2)]
    [InlineData(700, 430, 2)]
    [InlineData(1000, 430, 3)]
    [InlineData(10, 2, 9)]
    public void The_count_of_merges(int runs, int room, int merges)
    {
        Assert.Equal(merges, MergePlan.Merges(runs, room));
    }

    [Fact]
    public void After_the_groups_the_runs_fit_one_merge()
    {
        // 1000 runs, room for 430: a group of 430 leaves 571, a group of 142 leaves 430, which one merge takes.
        int runs = 1000;
        List<int> groups = [];
        for (int group = MergePlan.NextGroup(runs, 430); group > 0; group = MergePlan.NextGroup(runs, 430))
        {
            groups.Add(group);
            runs -= group - 1;
        }

        Assert.Equal([430, 142], groups);
        Assert.Equal(430, runs);
    }

    [Fact]
    public void A_merge_takes_at_least_two_runs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MergePlan.NextGroup(5, 1));
    }
}
