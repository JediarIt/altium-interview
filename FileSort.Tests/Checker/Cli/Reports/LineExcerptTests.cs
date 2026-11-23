using System.Text;

using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Cli.Reports;

namespace FileSort.Tests.Checker.Cli.Reports;

public class LineExcerptTests
{
    private static string Format(byte[] line) => LineExcerpt.Format(LineSample.Of(line));

    private static string Format(string line) => Format(Encoding.UTF8.GetBytes(line));

    [Fact]
    public void Text_is_shown_as_it_is()
    {
        Assert.Equal("415. Apple", Format("415. Apple"));
        Assert.Equal("5. Ёлка 日本 😀", Format("5. Ёлка 日本 😀"));
        Assert.Equal("", Format(""));
    }

    [Theory]
    [InlineData("1. A\rB", @"1. A\rB")]
    [InlineData("2. Banana\n", @"2. Banana\n")]
    [InlineData("1. A\tB", @"1. A\tB")]
    [InlineData(@"1. C:\data", @"1. C:\\data")]
    [InlineData("1. A\0B\u001B", @"1. A\x00B\x1B")]
    [InlineData("1. A\u007F", @"1. A\x7F")]
    [InlineData("1. A\u0085B", @"1. A\xC2\x85B")]
    public void Bytes_that_would_not_show_are_escaped(string line, string expected)
    {
        Assert.Equal(expected, Format(line));
    }

    [Fact]
    public void A_byte_order_mark_is_shown_as_its_bytes()
    {
        Assert.Equal(@"\xEF\xBB\xBF2. Banana", Format([0xEF, 0xBB, 0xBF, .. "2. Banana"u8]));
    }

    [Theory]
    [InlineData(new byte[] { 0x35, 0x2E, 0x20, 0xC3, 0x28 }, @"5. \xC3(")]
    [InlineData(new byte[] { 0x35, 0x2E, 0x20, 0xFF, 0x41 }, @"5. \xFFA")]
    [InlineData(new byte[] { 0x35, 0x2E, 0x20, 0xE2, 0x82 }, @"5. \xE2\x82")]
    [InlineData(new byte[] { 0x35, 0x2E, 0x20, 0xED, 0xA0, 0x80 }, @"5. \xED\xA0\x80")]
    public void Bytes_that_are_not_well_formed_UTF8_are_escaped(byte[] line, string expected)
    {
        Assert.Equal(expected, Format(line));
    }

    [Fact]
    public void A_long_line_is_cut_and_its_length_is_given()
    {
        string line = "1. " + new string('x', 1000);

        Assert.Equal("1. " + new string('x', 197) + "... (1003 bytes)", Format(line));
    }

    [Fact]
    public void A_line_of_exactly_the_shown_length_is_not_cut()
    {
        string line = "1. " + new string('x', LineSample.MaxBytes - 3);

        Assert.Equal(line, Format(line));
    }

    [Fact]
    public void A_character_cut_by_the_excerpt_is_left_out()
    {
        // The two-byte letter lies across the edge of what is kept of the line: its first byte is the last one kept.
        string line = "1. " + new string('x', LineSample.MaxBytes - 4) + "Ёлка";

        string shown = Format(line);

        Assert.Equal("1. " + new string('x', LineSample.MaxBytes - 4) + $"... ({Encoding.UTF8.GetByteCount(line)} bytes)", shown);
        Assert.DoesNotContain(@"\x", shown);
    }

    [Fact]
    public void A_character_cut_at_the_end_of_the_line_itself_is_shown_as_broken()
    {
        // The line ends inside a character: that is what is wrong with it, and the report must show it.
        Assert.Equal(@"5. Apple \xD0", Format([.. "5. Apple "u8, 0xD0]));
    }
}
