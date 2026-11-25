using System.Text;

using FileSort.Common;
using FileSort.Engine.Lines;
using FileSort.Engine.Merging;
using FileSort.Generator.Content;
using FileSort.Tests.Engine.Lines;
using FileSort.Tests.Format;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Engine.Merging;

/// <summary>
/// The heads of a merge and their comparison. A head decides most comparisons from the 24 bytes of String and the
/// Number that it carries; whatever it decides must be what the comparison of the entries says, which reads the lines
/// and is itself checked against the naive order of the tests.
/// </summary>
public class MergeHeadComparerTests
{
    /// <summary>
    /// Lines around what a head holds: Strings that end or differ at 8, 16 and 24 bytes, zero bytes where a shorter
    /// String is padded, Numbers at the edge of 64 bits and above it, equal values with different digits.
    /// </summary>
    private static readonly string[] HandWrittenLines =
    [
        "1. ", "2. ", "1. A", "1. AB", "1. AB\0", "1. AB\0\0\0\0\0\0", "1. AB\0\0\0\0\0\0\0", "1. AB\0\0\0\0\0\0\0x",
        "1. ABCDEFG", "1. ABCDEFGH", "2. ABCDEFGH", "1. ABCDEFGH\0", "1. ABCDEFGHI", "1. ABCDEFGHJ",
        "1. ABCDEFGHIJKLMNO", "1. ABCDEFGHIJKLMNOP", "2. ABCDEFGHIJKLMNOP", "1. ABCDEFGHIJKLMNOP\0",
        "1. ABCDEFGHIJKLMNOPQ", "1. ABCDEFGHIJKLMNOPR", "1. ABCDEFGHIJKLMNOPQRSTUVW", "1. ABCDEFGHIJKLMNOPQRSTUVWX",
        "2. ABCDEFGHIJKLMNOPQRSTUVWX", "1. ABCDEFGHIJKLMNOPQRSTUVWX\0", "1. ABCDEFGHIJKLMNOPQRSTUVWXY",
        "2. ABCDEFGHIJKLMNOPQRSTUVWXY", "1. ABCDEFGHIJKLMNOPQRSTUVWXZ", "10. ABCDEFGHIJKLMNOPQRSTUVWXYZ and more",
        "9. ABCDEFGHIJKLMNOPQRSTUVWXYZ and more", "9. ABCDEFGHIJKLMNOPQRSTUVWXYZ and less",
        "7. Apple", "007. Apple", "07. Apple", "0. Apple", "00. Apple", "8. Apple", "10. Apple", "9. Apple",
        "18446744073709551615. Apple", "18446744073709551616. Apple", "018446744073709551615. Apple",
        "99999999999999999999. Apple", "100000000000000000000000000000. Apple",
        "000000000000000000000000000000000000007. Apple", "9223372036854775808. Apple", "5. Apple\r", "5. Apple ",
        "1. й", "1. Ａ", "1. \U0001F600", "3. Something something something", "30432. Something something something",
    ];

    private static int Sign(int value) => Math.Sign(value);

    private static void AssertAgree(ParsedLines lines, MergeHead[] heads, int i, int j)
    {
        int expected = Sign(lines.Comparer.Compare(in lines.Entries[i], in lines.Entries[j]));
        int actual = Sign(lines.HeadComparer.Compare(in heads[i], in heads[j]));
        if (expected != actual)
        {
            string pair = $"'{lines.TextOf(lines.Entries[i])}' vs '{lines.TextOf(lines.Entries[j])}'";
            Assert.Fail($"{pair}: {actual}, the entries say {expected}");
        }
    }

    [Fact]
    public void A_head_holds_the_first_24_bytes_of_the_String_and_the_value_of_the_Number()
    {
        ParsedLines lines = ParsedLines.From("0415. Apple pie is the best of all");

        MergeHead head = lines.HeadOf(lines.Entries[0]);

        Assert.Equal(StringPrefix.Read("Apple pi"u8), head.Entry.Prefix);
        Assert.Equal(StringPrefix.Read("e is the"u8), head.Second);
        Assert.Equal(StringPrefix.Read(" best of"u8), head.Third);
        Assert.Equal("Apple pie is the best of all".Length, head.StringLength);
        Assert.True(head.NumberFits);
        Assert.Equal(415UL, head.Number);
    }

    [Theory]
    [InlineData("1. Apple", 0x0000000000000000UL, 0UL)]
    [InlineData("1. ABCDEFGHI", 0x4900000000000000UL, 0UL)]
    [InlineData("1. ABCDEFGHIJKLMNOPQR", 0x494A4B4C4D4E4F50UL, 0x5152000000000000UL)]
    public void A_String_that_ends_inside_the_key_is_padded_with_zero_bytes(string line, ulong second, ulong third)
    {
        ParsedLines lines = ParsedLines.From(line);

        MergeHead head = lines.HeadOf(lines.Entries[0]);

        Assert.Equal(second, head.Second);
        Assert.Equal(third, head.Third);
    }

    [Theory]
    [InlineData("18446744073709551615. A", true, 18446744073709551615UL)]
    [InlineData("000000000000000000000000000000000000007. A", true, 7UL)]
    [InlineData("0. A", true, 0UL)]
    [InlineData("18446744073709551616. A", false, 0UL)]
    [InlineData("100000000000000000000000000000. A", false, 0UL)]
    public void A_Number_above_64_bits_does_not_fit_and_leading_zeros_do_not_count(string line, bool fits, ulong value)
    {
        ParsedLines lines = ParsedLines.From(line);

        MergeHead head = lines.HeadOf(lines.Entries[0]);

        Assert.Equal(fits, head.NumberFits);
        if (fits)
        {
            Assert.Equal(value, head.Number);
        }
    }

    [Fact]
    public void Every_pair_of_hand_written_lines_compares_as_their_entries_do()
    {
        ParsedLines lines = ParsedLines.From(HandWrittenLines);
        MergeHead[] heads = [.. lines.Entries.Select(lines.HeadOf)];

        for (int i = 0; i < heads.Length; i++)
        {
            for (int j = 0; j < heads.Length; j++)
            {
                AssertAgree(lines, heads, i, j);
            }
        }
    }

    [Fact]
    public void Heads_without_a_key_compare_as_their_entries_do()
    {
        // A merge of a few runs makes its heads without the key: equal Strings, and Strings that agree in their
        // prefixes, must still go to the comparison that reads the lines.
        ParsedLines lines = ParsedLines.From(HandWrittenLines);
        MergeHead[] heads = [.. lines.Entries.Select(entry => MergeHead.WithoutKey(in entry))];

        for (int i = 0; i < heads.Length; i++)
        {
            for (int j = 0; j < heads.Length; j++)
            {
                AssertAgree(lines, heads, i, j);
            }
        }
    }

    [Theory]
    [InlineData(LineEnding.Lf, 3UL)]
    [InlineData(LineEnding.CrLf, 4UL)]
    public void Generated_lines_compare_as_their_entries_do(LineEnding lineEnding, ulong seed)
    {
        string terminator = lineEnding == LineEnding.CrLf ? "\r\n" : "\n";
        byte[] content = TestContent.Generate(
            TestContent.Options(seed: seed, edgeCases: EdgeCases.All, lineEnding: lineEnding), TestContent.Megabyte);
        List<byte[]> raw = NaiveLineParser.SplitLines(content, terminator);
        raw.AddRange(HandWrittenLines.Select(Encoding.UTF8.GetBytes));
        ParsedLines lines = ParsedLines.From(raw);
        MergeHead[] heads = [.. lines.Entries.Select(lines.HeadOf)];

        // Pairs at random, most of them decided by the first part of the key.
        System.Random random = new(11);
        for (int k = 0; k < 100_000; k++)
        {
            AssertAgree(lines, heads, random.Next(heads.Length), random.Next(heads.Length));
        }

        // Neighbours of the sorted order, which a merge compares: they agree in most of their keys.
        int[] order = [.. Enumerable.Range(0, heads.Length)];
        EntryComparer comparer = lines.Comparer;
        Array.Sort(order, (x, y) => comparer.Compare(in lines.Entries[x], in lines.Entries[y]));
        for (int k = 1; k < order.Length; k++)
        {
            AssertAgree(lines, heads, order[k - 1], order[k]);
            AssertAgree(lines, heads, order[k], order[k - 1]);
        }
    }
}
