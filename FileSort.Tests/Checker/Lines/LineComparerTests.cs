using System.Text;

using FileSort.Checker.Lines;
using FileSort.Generator.Content;
using FileSort.Tests.Format;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Checker.Lines;

/// <summary>
/// The checker's comparison against every scenario of the <c>sort-order</c> spec, and against the naive comparison
/// of the tests on generated lines and on lines with Numbers above 64 bits.
/// </summary>
public class LineComparerTests
{
    private static int Compare(byte[] x, byte[] y)
    {
        Assert.Equal(LineFault.None, LineParser.Parse(x, out int xNumberLength));
        Assert.Equal(LineFault.None, LineParser.Parse(y, out int yNumberLength));
        return Math.Sign(LineComparer.Compare(x, xNumberLength, y, yNumberLength));
    }

    private static int Compare(string x, string y) => Compare(Encoding.UTF8.GetBytes(x), Encoding.UTF8.GetBytes(y));

    private static int NaiveCompare(byte[] x, byte[] y)
    {
        return Math.Sign(NaiveLineComparer.Instance.Compare(NaiveLineParser.ParseLine(x), NaiveLineParser.ParseLine(y)));
    }

    // Requirements: Order by String, Order by Number

    [Theory]
    [MemberData(nameof(SortOrderScenarios.Orders), MemberType = typeof(SortOrderScenarios))]
    public void Every_line_of_a_scenario_comes_before_the_lines_after_it(string scenario, string[] lines, string[] expected)
    {
        Assert.NotEmpty(scenario);
        Assert.Equal(lines.Length, expected.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(0, Compare(expected[i], expected[i]));
            for (int j = i + 1; j < expected.Length; j++)
            {
                Assert.True(Compare(expected[i], expected[j]) < 0, $"{expected[i]} | {expected[j]}");
                Assert.True(Compare(expected[j], expected[i]) > 0, $"{expected[j]} | {expected[i]}");
            }
        }
    }

    [Fact]
    public void Trailing_space_makes_the_Strings_different()
    {
        Assert.NotEqual(0, LineComparer.CompareStrings("Apple "u8, "Apple"u8));
        Assert.True(LineComparer.CompareStrings("Apple"u8, "Apple "u8) < 0);
    }

    [Fact]
    public void Bytes_are_compared_as_unsigned()
    {
        // 0x7F is the largest ASCII byte; every byte of a character outside ASCII is above it.
        Assert.True(Compare("1. \u007F", "1. \u0080") < 0);
        Assert.True(Compare("1. z", "1. é") < 0);
    }

    // Requirement: Lines with equal keys

    [Fact]
    public void Identical_lines()
    {
        Assert.Equal(0, Compare("7. Apple", "7. Apple"));
    }

    [Fact]
    public void Equal_keys_different_bytes()
    {
        Assert.Equal(0, Compare("007. Apple", "7. Apple"));
        Assert.Equal(0, Compare("7. Apple", "007. Apple"));
    }

    // Numbers as digits

    [Theory]
    [InlineData("0", "000", 0)]
    [InlineData("0", "1", -1)]
    [InlineData("00", "1", -1)]
    [InlineData("9", "10", -1)]
    [InlineData("10", "9", 1)]
    [InlineData("010", "9", 1)]
    [InlineData("19", "20", -1)]
    [InlineData("0019", "20", -1)]
    [InlineData("18446744073709551615", "18446744073709551616", -1)]
    [InlineData("99999999999999999999", "100000000000000000000", -1)]
    [InlineData("000000000000000000000000000001", "2", -1)]
    [InlineData("1234567890123456789012345678901234567890", "1234567890123456789012345678901234567891", -1)]
    [InlineData("1234567890123456789012345678901234567890", "01234567890123456789012345678901234567890", 0)]
    public void Numbers_are_compared_by_value(string x, string y, int expected)
    {
        byte[] first = Encoding.ASCII.GetBytes(x);
        byte[] second = Encoding.ASCII.GetBytes(y);

        Assert.Equal(expected, Math.Sign(LineComparer.CompareNumbers(first, second)));
        Assert.Equal(-expected, Math.Sign(LineComparer.CompareNumbers(second, first)));
    }

    // Against the naive comparison

    [Fact]
    public void It_agrees_with_the_naive_comparison_on_generated_lines()
    {
        byte[] content = TestContent.Generate(TestContent.Options(seed: 5, edgeCases: EdgeCases.All), 512 * 1024);
        List<byte[]> lines = NaiveLineParser.SplitLines(content, "\n");
        Random random = new(1);

        // Neighbours, and pairs from all over the file. Sorting first puts lines with equal Strings side by side,
        // so the Numbers decide for many of the neighbours.
        List<byte[]> sorted = [.. NaiveSort.Sort(lines.ConvertAll(line => NaiveLineParser.ParseLine(line)!)).Select(line => line.ToBytes())];
        int decidedByNumber = 0;
        for (int i = 1; i < sorted.Count; i++)
        {
            byte[] other = lines[random.Next(lines.Count)];

            Assert.Equal(NaiveCompare(sorted[i - 1], sorted[i]), Compare(sorted[i - 1], sorted[i]));
            Assert.Equal(NaiveCompare(sorted[i], sorted[i - 1]), Compare(sorted[i], sorted[i - 1]));
            Assert.Equal(NaiveCompare(sorted[i], other), Compare(sorted[i], other));

            bool sameString = NaiveLineParser.ParseLine(sorted[i])!.Text.AsSpan().SequenceEqual(NaiveLineParser.ParseLine(sorted[i - 1])!.Text);
            decidedByNumber += sameString ? 1 : 0;
        }

        Assert.True(decidedByNumber > 1000, $"Only {decidedByNumber} neighbours share their String.");
    }

    [Fact]
    public void It_agrees_with_the_naive_comparison_on_Numbers_above_64_bits()
    {
        Random random = new(2);
        string[] strings = ["Apple", "Apple pie", ""];
        List<byte[]> lines = [];
        for (int i = 0; i < 400; i++)
        {
            // 1 to 40 digits, a quarter of them with zeros in front: far beyond the 20 digits of 64 bits.
            string digits = string.Concat(Enumerable.Range(0, random.Next(1, 41)).Select(_ => (char)('0' + random.Next(10))));
            string zeros = new('0', random.Next(4) == 0 ? random.Next(1, 4) : 0);
            lines.Add(Encoding.ASCII.GetBytes($"{zeros}{digits}. {strings[random.Next(strings.Length)]}"));
        }

        foreach (byte[] x in lines)
        {
            foreach (byte[] y in lines)
            {
                Assert.Equal(NaiveCompare(x, y), Compare(x, y));
            }
        }
    }
}
