using FileSort.Common;
using FileSort.Generator.Cli.Options;
using FileSort.Generator.Content;

namespace FileSort.Tests.Generator.Cli.Options;

/// <summary>
/// The values that the options of the generator alone accept and reject. Sizes and whole numbers are parsed by
/// <c>FileSort.Cli</c> and tested with it.
/// </summary>
public class ValueParserTests
{
    [Theory]
    [InlineData("1", 1, 1)]
    [InlineData("4", 4, 4)]
    [InlineData("1-5", 1, 5)]
    [InlineData("2-2", 2, 2)]
    [InlineData("1-1024", 1, 1024)]
    [InlineData("1024", 1024, 1024)]
    public void Word_ranges(string text, int min, int max)
    {
        Assert.True(WordRangeParser.TryParse(text, out WordRange range, out string? error), error);
        Assert.Equal(new WordRange(min, max), range);
        Assert.True(WordRangeParser.TryParse(range.ToString(), out WordRange again, out _));
        Assert.Equal(range, again);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("0-5")]
    [InlineData("5-1")]
    [InlineData("1025")]
    [InlineData("1-1025")]
    [InlineData("1-")]
    [InlineData("-5")]
    [InlineData("1-2-3")]
    [InlineData("a")]
    [InlineData("1.5")]
    [InlineData("1 - 5")]
    public void Word_ranges_outside_the_accepted_form_are_rejected(string text)
    {
        Assert.False(WordRangeParser.TryParse(text, out _, out string? error));
        Assert.Contains("1-5", error);
    }

    [Theory]
    [InlineData("unicode", (int)EdgeCases.Unicode)]
    [InlineData("separator", (int)EdgeCases.Separator)]
    [InlineData("case", (int)EdgeCases.Case)]
    [InlineData("big-numbers", (int)EdgeCases.BigNumbers)]
    [InlineData("leading-zeros", (int)EdgeCases.LeadingZeros)]
    [InlineData("empty", (int)EdgeCases.Empty)]
    [InlineData("carriage-return", (int)EdgeCases.CarriageReturn)]
    [InlineData("all", (int)EdgeCases.All)]
    [InlineData("empty,leading-zeros", (int)(EdgeCases.Empty | EdgeCases.LeadingZeros))]
    [InlineData("case,case", (int)EdgeCases.Case)]
    [InlineData("case,all", (int)EdgeCases.All)]
    public void Edge_case_lists(string text, int expected)
    {
        Assert.True(EdgeCaseNames.TryParse(text, out EdgeCases categories, out string? error), error);
        Assert.Equal((EdgeCases)expected, categories);
        Assert.True(EdgeCaseNames.TryParse(EdgeCaseNames.Format(categories), out EdgeCases again, out _));
        Assert.Equal(categories, again);
    }

    [Theory]
    [InlineData("")]
    [InlineData("emojis")]
    [InlineData("Unicode")]
    [InlineData("unicode,")]
    [InlineData("unicode case")]
    [InlineData("none")]
    public void Unknown_edge_case_names_are_rejected(string text)
    {
        Assert.False(EdgeCaseNames.TryParse(text, out _, out string? error));
        Assert.Contains("big-numbers", error);
    }

    [Fact]
    public void Line_endings()
    {
        Assert.Equal(LineEnding.Lf, LineEndingNames.Parse("lf"));
        Assert.Equal(LineEnding.CrLf, LineEndingNames.Parse("crlf"));
        Assert.Equal("lf", LineEndingNames.Format(LineEnding.Lf));
        Assert.Equal("crlf", LineEndingNames.Format(LineEnding.CrLf));
        Assert.Throws<ArgumentException>(() => LineEndingNames.Parse("cr"));
    }
}
