using System.Text;
using System.Text.RegularExpressions;

using FileSort.Common;
using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Generator.Content.Lines;
using FileSort.Generator.Random;
using FileSort.Tests.Format;

namespace FileSort.Tests.Generator.Content;

/// <summary>Scenarios of "Edge cases", and the rules of the design that the spec leaves out.</summary>
public partial class EdgeCaseTests
{
    private static readonly EdgeCases[] Categories =
    [
        EdgeCases.Unicode,
        EdgeCases.Separator,
        EdgeCases.Case,
        EdgeCases.BigNumbers,
        EdgeCases.LeadingZeros,
        EdgeCases.Empty,
    ];

    [GeneratedRegex(@"^[A-Z][a-z]*( [a-z]+)*$")]
    private static partial Regex PlainString();

    [GeneratedRegex(@"^[0-9]+\. ")]
    private static partial Regex DigitsAndSeparator();

    private static List<NaiveLine> Lines(EdgeCases edgeCases, string words = "1-5")
    {
        return TestContent.Lines(TestContent.Options(edgeCases: edgeCases, words: words), TestContent.Megabyte);
    }

    // What a reader can tell about a line without knowing how it was made.
    private static bool Shows(NaiveLine line, EdgeCases category)
    {
        string text = line.TextAsString;
        return category switch
        {
            EdgeCases.Unicode => text.Any(c => c > 0x7F),
            EdgeCases.Separator => text.Contains(". ", StringComparison.Ordinal) || text.StartsWith(' '),
            EdgeCases.Case => text.Length > 0 && !text.StartsWith(' ')
                && text.All(c => c is ' ' || char.IsAsciiLetter(c)) && !PlainString().IsMatch(text),
            EdgeCases.BigNumbers => line.Number > 4294967295,
            EdgeCases.LeadingZeros => line.NumberText.Length >= 2 && line.NumberText[0] == '0',
            EdgeCases.Empty => text.Length == 0,
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
    }

    private static double Share(List<NaiveLine> lines, EdgeCases category)
    {
        return lines.Count(line => Shows(line, category)) / (double)lines.Count;
    }

    [Fact]
    public void Category_unicode()
    {
        List<NaiveLine> lines = Lines(EdgeCases.Unicode);
        string all = string.Join('\n', lines.Select(line => line.TextAsString));

        Assert.Equal([1, 2, 3, 4], all.EnumerateRunes().Select(rune => rune.Utf8SequenceLength).Distinct().Order());
        Assert.Contains("Ａ", all, StringComparison.Ordinal);
        Assert.Contains("\U0001F600", all, StringComparison.Ordinal);
        Assert.Contains("й", all, StringComparison.Ordinal);
        Assert.Contains("й", all, StringComparison.Ordinal);
        Assert.True(Share(lines, EdgeCases.Unicode) >= 0.01);
    }

    [Fact]
    public void Category_separator()
    {
        List<NaiveLine> lines = Lines(EdgeCases.Separator);

        Assert.Contains(lines, line => !DigitsAndSeparator().IsMatch(line.TextAsString) && line.TextAsString.Contains(". ", StringComparison.Ordinal));
        Assert.Contains(lines, line => DigitsAndSeparator().IsMatch(line.TextAsString));
        Assert.Contains(lines, line => line.TextAsString.StartsWith(' '));
        Assert.True(Share(lines, EdgeCases.Separator) >= 0.01);
    }

    [Fact]
    public void Category_case()
    {
        List<NaiveLine> lines = Lines(EdgeCases.Case);
        List<string> texts = [.. lines.Select(line => line.TextAsString)];
        List<string> words = [.. texts.SelectMany(text => text.Split(' '))];

        Assert.Contains(words, word => word.All(char.IsAsciiLetterLower));
        Assert.Contains(words, word => word.Length > 1 && char.IsAsciiLetterUpper(word[0]) && word.Skip(1).All(char.IsAsciiLetterLower));
        Assert.Contains(words, word => word.Length > 1 && word.All(char.IsAsciiLetterUpper));
        Assert.Contains(texts, text => char.IsAsciiLetterLower(text[0]));
        Assert.Contains(texts, text => text.Split(' ').Skip(1).Any(word => char.IsAsciiLetterUpper(word[0])));
        Assert.True(Share(lines, EdgeCases.Case) >= 0.01);
    }

    [Fact]
    public void Category_big_numbers()
    {
        List<NaiveLine> lines = Lines(EdgeCases.BigNumbers);
        HashSet<ulong> numbers = [.. lines.Select(line => line.Number)];

        Assert.Contains(4294967296UL, numbers);
        Assert.Contains(9223372036854775807UL, numbers);
        Assert.Contains(9223372036854775808UL, numbers);
        Assert.Contains(18446744073709551615UL, numbers);

        // Every length from 10 to 20 digits occurs, not only the boundary values.
        Assert.Equal(Enumerable.Range(1, 20), lines.Select(line => line.NumberText.Length).Distinct().Order());
        Assert.True(Share(lines, EdgeCases.BigNumbers) >= 0.01);
    }

    [Fact]
    public void Category_leading_zeros()
    {
        List<NaiveLine> lines = Lines(EdgeCases.LeadingZeros);

        Assert.True(Share(lines, EdgeCases.LeadingZeros) >= 0.01);
        Assert.All(lines, line => Assert.InRange(line.NumberText.Length, 1, 20));

        // One, two and three zeros occur.
        List<int> zeros = [.. lines
            .Where(line => line.Number > 0)
            .Select(line => line.NumberText.Length - line.NumberText.TrimStart('0').Length)
            .Distinct()
            .Order()];
        Assert.Equal([0, 1, 2, 3], zeros);
    }

    [Fact]
    public void Leading_zeros_never_make_a_Number_longer_than_20_digits()
    {
        List<NaiveLine> lines = Lines(EdgeCases.LeadingZeros | EdgeCases.BigNumbers);

        Assert.All(lines, line => Assert.InRange(line.NumberText.Length, 1, 20));
        Assert.Contains(lines, line => line.NumberText.Length == 20 && line.NumberText[0] == '0');
    }

    [Fact]
    public void Category_empty()
    {
        List<NaiveLine> lines = Lines(EdgeCases.Empty);

        Assert.True(Share(lines, EdgeCases.Empty) >= 0.01);
    }

    [Fact]
    public void All_categories()
    {
        List<NaiveLine> lines = Lines(EdgeCases.All);

        Assert.All(Categories, category =>
            Assert.True(Share(lines, category) >= 0.01, $"{category}: {Share(lines, category):P2}"));
    }

    [Fact]
    public void Selected_categories_only()
    {
        List<NaiveLine> lines = Lines(EdgeCases.Empty | EdgeCases.LeadingZeros);

        Assert.True(Share(lines, EdgeCases.Empty) >= 0.01);
        Assert.True(Share(lines, EdgeCases.LeadingZeros) >= 0.01);
        Assert.All(lines, line =>
        {
            Assert.True(line.Text.Length == 0 || PlainString().IsMatch(line.TextAsString), line.TextAsString);
            Assert.True(line.Number <= 4294967295);
        });
    }

    [Fact]
    public void Each_category_alone_shows_no_other_category()
    {
        foreach (EdgeCases enabled in Categories)
        {
            List<NaiveLine> lines = Lines(enabled);

            Assert.All(
                Categories.Where(category => category != enabled),
                category => Assert.DoesNotContain(lines, line => Shows(line, category)));
        }
    }

    [Fact]
    public void Edge_cases_keep_the_number_of_words()
    {
        List<NaiveLine> lines = Lines(EdgeCases.All & ~EdgeCases.Empty, words: "3");

        Assert.All(lines, line =>
        {
            // The prefix of digits with the separator and the leading space stand in front of the words.
            string text = DigitsAndSeparator().Replace(line.TextAsString, "");
            text = text.StartsWith(' ') ? text[1..] : text;
            Assert.Equal(3, text.Split(' ').Length);
        });
    }

    [Fact]
    public void A_one_word_String_takes_a_separator_form_that_needs_no_space()
    {
        List<NaiveLine> lines = Lines(EdgeCases.Separator, words: "1");
        List<string> unusual = [.. lines.Select(line => line.TextAsString).Where(text => !PlainString().IsMatch(text))];

        Assert.NotEmpty(unusual);
        Assert.All(unusual, text => Assert.True(text.StartsWith(' ') || DigitsAndSeparator().IsMatch(text), text));
        Assert.Contains(unusual, text => text.StartsWith(' '));
        Assert.Contains(unusual, text => DigitsAndSeparator().IsMatch(text));
    }

    [Fact]
    public void Empty_wins_over_the_other_String_categories()
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.All);
        StringComposer composer = new(options);
        byte[] buffer = new byte[composer.MaxLength + StringComposer.Slack];
        Xoshiro256 random = Xoshiro256.Create(1, 0);

        Assert.Equal(0, composer.Compose(ref random, EdgeCases.All, buffer));
        Assert.Equal(0, composer.Compose(ref random, EdgeCases.Empty | EdgeCases.Unicode, buffer));
        Assert.Equal(0, composer.Compose(ref random, EdgeCases.Empty | EdgeCases.Separator | EdgeCases.Case, buffer));
    }

    [Fact]
    public void Each_category_applies_with_probability_one_in_sixteen()
    {
        Xoshiro256 random = Xoshiro256.Create(1, 0);
        int[] counts = new int[Categories.Length];
        const int Rolls = 160_000;

        for (int i = 0; i < Rolls; i++)
        {
            EdgeCases applied = EdgeRoll.Apply(random.Next(), EdgeCases.All);
            for (int category = 0; category < Categories.Length; category++)
            {
                counts[category] += applied.HasFlag(Categories[category]) ? 1 : 0;
            }
        }

        Assert.All(counts, count => Assert.InRange(count, 9_500, 10_500));
        Assert.Equal(EdgeCases.None, EdgeRoll.Apply(0, EdgeCases.None));
        Assert.Equal(EdgeCases.Case, EdgeRoll.Apply(0, EdgeCases.Case));
    }

    [Fact]
    public void The_longest_line_fits_the_declared_maximum()
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.All, words: "1024", lineEnding: LineEnding.CrLf);
        int maxLineLength = new BlockGenerator(options).MaxLineLength;

        List<NaiveLine> lines = TestContent.Lines(options, 4 * TestContent.Megabyte);

        Assert.All(lines, line =>
            Assert.True(Encoding.ASCII.GetByteCount(line.NumberText) + 2 + line.Text.Length + 2 <= maxLineLength));
    }
}
