using FileSort.Cli.Options;

namespace FileSort.Tests.Cli.Options;

/// <summary>
/// The sizes that the programs accept: the generator's <c>--size</c> with its scenario "Units and fractions", and
/// the sorter's <c>--memory</c>.
/// </summary>
public class SizeParserTests
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
}
