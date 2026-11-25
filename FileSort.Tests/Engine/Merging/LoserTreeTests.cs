using FileSort.Engine.Lines;
using FileSort.Engine.Merging;
using FileSort.Tests.Engine.Lines;

namespace FileSort.Tests.Engine.Merging;

/// <summary>The loser tree merges sorted sources of any number, some empty, into one sorted sequence.</summary>
public class LoserTreeTests
{
    private static List<string> Merge(params string[][] sources)
    {
        ParsedLines lines = ParsedLines.From([.. sources.SelectMany(source => source)]);
        int[] starts = new int[sources.Length + 1];
        for (int source = 0; source < sources.Length; source++)
        {
            starts[source + 1] = starts[source] + sources[source].Length;
        }

        LoserTree<MergeHead, MergeHeadComparer> tree = new(sources.Length, lines.HeadComparer);
        int[] next = new int[sources.Length];
        for (int source = 0; source < sources.Length; source++)
        {
            next[source] = starts[source];
            if (sources[source].Length > 0)
            {
                tree.SetHead(source, lines.HeadOf(lines.Entries[next[source]]));
            }
            else
            {
                tree.SetEmpty(source);
            }
        }

        tree.Start();
        List<string> merged = [];
        while (!tree.Done)
        {
            int winner = tree.Winner;
            merged.Add(lines.TextOf(tree.WinnerHead.Entry));
            if (++next[winner] < starts[winner + 1])
            {
                tree.Advance(lines.HeadOf(lines.Entries[next[winner]]));
            }
            else
            {
                tree.Exhaust();
            }
        }

        return merged;
    }

    [Fact]
    public void One_source()
    {
        Assert.Equal(["1. A", "2. B"], Merge(["1. A", "2. B"]));
    }

    // The String of a line: what follows the first space.
    private static string StringOf(string line)
    {
        return line[(line.IndexOf(' ') + 1)..];
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(13)]
    public void Many_sources_some_of_them_empty(int count)
    {
        // Every fourth source is empty; the others have 20 lines each, sorted by their Strings.
        string[][] sources = new string[count][];
        for (int source = 0; source < count; source++)
        {
            IEnumerable<string> lines = Enumerable.Range(0, 20).Select(i => $"{i}. Line {(i * count) + source:D4}");
            sources[source] = source % 4 == 3 ? [] : [.. lines.OrderBy(StringOf, StringComparer.Ordinal)];
        }

        List<string> merged = Merge(sources);

        Assert.Equal(sources.Sum(source => source.Length), merged.Count);
        Assert.Equal([.. merged.OrderBy(StringOf, StringComparer.Ordinal)], merged);
    }

    [Fact]
    public void Identical_lines_from_several_sources_all_come_out()
    {
        List<string> merged = Merge(["7. Apple"], ["7. Apple", "8. Apple"], ["7. Apple"]);
        Assert.Equal(["7. Apple", "7. Apple", "7. Apple", "8. Apple"], merged);
    }

    [Fact]
    public void All_sources_empty()
    {
        Assert.Empty(Merge([], []));
    }
}
