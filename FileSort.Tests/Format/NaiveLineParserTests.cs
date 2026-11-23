using System.Globalization;
using System.Numerics;
using System.Text;

namespace FileSort.Tests.Format;

/// <summary>
/// One test per scenario of the <c>line-format</c> spec, run against the naive parser, so that the oracle used by the
/// other tests is itself checked against the spec.
/// </summary>
public class NaiveLineParserTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static NaiveLine? Parse(string line) => NaiveLineParser.ParseLine(Utf8(line));

    // Requirement: Line structure

    [Fact]
    public void Plain_line()
    {
        NaiveLine? line = Parse("415. Apple");

        Assert.NotNull(line);
        Assert.Equal(415, line.Number);
        Assert.Equal("Apple", line.TextAsString);
    }

    [Fact]
    public void Separator_inside_the_String()
    {
        NaiveLine? line = Parse("12. 3. Apple");

        Assert.NotNull(line);
        Assert.Equal(12, line.Number);
        Assert.Equal("3. Apple", line.TextAsString);
    }

    [Fact]
    public void Extra_space_after_the_separator()
    {
        NaiveLine? line = Parse("5.  Apple");

        Assert.NotNull(line);
        Assert.Equal(" Apple", line.TextAsString);
    }

    [Theory]
    [InlineData("Apple")]
    [InlineData("12.Apple")]
    [InlineData("12 Apple")]
    public void No_separator(string line)
    {
        Assert.Null(Parse(line));
    }

    // Requirement: Number

    [Fact]
    public void Zero()
    {
        Assert.Equal(BigInteger.Zero, Parse("0. Apple")?.Number);
    }

    [Fact]
    public void Largest_Number()
    {
        Assert.Equal((BigInteger)ulong.MaxValue, Parse("18446744073709551615. Apple")?.Number);
    }

    [Fact]
    public void Number_above_the_range()
    {
        NaiveLine? above = Parse("18446744073709551616. Apple");

        Assert.NotNull(above);
        Assert.Equal(Parse("18446744073709551615. Apple")!.Number + 1, above.Number);
    }

    [Fact]
    public void Leading_zeros()
    {
        Assert.Equal((BigInteger)7, Parse("007. Apple")?.Number);
        Assert.Equal(Parse("7. Apple")?.Number, Parse("007. Apple")?.Number);
    }

    [Fact]
    public void More_than_20_digits()
    {
        NaiveLine? zeros = Parse("000000000000000000007. Apple");
        NaiveLine? long40 = Parse("1234567890123456789012345678901234567890. Apple");

        Assert.Equal(21, zeros?.NumberText.Length);
        Assert.Equal((BigInteger)7, zeros?.Number);
        Assert.Equal(40, long40?.NumberText.Length);
        Assert.Equal(BigInteger.Parse("1234567890123456789012345678901234567890", CultureInfo.InvariantCulture), long40?.Number);
    }

    [Theory]
    [InlineData("-5. Apple")]
    [InlineData("+5. Apple")]
    [InlineData(" 5. Apple")]
    [InlineData(". Apple")]
    public void Sign_space_or_missing_digits(string line)
    {
        Assert.Null(Parse(line));
    }

    // Requirement: String

    [Fact]
    public void Empty_String()
    {
        NaiveLine line = Assert.Single(NaiveLineParser.ParseValidFile(Utf8("5. \n"), "\n"));

        Assert.Empty(line.Text);
    }

    [Fact]
    public void Non_ASCII_String()
    {
        Assert.Equal("Ёлка 日本 😀", Parse("5. Ёлка 日本 😀")?.TextAsString);
    }

    [Fact]
    public void Case_and_spaces_are_significant()
    {
        string[] texts = [Parse("5. Apple")!.TextAsString, Parse("5. apple")!.TextAsString, Parse("5. Apple ")!.TextAsString];

        Assert.Equal(3, texts.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Ill_formed_UTF8()
    {
        byte[] line = [.. Utf8("5. "), 0xC3, 0x28];

        Assert.Null(NaiveLineParser.ParseLine(line));
    }

    // Requirement: Line terminator

    [Fact]
    public void Line_feed()
    {
        List<NaiveLine> lines = NaiveLineParser.ParseValidFile(Utf8("1. Apple\n2. Banana\n"), "\n");

        Assert.Equal(["Apple", "Banana"], lines.Select(l => l.TextAsString));
    }

    [Fact]
    public void Carriage_return_and_line_feed()
    {
        List<NaiveLine> lines = NaiveLineParser.ParseValidFile(Utf8("1. Apple\r\n2. Banana\r\n"), "\r\n");

        Assert.Equal(["Apple", "Banana"], lines.Select(l => l.TextAsString));
    }

    [Fact]
    public void Last_line_without_a_terminator()
    {
        List<NaiveLine> lines = NaiveLineParser.ParseValidFile(Utf8("1. Apple\n2. Banana"), "\n");

        Assert.Equal(["Apple", "Banana"], lines.Select(l => l.TextAsString));
    }

    [Fact]
    public void Carriage_return_inside_a_String()
    {
        NaiveLine line = Assert.Single(NaiveLineParser.ParseValidFile(Utf8("1. A\rB\n"), "\n"));

        Assert.Equal("A\rB"u8.ToArray(), line.Text);
    }

    [Fact]
    public void Carriage_return_at_the_end_of_a_String()
    {
        NaiveLine line = Assert.Single(NaiveLineParser.ParseValidFile(Utf8("1. Apple\r\n"), "\n"));

        Assert.Equal("Apple\r"u8.ToArray(), line.Text);
    }

    [Fact]
    public void Carriage_return_before_the_terminator()
    {
        NaiveLine line = Assert.Single(NaiveLineParser.ParseValidFile(Utf8("1. Apple\r\r\n"), "\r\n"));

        Assert.Equal("Apple\r"u8.ToArray(), line.Text);
    }

    [Fact]
    public void Line_feed_without_a_carriage_return()
    {
        byte[] file = Utf8("1. Apple\r\n2. Banana\n3. Cherry\r\n");

        List<byte[]> lines = NaiveLineParser.SplitLines(file, "\r\n");
        List<NaiveLine?> parsed = NaiveLineParser.ParseFile(file, "\r\n");

        Assert.Equal(2, lines.Count);
        Assert.Equal(Utf8("2. Banana\n3. Cherry"), lines[1]);
        Assert.NotNull(parsed[0]);
        Assert.Null(parsed[1]);
    }

    // Requirement: File structure

    [Fact]
    public void Empty_file()
    {
        Assert.Empty(NaiveLineParser.ParseFile([], "\n"));
    }

    [Fact]
    public void Empty_line()
    {
        List<NaiveLine?> lines = NaiveLineParser.ParseFile(Utf8("1. Apple\n\n2. Banana\n"), "\n");

        Assert.Equal(3, lines.Count);
        Assert.NotNull(lines[0]);
        Assert.Null(lines[1]);
        Assert.NotNull(lines[2]);
    }

    [Fact]
    public void Byte_order_mark_at_the_start()
    {
        byte[] file = [0xEF, 0xBB, 0xBF, .. Utf8("1. Apple\n")];

        NaiveLine line = Assert.Single(NaiveLineParser.ParseValidFile(file, "\n"));

        Assert.Equal(1, line.Number);
        Assert.Equal("Apple", line.TextAsString);
    }

    [Fact]
    public void Byte_order_mark_and_nothing_else()
    {
        Assert.Empty(NaiveLineParser.ParseFile([0xEF, 0xBB, 0xBF], "\n"));
    }

    [Fact]
    public void Byte_order_mark_at_the_start_of_a_later_line()
    {
        byte[] file = [.. Utf8("1. Apple\n"), 0xEF, 0xBB, 0xBF, .. Utf8("2. Banana\n")];

        List<NaiveLine?> lines = NaiveLineParser.ParseFile(file, "\n");

        Assert.Equal(2, lines.Count);
        Assert.NotNull(lines[0]);
        Assert.Null(lines[1]);
    }
}
