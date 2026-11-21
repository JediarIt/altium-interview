using FileSort.Common;
using FileSort.Generator.Cli.Options;
using FileSort.Generator.Content;

namespace FileSort.Tests.Generator.Cli.Options;

/// <summary>The values each option accepts and rejects, and the scenario "Units and fractions".</summary>
public class ValueParserTests
{
    [Theory]
    [InlineData("1048576", 1048576)]
    [InlineData("500mb", 524288000)]
    [InlineData("1GB", 1073741824)]
    [InlineData("1.5GB", 1610612736)]
    [InlineData("0.1KB", 103)]
    public void Units_and_fractions(string text, long expected)
    {
        Assert.True(SizeParser.TryParse(text, out long bytes, out string? error), error);
        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1B", 1)]
    [InlineData("1b", 1)]
    [InlineData("1KB", 1L << 10)]
    [InlineData("1KiB", 1L << 10)]
    [InlineData("1kib", 1L << 10)]
    [InlineData("1MB", 1L << 20)]
    [InlineData("1MiB", 1L << 20)]
    [InlineData("1GiB", 1L << 30)]
    [InlineData("1TB", 1L << 40)]
    [InlineData("1TiB", 1L << 40)]
    [InlineData("100gb", 100L << 30)]
    [InlineData("0.5", 1)]
    [InlineData("0.000001GB", 1074)]
    [InlineData("2.50MB", 2621440)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("8388607.99999999TB", 9223372036854764813)]
    public void Sizes_use_binary_units_and_round_up(string text, long expected)
    {
        Assert.True(SizeParser.TryParse(text, out long bytes, out string? error), error);
        Assert.Equal(expected, bytes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("0.0MB")]
    [InlineData("0GB")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("10XB")]
    [InlineData("GB")]
    [InlineData("1,5GB")]
    [InlineData("1e3MB")]
    [InlineData("1E3")]
    [InlineData("1.GB")]
    [InlineData(".5GB")]
    [InlineData("1.2.3GB")]
    [InlineData("1 GB")]
    [InlineData(" 1GB")]
    [InlineData("1GBB")]
    [InlineData("1PB")]
    [InlineData("9223372036854775808")]
    [InlineData("8388608TB")]
    [InlineData("99999999999999999999999999999999999999TB")]
    public void Sizes_outside_the_accepted_form_are_rejected(string text)
    {
        Assert.False(SizeParser.TryParse(text, out _, out string? error));
        Assert.Contains(text, error);
    }

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

    [Theory]
    [InlineData("0", 0UL)]
    [InlineData("42", 42UL)]
    [InlineData("007", 7UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void Whole_numbers(string text, ulong expected)
    {
        Assert.True(WholeNumberParser.TryParse(text, 0, ulong.MaxValue, out ulong value, out string? error), error);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData(" 1")]
    [InlineData("1,000")]
    [InlineData("0x10")]
    [InlineData("18446744073709551616")]
    public void Whole_numbers_take_digits_only(string text)
    {
        Assert.False(WholeNumberParser.TryParse(text, 0, ulong.MaxValue, out _, out string? error));
        Assert.Contains("whole number", error);
    }

    [Theory]
    [InlineData("0", 0, 100, true)]
    [InlineData("100", 0, 100, true)]
    [InlineData("101", 0, 100, false)]
    [InlineData("1", 1, 256, true)]
    [InlineData("256", 1, 256, true)]
    [InlineData("0", 1, 256, false)]
    [InlineData("257", 1, 256, false)]
    public void Whole_numbers_respect_their_range(string text, ulong min, ulong max, bool accepted)
    {
        Assert.Equal(accepted, WholeNumberParser.TryParse(text, min, max, out _, out _));
    }
}
