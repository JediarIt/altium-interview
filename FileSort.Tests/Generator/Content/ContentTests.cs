using System.Text.RegularExpressions;

using FileSort.Generator.Content;
using FileSort.Tests.Format;

namespace FileSort.Tests.Generator.Content;

/// <summary>Scenarios of "Default content", "String length in words" and "Repeated Strings".</summary>
public partial class ContentTests
{
    [GeneratedRegex(@"^(0|[1-9][0-9]{0,9})\. [A-Z][a-z]*( [a-z]+)*$")]
    private static partial Regex PlainLine();

    private static int WordCount(NaiveLine line) => line.TextAsString.Split(' ').Length;

    // Requirement: Default content

    [Fact]
    public void Shape_of_a_default_line()
    {
        List<NaiveLine> lines = TestContent.Lines(TestContent.Options(), 4 * TestContent.Megabyte);

        Assert.All(lines, line =>
        {
            Assert.Matches(PlainLine(), $"{line.NumberText}. {line.TextAsString}");
            Assert.True(line.Number <= 4294967295);
        });
    }

    [Fact]
    public void Repeats_in_a_small_file()
    {
        List<NaiveLine> lines = TestContent.Lines(TestContent.Options(), 10 * TestContent.Megabyte);

        Assert.Equal(Enumerable.Range(1, 10), lines.Select(line => line.NumberText.Length).Distinct().Order());

        int distinctLines = lines.Select(line => $"{line.NumberText}. {line.TextAsString}").Distinct().Count();
        Assert.True(distinctLines < lines.Count, "No two lines are byte-identical.");

        // The README of the generator says "thousands of identical lines" in a file of this size.
        int repeated = lines.Count - distinctLines;
        Assert.True(repeated > 1000, $"Only {repeated} lines repeat an earlier line.");

        bool sharedString = lines
            .GroupBy(line => line.TextAsString)
            .Any(HasSeveralNumbers);
        Assert.True(sharedString, "No String occurs with two different Numbers.");
    }

    [Fact]
    public void Numbers_have_a_uniform_bit_length()
    {
        List<NaiveLine> lines = TestContent.Lines(TestContent.Options(), 10 * TestContent.Megabyte);

        // The exact share of each decimal length: a bit length from 1 to 32 with equal probability, then a uniform
        // value of that many bits.
        double[] expected = new double[11];
        for (int bits = 1; bits <= 32; bits++)
        {
            double values = Math.Pow(2, bits);
            for (int digits = 1; digits <= 10; digits++)
            {
                double from = digits == 1 ? 0 : Math.Pow(10, digits - 1);
                double to = Math.Min(Math.Pow(10, digits), values);
                expected[digits] += Math.Max(0, to - from) / values / 32;
            }
        }

        // The figures of the design: 13.3% of one digit, 4.3% of ten digits.
        Assert.Equal(0.133, expected[1], 3);
        Assert.Equal(0.043, expected[10], 3);

        for (int digits = 1; digits <= 10; digits++)
        {
            double actual = lines.Count(line => line.NumberText.Length == digits) / (double)lines.Count;
            Assert.InRange(actual, expected[digits] - 0.005, expected[digits] + 0.005);
        }
    }

    // Requirement: String length in words

    [Fact]
    public void Range_of_words()
    {
        List<NaiveLine> lines = TestContent.Lines(TestContent.Options(words: "2-3"), TestContent.Megabyte);

        Assert.Equal([2, 3], lines.Select(WordCount).Distinct().Order());
    }

    [Fact]
    public void Exact_number_of_words()
    {
        List<NaiveLine> lines = TestContent.Lines(TestContent.Options(words: "4"), TestContent.Megabyte);

        Assert.All(lines, line => Assert.Equal(4, WordCount(line)));
    }

    [Fact]
    public void Every_number_of_words_of_the_range_is_equally_likely()
    {
        List<NaiveLine> lines = TestContent.Lines(TestContent.Options(duplicates: 0), 10 * TestContent.Megabyte);

        for (int words = 1; words <= 5; words++)
        {
            double share = lines.Count(line => WordCount(line) == words) / (double)lines.Count;
            Assert.InRange(share, 0.19, 0.21);
        }
    }

    [Fact]
    public void Long_Strings_use_every_word_slot_of_a_random_value()
    {
        ContentOptions options = TestContent.Options(duplicates: 0, words: "40");
        List<NaiveLine> lines = TestContent.Lines(options, TestContent.Megabyte);

        Assert.All(lines, line => Assert.Equal(40, WordCount(line)));

        // Every position of a long String draws from the whole dictionary.
        for (int position = 0; position < 40; position += 13)
        {
            int distinct = lines.Select(line => WordAt(line, position)).Distinct().Count();
            Assert.True(distinct > 900, $"Position {position} has only {distinct} distinct words.");
        }
    }

    // Requirement: Repeated Strings

    [Fact]
    public void Share_of_pool_lines()
    {
        List<NaiveLine> lines = TestContent.Lines(
            TestContent.Options(duplicates: 30, words: "5"), 10 * TestContent.Megabyte);

        Assert.InRange(TestContent.RepeatedShare(lines), 0.29, 0.31);
    }

    [Fact]
    public void With_default_options_short_Strings_repeat_by_chance_on_top_of_the_pool()
    {
        // The README of the generator says "about 45%": 30% from the pool, plus the one-word Strings of the other
        // lines.
        List<NaiveLine> lines = TestContent.Lines(TestContent.Options(), 10 * TestContent.Megabyte);

        Assert.InRange(TestContent.RepeatedShare(lines), 0.43, 0.47);
    }

    [Fact]
    public void Pool_turned_off()
    {
        List<NaiveLine> lines = TestContent.Lines(
            TestContent.Options(duplicates: 0, words: "5"), 10 * TestContent.Megabyte);

        Assert.True(TestContent.RepeatedShare(lines) < 0.01);
    }

    [Fact]
    public void Only_repeated_Strings()
    {
        List<NaiveLine> lines = TestContent.Lines(TestContent.Options(duplicates: 100), 10 * TestContent.Megabyte);

        int distinct = lines.Select(line => line.TextAsString).Distinct().Count();

        Assert.InRange(distinct, 2, ContentFormat.PoolSize);
    }

    [Fact]
    public void Pool_Strings_are_chosen_uniformly()
    {
        List<NaiveLine> lines = TestContent.Lines(
            TestContent.Options(duplicates: 100, words: "5"), 10 * TestContent.Megabyte);

        List<int> counts = [.. lines.GroupBy(line => line.TextAsString).Select(group => group.Count())];
        double mean = lines.Count / (double)ContentFormat.PoolSize;

        Assert.Equal(ContentFormat.PoolSize, counts.Count);
        Assert.InRange(counts.Min(), mean * 0.6, mean);
        Assert.InRange(counts.Max(), mean, mean * 1.4);
    }

    // The lines of one String have more than one Number.
    private static bool HasSeveralNumbers(IGrouping<string, NaiveLine> sameString)
    {
        return sameString.Select(line => line.Number).Distinct().Count() > 1;
    }

    // The word at a position of the String, in lower case.
    private static string WordAt(NaiveLine line, int position)
    {
        return line.TextAsString.Split(' ')[position].ToLowerInvariant();
    }
}
