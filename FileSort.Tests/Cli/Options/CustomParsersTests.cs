using System.CommandLine;
using System.CommandLine.Parsing;

using FileSort.Cli.Options;

namespace FileSort.Tests.Cli.Options;

/// <summary>The custom parsers that the options of every program are built with.</summary>
public class CustomParsersTests
{
    private static ParseResult Parse(Option option, params string[] args)
    {
        return new RootCommand { option }.Parse(args);
    }

    [Fact]
    public void A_whole_number_in_its_range_is_the_value()
    {
        Option<int> threads = new("--threads") { CustomParser = CustomParsers.WholeNumber(1, 64) };

        ParseResult result = Parse(threads, "--threads", "64");

        Assert.Empty(result.Errors);
        Assert.Equal(64, result.GetValue(threads));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65")]
    [InlineData("many")]
    [InlineData("-1")]
    public void A_whole_number_out_of_its_range_is_an_error_that_quotes_it(string text)
    {
        Option<int> threads = new("--threads") { CustomParser = CustomParsers.WholeNumber(1, 64) };

        ParseResult result = Parse(threads, "--threads", text);

        ParseError error = Assert.Single(result.Errors);
        Assert.Equal($"'{text}' is not accepted. Expected a whole number from 1 to 64.", error.Message);
    }

    [Fact]
    public void A_value_parser_becomes_the_parser_of_an_option()
    {
        Option<long> size = new("--size") { CustomParser = CustomParsers.From<long>(SizeParser.TryParse) };

        Assert.Equal(1610612736, Parse(size, "--size", "1.5GB").GetValue(size));
        ParseError error = Assert.Single(Parse(size, "--size", "lots").Errors);
        Assert.Contains("'lots' is not a size", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_range_that_cannot_hold_a_number_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CustomParsers.WholeNumber(-1, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => CustomParsers.WholeNumber(5, 4));
    }
}
