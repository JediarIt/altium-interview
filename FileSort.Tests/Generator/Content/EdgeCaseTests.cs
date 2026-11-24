using System.Numerics;
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
        EdgeCases.CarriageReturn,
    ];

    [GeneratedRegex(@"^[A-Z][a-z]*( [a-z]+)*$")]
    private static partial Regex PlainString();

    [GeneratedRegex(@"^[0-9]+\. ")]
    private static partial Regex DigitsAndSeparator();

    private static List<NaiveLine> Lines(
        EdgeCases edgeCases, string words = "1-5", LineEnding lineEnding = LineEnding.Lf)
    {
        return TestContent.Lines(
            TestContent.Options(edgeCases: edgeCases, words: words, lineEnding: lineEnding), TestContent.Megabyte);
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
            EdgeCases.CarriageReturn => text.EndsWith('\r'),
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

        Assert.Contains(lines, line => HasSeparatorInside(line.TextAsString));
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
        Assert.Contains(words, IsCapitalized);
        Assert.Contains(words, word => word.Length > 1 && word.All(char.IsAsciiLetterUpper));
        Assert.Contains(texts, text => char.IsAsciiLetterLower(text[0]));
        Assert.Contains(texts, HasCapitalAfterTheFirstWord);
        Assert.True(Share(lines, EdgeCases.Case) >= 0.01);
    }

    [Fact]
    public void Category_big_numbers()
    {
        List<NaiveLine> lines = Lines(EdgeCases.BigNumbers);
        HashSet<BigInteger> numbers = [.. lines.Select(line => line.Number)];

        Assert.Contains((BigInteger)4294967296UL, numbers);
        Assert.Contains((BigInteger)9223372036854775807UL, numbers);
        Assert.Contains((BigInteger)9223372036854775808UL, numbers);
        Assert.Contains((BigInteger)18446744073709551615UL, numbers);
        Assert.Contains((BigInteger)18446744073709551615UL + 1, numbers);

        // Every length from 10 to 30 digits occurs, not only the boundary values: Numbers of 33 to 64 bits have 10 to
        // 20 digits, the long ones 21 to 30, all of them above the range of 64 bits.
        Assert.Equal(Enumerable.Range(1, 30), lines.Select(line => line.NumberText.Length).Distinct().Order());
        Assert.All(
            lines.Where(line => line.NumberText.Length > 20),
            line => Assert.True(line.Number > ulong.MaxValue, line.NumberText));
        Assert.True(Share(lines, EdgeCases.BigNumbers) >= 0.01);
    }

    [Fact]
    public void Category_leading_zeros()
    {
        List<NaiveLine> lines = Lines(EdgeCases.LeadingZeros);

        Assert.True(Share(lines, EdgeCases.LeadingZeros) >= 0.01);
        Assert.All(lines, line => Assert.InRange(line.NumberText.Length, 1, 13));

        // One, two and three zeros occur.
        List<int> zeros = [.. lines
            .Where(line => line.Number > 0)
            .Select(line => line.NumberText.Length - line.NumberText.TrimStart('0').Length)
            .Distinct()
            .Order()];
        Assert.Equal([0, 1, 2, 3], zeros);
    }

    [Fact]
    public void Leading_zeros_in_front_of_big_Numbers_go_past_20_digits_and_never_past_the_longest_Number()
    {
        List<NaiveLine> lines = Lines(EdgeCases.LeadingZeros | EdgeCases.BigNumbers);

        Assert.All(lines, line => Assert.InRange(line.NumberText.Length, 1, LineComposer.MaxNumberDigits));
        Assert.Contains(lines, line => line.NumberText.Length > 20 && line.NumberText[0] == '0');
        Assert.Equal(33, LineComposer.MaxNumberDigits);
    }

    [Fact]
    public void Category_carriage_return()
    {
        foreach (LineEnding lineEnding in new[] { LineEnding.Lf, LineEnding.CrLf })
        {
            List<NaiveLine> lines = Lines(EdgeCases.CarriageReturn, lineEnding: lineEnding);

            Assert.True(Share(lines, EdgeCases.CarriageReturn) >= 0.01);
            Assert.False(Shows(lines[0], EdgeCases.CarriageReturn));

            // The carriage return is the last byte of the String and appears nowhere else in it.
            Assert.All(lines, line => Assert.DoesNotContain((byte)'\r', line.Text.SkipLast(1)));
        }
    }

    [Fact]
    public void A_carriage_return_turns_an_empty_String_into_a_carriage_return()
    {
        List<NaiveLine> lines = Lines(EdgeCases.Empty | EdgeCases.CarriageReturn);

        Assert.Contains(lines, line => line.Text is [(byte)'\r']);
    }

    [Fact]
    public void Pool_Strings_get_the_carriage_return_of_their_line()
    {
        List<NaiveLine> lines = TestContent.Lines(
            TestContent.Options(duplicates: 100, edgeCases: EdgeCases.CarriageReturn), TestContent.Megabyte);

        // Every String comes from the pool; some lines add a carriage return to it, the other lines do not.
        IEnumerable<string> texts = lines.Select(line => line.TextAsString);
        HashSet<string> withoutCarriageReturn = [.. texts.Where(text => !text.EndsWith('\r'))];
        Assert.Contains(
            lines,
            line => line.TextAsString.EndsWith('\r') && withoutCarriageReturn.Contains(line.TextAsString[..^1]));
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
        IEnumerable<string> texts = lines.Select(line => line.TextAsString);
        List<string> unusual = [.. texts.Where(text => !PlainString().IsMatch(text))];

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

    [Theory]
    [InlineData("1024", LineEnding.Lf)]
    [InlineData("1024", LineEnding.CrLf)]
    [InlineData("1-5", LineEnding.Lf)]
    [InlineData("1-5", LineEnding.CrLf)]
    public void Every_line_is_valid_and_fits_the_declared_maximum(string words, LineEnding lineEnding)
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.All, words: words, lineEnding: lineEnding);
        int maxLineLength = new BlockGenerator(options).MaxLineLength;

        // The naive parser fails the test at the first line that is not valid.
        List<NaiveLine> lines = TestContent.Lines(options, 4 * TestContent.Megabyte);

        Assert.All(lines, line => Assert.True(LengthWithTerminator(line, lineEnding) <= maxLineLength));
    }

    [Fact]
    public void The_longest_line_grows_only_with_the_categories_that_lengthen_a_line()
    {
        int plain = new BlockGenerator(TestContent.Options()).MaxLineLength;

        // The bytes of plain content depend on this length, which content version 1 had too.
        Assert.Equal(plain, new BlockGenerator(TestContent.Options(edgeCases: EdgeCases.Empty)).MaxLineLength);
        Assert.Equal(
            plain + LineComposer.MaxNumberDigits - 20,
            new BlockGenerator(TestContent.Options(edgeCases: EdgeCases.LeadingZeros)).MaxLineLength);
        BlockGenerator carriageReturn = new(TestContent.Options(edgeCases: EdgeCases.CarriageReturn));
        Assert.Equal(plain + 1, carriageReturn.MaxLineLength);
    }

    // A String with the separator inside it, not at its start.
    private static bool HasSeparatorInside(string text)
    {
        return !DigitsAndSeparator().IsMatch(text) && text.Contains(". ", StringComparison.Ordinal);
    }

    // A word of two letters or more: the first in upper case, the rest in lower case.
    private static bool IsCapitalized(string word)
    {
        return word.Length > 1 && char.IsAsciiLetterUpper(word[0]) && word.Skip(1).All(char.IsAsciiLetterLower);
    }

    private static bool HasCapitalAfterTheFirstWord(string text)
    {
        return text.Split(' ').Skip(1).Any(word => char.IsAsciiLetterUpper(word[0]));
    }

    // The bytes of the line with its terminator.
    private static int LengthWithTerminator(NaiveLine line, LineEnding lineEnding)
    {
        return Encoding.ASCII.GetByteCount(line.NumberText) + 2 + line.Text.Length + lineEnding.TerminatorLength();
    }
}