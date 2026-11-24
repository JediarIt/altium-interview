using FileSort.Cli.Options;

namespace FileSort.Tests.Cli.Options;

/// <summary>The whole numbers that the numeric options of every program accept and reject.</summary>
public class WholeNumberParserTests
{
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
