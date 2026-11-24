using System.Text;

using FileSort.Common;
using FileSort.Engine.Lines;
using FileSort.Generator.Content;
using FileSort.Tests.Format;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Engine.Lines;

/// <summary>
/// The engine's comparison of index entries: every scenario of <c>sort-order</c>, the order of lines with equal keys,
/// and agreement with the naive comparison of the tests, with the tie-break by bytes, on generated lines and on lines
/// written by hand for the cases that the entry handles apart.
/// </summary>
public class EntryComparerTests
{
    /// <summary>
    /// Lines whose order the entry decides in its less common ways: Numbers above 64 bits and of zeros only, Strings
    /// around the 8 bytes of the prefix, carriage returns and zero bytes in Strings, equal keys with different bytes.
    /// </summary>
    private static readonly string[] HandWrittenLines =
    [
        "18446744073709551615. Apple", "18446744073709551616. Apple", "18446744073709551617. Apple",
        "018446744073709551616. Apple", "100000000000000000000000000000. Apple", "99999999999999999999. Apple",
        "000000000000000000000000000000000000007. Apple", "7. Apple", "007. Apple", "07. Apple",
        "0. Apple", "00. Apple", "000000000000000000000000. Apple", "1. Apple", "01. Apple",
        "0. ", "00. ", "5. ", "5. \r", "5. Apple\r", "5. Apple\r\r", "5. Apple", "5. Apple ", "5. A\rB",
        "1. AB", "1. AB\0", "1. AB\0\0\0\0\0\0", "1. AB\0\0\0\0\0\0\0", "1. AB\0\0\0\0\0\0\0x", "1. ABCDEFGH",
        "1. ABCDEFGHI", "1. ABCDEFG", "1. ABCDEFGH\0", "2. ABCDEFGHI", "1. ABCDEFGHIJKLMNOP", "1. ABCDEFGHIJKLMNOQ",
        "1. \U0001F600", "1. Ａ", "1. й", "1. й", "1. apple", "1. Banana", "9. Apple pie",
        "18446744073709551616. Apple pie", "4294967296. Apple", "9223372036854775807. Apple",
        "9223372036854775808. Apple",
    ];

    private static int Sign(int value) => Math.Sign(value);

    // The order the sorter writes: the naive comparison of sort-order, then the bytes of the lines.
    private static int NaiveOrder(NaiveLine x, NaiveLine y)
    {
        int keys = NaiveLineComparer.Instance.Compare(x, y);
        return keys != 0 ? keys : ByteOrder.Instance.Compare(x.ToBytes(), y.ToBytes());
    }

    private static string[] SortedTexts(ParsedLines lines)
    {
        IndexEntry[] entries = [.. lines.Entries];
        EntryComparer comparer = lines.Comparer;
        Array.Sort(entries, (x, y) => comparer.Compare(in x, in y));
        return [.. entries.Select(lines.TextOf)];
    }

    // Requirements of sort-order: Order by String, Order by Number

    [Theory]
    [MemberData(nameof(SortOrderScenarios.Orders), MemberType = typeof(SortOrderScenarios))]
    public void The_lines_of_a_scenario_are_sorted_into_its_order(string scenario, string[] lines, string[] expected)
    {
        Assert.NotEmpty(scenario);

        Assert.Equal(expected, SortedTexts(ParsedLines.From(lines)));
        Assert.Equal(expected, SortedTexts(ParsedLines.From([.. lines.Reverse()])));
    }

    // Requirement of sort-order: Lines with equal keys

    [Fact]
    public void Identical_lines_compare_equal()
    {
        ParsedLines lines = ParsedLines.From("7. Apple", "7. Apple", "007. Apple");

        Assert.Equal(0, lines.Comparer.Compare(in lines.Entries[0], in lines.Entries[1]));
        Assert.NotEqual(0, lines.Comparer.Compare(in lines.Entries[0], in lines.Entries[2]));
    }

    // The sorter's requirement "Lines with equal keys"

    [Fact]
    public void Equal_keys_different_bytes()
    {
        ParsedLines lines = ParsedLines.From("7. Apple", "007. Apple", "07. Apple", "00. Apple", "0. Apple");

        Assert.Equal(["0. Apple", "00. Apple", "007. Apple", "07. Apple", "7. Apple"], SortedTexts(lines));
    }

    // Agreement with the naive comparison

    [Fact]
    public void Every_pair_of_hand_written_lines_compares_as_the_naive_order_says()
    {
        ParsedLines lines = ParsedLines.From(HandWrittenLines);
        List<NaiveLine> naive = [.. lines.Lines.Select(line => NaiveLineParser.ParseLine(line)!)];

        for (int i = 0; i < naive.Count; i++)
        {
            for (int j = 0; j < naive.Count; j++)
            {
                int expected = Sign(NaiveOrder(naive[i], naive[j]));
                int actual = Sign(lines.Comparer.Compare(in lines.Entries[i], in lines.Entries[j]));
                string pair = $"'{HandWrittenLines[i]}' vs '{HandWrittenLines[j]}'";
                Assert.True(expected == actual, $"{pair}: {actual}, expected {expected}");
            }
        }
    }

    [Theory]
    [InlineData(LineEnding.Lf, 1UL)]
    [InlineData(LineEnding.CrLf, 2UL)]
    public void Generated_lines_sort_into_the_naive_order_and_random_pairs_agree(LineEnding lineEnding, ulong seed)
    {
        string terminator = lineEnding == LineEnding.CrLf ? "\r\n" : "\n";
        byte[] content = TestContent.Generate(
            TestContent.Options(seed: seed, edgeCases: EdgeCases.All, lineEnding: lineEnding), TestContent.Megabyte);
        List<byte[]> raw = NaiveLineParser.SplitLines(content, terminator);
        raw.AddRange(HandWrittenLines.Select(Encoding.UTF8.GetBytes));
        ParsedLines lines = ParsedLines.From(raw);
        List<NaiveLine> naive = [.. raw.Select(line => NaiveLineParser.ParseLine(line)!)];

        // The whole set sorted by the engine's comparison is the naive sort, byte for byte.
        IndexEntry[] entries = [.. lines.Entries];
        EntryComparer comparer = lines.Comparer;
        Array.Sort(entries, (x, y) => comparer.Compare(in x, in y));
        byte[] sorted = [.. entries.SelectMany(entry => (byte[])[.. lines.LineOf(entry), (byte)'\n'])];
        Assert.Equal(NaiveSort.Join(NaiveSort.Sort(naive), "\n"), sorted);

        // Pairs at random, most of them decided by the prefix, and neighbours of the sorted order, which are not.
        System.Random random = new(7);
        for (int k = 0; k < 100_000; k++)
        {
            int i = random.Next(naive.Count);
            int j = random.Next(naive.Count);
            int naiveSign = Sign(NaiveOrder(naive[i], naive[j]));
            Assert.Equal(naiveSign, Sign(comparer.Compare(in lines.Entries[i], in lines.Entries[j])));
        }

        for (int k = 1; k < entries.Length; k++)
        {
            Assert.True(comparer.Compare(in entries[k - 1], in entries[k]) <= 0);
            Assert.True(comparer.Compare(in entries[k], in entries[k - 1]) >= 0);
        }
    }

    [Fact]
    public void A_line_compares_equal_to_itself_and_the_order_is_antisymmetric()
    {
        ParsedLines lines = ParsedLines.From(HandWrittenLines);

        for (int i = 0; i < lines.Entries.Length; i++)
        {
            Assert.Equal(0, lines.Comparer.Compare(in lines.Entries[i], in lines.Entries[i]));
            for (int j = 0; j < lines.Entries.Length; j++)
            {
                Assert.Equal(
                    -Sign(lines.Comparer.Compare(in lines.Entries[j], in lines.Entries[i])),
                    Sign(lines.Comparer.Compare(in lines.Entries[i], in lines.Entries[j])));
            }
        }
    }
}
