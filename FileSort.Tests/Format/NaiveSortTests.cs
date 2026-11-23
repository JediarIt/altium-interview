using System.Text;

namespace FileSort.Tests.Format;

/// <summary>
/// The naive comparison and the naive sort against every scenario of the <c>sort-order</c> spec, so that the oracle
/// of the checker's tests is itself checked against the spec.
/// </summary>
public class NaiveSortTests
{
    private static NaiveLine Parse(string line)
    {
        NaiveLine? parsed = NaiveLineParser.ParseLine(Encoding.UTF8.GetBytes(line));
        Assert.NotNull(parsed);
        return parsed;
    }

    private static List<NaiveLine> Parse(IEnumerable<string> lines) => [.. lines.Select(Parse)];

    private static string[] Texts(IEnumerable<NaiveLine> lines) => [.. lines.Select(l => Encoding.UTF8.GetString(l.ToBytes()))];

    // Requirements: Order by String, Order by Number

    [Theory]
    [MemberData(nameof(SortOrderScenarios.Orders), MemberType = typeof(SortOrderScenarios))]
    public void The_lines_of_a_scenario_are_sorted_into_its_order(string scenario, string[] lines, string[] expected)
    {
        Assert.NotEmpty(scenario);

        Assert.Equal(expected, Texts(NaiveSort.Sort(Parse(lines))));
        Assert.Equal(expected, Texts(NaiveSort.Sort(Parse(lines.Reverse()))));
    }

    [Theory]
    [MemberData(nameof(SortOrderScenarios.Orders), MemberType = typeof(SortOrderScenarios))]
    public void The_order_of_a_scenario_is_strict(string scenario, string[] lines, string[] expected)
    {
        Assert.NotEmpty(scenario);
        Assert.Equal(lines.Length, expected.Length);
        List<NaiveLine> sorted = Parse(expected);

        for (int i = 0; i < sorted.Count; i++)
        {
            for (int j = i + 1; j < sorted.Count; j++)
            {
                Assert.True(NaiveLineComparer.Instance.Compare(sorted[i], sorted[j]) < 0, $"{expected[i]} | {expected[j]}");
                Assert.True(NaiveLineComparer.Instance.Compare(sorted[j], sorted[i]) > 0, $"{expected[j]} | {expected[i]}");
            }
        }

        Assert.True(NaiveSort.IsSorted(sorted));
        Assert.False(NaiveSort.IsSorted(Parse(expected.Reverse())));
    }

    // Requirement: Lines with equal keys

    [Fact]
    public void Identical_lines()
    {
        List<NaiveLine> sorted = NaiveSort.Sort(
            Parse(["7. Apple", "5. Apple", "7. Apple", "9. Apple", "7. Banana", "7. Apple", "7. Ant"]));

        Assert.Equal(
            ["7. Ant", "5. Apple", "7. Apple", "7. Apple", "7. Apple", "9. Apple", "7. Banana"], Texts(sorted));
    }

    [Fact]
    public void Equal_keys_different_bytes()
    {
        NaiveLine padded = Parse("007. Apple");
        NaiveLine plain = Parse("7. Apple");

        Assert.Equal(0, NaiveLineComparer.Instance.Compare(padded, plain));
        Assert.Equal(0, NaiveLineComparer.Instance.Compare(plain, padded));
        Assert.True(NaiveSort.IsSorted([padded, plain]));
        Assert.True(NaiveSort.IsSorted([plain, padded]));
    }

    [Fact]
    public void Lines_with_equal_keys_keep_their_order()
    {
        Assert.Equal(["007. Apple", "7. Apple", "07. Apple"], Texts(NaiveSort.Sort(Parse(["007. Apple", "7. Apple", "07. Apple"]))));
        Assert.Equal(["7. Apple", "07. Apple", "007. Apple"], Texts(NaiveSort.Sort(Parse(["7. Apple", "07. Apple", "007. Apple"]))));
    }

    // The sorted file that the tests of the checker use

    [Fact]
    public void A_sorted_file_has_the_lines_of_the_input_byte_for_byte()
    {
        byte[] input = Encoding.UTF8.GetBytes("10. Apple\r\n007. Apple\r\n9. Apple\r\r\n1. \r\n3. Apple pie");

        byte[] sorted = NaiveSort.SortFile(input, "\r\n");

        // The last line of the input has no terminator; every line of the sorted file has one.
        Assert.Equal("1. \r\n007. Apple\r\n10. Apple\r\n9. Apple\r\r\n3. Apple pie\r\n", Encoding.UTF8.GetString(sorted));
    }

    [Fact]
    public void A_sorted_file_does_not_carry_the_byte_order_mark_of_the_input()
    {
        byte[] input = [0xEF, 0xBB, 0xBF, .. "2. Banana\n1. Apple\n"u8];

        Assert.Equal("1. Apple\n2. Banana\n"u8.ToArray(), NaiveSort.SortFile(input, "\n"));
    }

    [Fact]
    public void An_empty_file_sorts_into_an_empty_file()
    {
        Assert.Empty(NaiveSort.SortFile([], "\n"));
    }
}
