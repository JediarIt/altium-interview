using System.Text;

using FileSort.Checker.Lines;
using FileSort.Generator.Content;
using FileSort.Tests.Format;
using FileSort.Tests.Generator;

using CommonLineEnding = FileSort.Common.LineEnding;

namespace FileSort.Tests.Checker.Lines;

/// <summary>
/// The checker's parser against every scenario of "Line structure", "Number" and "String" of the
/// <c>line-format</c> spec, and against the naive parser of the tests on generated lines.
/// </summary>
public class LineParserTests
{
    private sealed record Parsed(LineFault Fault, string Number, byte[] Text)
    {
        public bool IsValid => Fault == LineFault.None;

        public string TextAsString => Encoding.UTF8.GetString(Text);
    }

    private static Parsed Parse(byte[] line)
    {
        LineFault fault = LineParser.Parse(line, out int numberLength);
        return fault == LineFault.None
            ? new Parsed(fault, Encoding.ASCII.GetString(LineParser.Number(line, numberLength)), LineParser.Text(line, numberLength).ToArray())
            : new Parsed(fault, "", []);
    }

    private static Parsed Parse(string line) => Parse(Encoding.UTF8.GetBytes(line));

    private static int CompareNumbers(string x, string y)
    {
        return Math.Sign(LineComparer.CompareNumbers(Encoding.ASCII.GetBytes(x), Encoding.ASCII.GetBytes(y)));
    }

    // Requirement: Line structure

    [Fact]
    public void Plain_line()
    {
        Parsed line = Parse("415. Apple");

        Assert.True(line.IsValid);
        Assert.Equal("415", line.Number);
        Assert.Equal("Apple", line.TextAsString);
    }

    [Fact]
    public void Separator_inside_the_String()
    {
        Parsed line = Parse("12. 3. Apple");

        Assert.True(line.IsValid);
        Assert.Equal("12", line.Number);
        Assert.Equal("3. Apple", line.TextAsString);
    }

    [Fact]
    public void Extra_space_after_the_separator()
    {
        Parsed line = Parse("5.  Apple");

        Assert.True(line.IsValid);
        Assert.Equal(" Apple", line.TextAsString);
    }

    [Theory]
    [InlineData("Apple", nameof(LineFault.NoNumber))]
    [InlineData("12.Apple", nameof(LineFault.NoSeparator))]
    [InlineData("12 Apple", nameof(LineFault.NoSeparator))]
    public void No_separator(string line, string expected)
    {
        Assert.Equal(Enum.Parse<LineFault>(expected), Parse(line).Fault);
    }

    // Requirement: Number

    [Fact]
    public void Zero()
    {
        Parsed line = Parse("0. Apple");

        Assert.True(line.IsValid);
        Assert.Equal("0", line.Number);
        Assert.Equal(0, CompareNumbers(line.Number, "000"));
    }

    [Fact]
    public void Largest_Number()
    {
        Assert.True(Parse("18446744073709551615. Apple").IsValid);
    }

    [Fact]
    public void Number_above_the_range()
    {
        Parsed above = Parse("18446744073709551616. Apple");
        Parsed largest = Parse("18446744073709551615. Apple");

        Assert.True(above.IsValid);
        Assert.Equal(1, CompareNumbers(above.Number, largest.Number));

        // Greater by one: nothing fits between the two.
        Assert.Equal(
            NaiveLineParser.ParseLine(Encoding.ASCII.GetBytes("18446744073709551615. Apple"))!.Number + 1,
            NaiveLineParser.ParseLine(Encoding.ASCII.GetBytes("18446744073709551616. Apple"))!.Number);
    }

    [Fact]
    public void Leading_zeros()
    {
        Parsed padded = Parse("007. Apple");

        Assert.True(padded.IsValid);
        Assert.Equal("007", padded.Number);
        Assert.Equal(0, CompareNumbers(padded.Number, Parse("7. Apple").Number));
    }

    [Fact]
    public void More_than_20_digits()
    {
        Parsed zeros = Parse("000000000000000000007. Apple");
        Parsed long40 = Parse("1234567890123456789012345678901234567890. Apple");

        Assert.True(zeros.IsValid);
        Assert.Equal(21, zeros.Number.Length);
        Assert.Equal(0, CompareNumbers(zeros.Number, "7"));
        Assert.True(long40.IsValid);
        Assert.Equal(40, long40.Number.Length);
    }

    [Theory]
    [InlineData("-5. Apple")]
    [InlineData("+5. Apple")]
    [InlineData(" 5. Apple")]
    [InlineData(". Apple")]
    public void Sign_space_or_missing_digits(string line)
    {
        Assert.Equal(LineFault.NoNumber, Parse(line).Fault);
    }

    // Requirement: String

    [Fact]
    public void Empty_String()
    {
        Parsed line = Parse("5. ");

        Assert.True(line.IsValid);
        Assert.Empty(line.Text);
    }

    [Fact]
    public void Non_ASCII_String()
    {
        Parsed line = Parse("5. Ёлка 日本 😀");

        Assert.True(line.IsValid);
        Assert.Equal("Ёлка 日本 😀", line.TextAsString);
    }

    [Fact]
    public void Case_and_spaces_are_significant()
    {
        byte[][] texts = [Parse("5. Apple").Text, Parse("5. apple").Text, Parse("5. Apple ").Text];

        Assert.NotEqual(0, LineComparer.CompareStrings(texts[0], texts[1]));
        Assert.NotEqual(0, LineComparer.CompareStrings(texts[0], texts[2]));
        Assert.NotEqual(0, LineComparer.CompareStrings(texts[1], texts[2]));
    }

    [Fact]
    public void Ill_formed_UTF8()
    {
        byte[] line = [.. "5. "u8, 0xC3, 0x28];

        Assert.Equal(LineFault.IllFormedString, Parse(line).Fault);
    }

    // The reasons that the report names, beyond the scenarios

    [Fact]
    public void An_empty_line_is_not_valid()
    {
        Assert.Equal(LineFault.Empty, Parse("").Fault);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12.")]
    [InlineData("12 .")]
    [InlineData("12a. Apple")]
    [InlineData("1 2. Apple")]
    [InlineData("12.\tApple")]
    public void Digits_that_are_not_followed_by_the_separator(string line)
    {
        Assert.Equal(LineFault.NoSeparator, Parse(line).Fault);
    }

    [Fact]
    public void A_byte_order_mark_in_front_of_a_line_is_not_a_digit()
    {
        byte[] line = [0xEF, 0xBB, 0xBF, .. "2. Banana"u8];

        Assert.Equal(LineFault.NoNumber, Parse(line).Fault);
    }

    [Theory]
    [InlineData(new byte[] { 0xD0 })] // The first byte of a two-byte character, cut off.
    [InlineData(new byte[] { 0xE2, 0x82 })] // Two bytes of a three-byte character.
    [InlineData(new byte[] { 0xC0, 0xAF })] // An overlong encoding of '/'.
    [InlineData(new byte[] { 0xED, 0xA0, 0x80 })] // A surrogate, which UTF-8 does not encode.
    [InlineData(new byte[] { 0xF5, 0x80, 0x80, 0x80 })] // Above the last code point.
    [InlineData(new byte[] { 0x80 })] // A continuation byte with nothing to continue.
    [InlineData(new byte[] { 0xFF })]
    public void A_String_that_is_not_well_formed_UTF8(byte[] text)
    {
        byte[] line = [.. "5. Apple "u8, .. text];

        Assert.Equal(LineFault.IllFormedString, Parse(line).Fault);
    }

    [Fact]
    public void The_Number_is_not_checked_as_UTF8_and_the_String_may_contain_any_valid_character()
    {
        // Control characters, a carriage return and the separator again are bytes of the String like any other.
        Parsed line = Parse("7. A\rB\t. 9. \u0000");

        Assert.True(line.IsValid);
        Assert.Equal("A\rB\t. 9. \u0000", line.TextAsString);
    }

    // Against the naive parser

    [Theory]
    [InlineData(CommonLineEnding.Lf)]
    [InlineData(CommonLineEnding.CrLf)]
    public void It_agrees_with_the_naive_parser_on_generated_lines(CommonLineEnding lineEnding)
    {
        byte[] content = TestContent.Generate(
            TestContent.Options(seed: 3, edgeCases: EdgeCases.All, lineEnding: lineEnding), TestContent.Megabyte);

        List<byte[]> lines = NaiveLineParser.SplitLines(content, lineEnding == CommonLineEnding.CrLf ? "\r\n" : "\n");

        Assert.True(lines.Count > 10_000);
        foreach (byte[] line in lines)
        {
            NaiveLine? naive = NaiveLineParser.ParseLine(line);
            Parsed parsed = Parse(line);

            Assert.NotNull(naive);
            Assert.True(parsed.IsValid, Encoding.UTF8.GetString(line));
            Assert.Equal(naive.NumberText, parsed.Number);
            Assert.Equal(naive.Text, parsed.Text);
        }
    }

    [Fact]
    public void It_agrees_with_the_naive_parser_on_damaged_lines()
    {
        byte[] content = TestContent.Generate(TestContent.Options(seed: 4, edgeCases: EdgeCases.All), 256 * 1024);
        List<byte[]> lines = NaiveLineParser.SplitLines(content, "\n");
        Random random = new(1);
        int invalid = 0;

        foreach (byte[] line in lines)
        {
            byte[] damaged = Damage(line, random);

            NaiveLine? naive = NaiveLineParser.ParseLine(damaged);
            Parsed parsed = Parse(damaged);

            Assert.True(naive is not null == parsed.IsValid, Encoding.Latin1.GetString(damaged));
            if (naive is null)
            {
                invalid++;
                continue;
            }

            Assert.Equal(naive.NumberText, parsed.Number);
            Assert.Equal(naive.Text, parsed.Text);
        }

        // The damage makes a good share of the lines invalid, and leaves a good share valid.
        Assert.InRange(invalid, lines.Count / 10, lines.Count * 9 / 10);
    }

    // One small change at a random place: a byte removed, replaced or inserted, or the line cut.
    private static byte[] Damage(byte[] line, Random random)
    {
        // Bytes that matter to the format, and bytes that break UTF-8. No line feed: a line has none.
        ReadOnlySpan<byte> pool = [(byte)'.', (byte)' ', (byte)'7', (byte)'x', (byte)'-', 0xC3, 0xFF, 0x80];
        int at = random.Next(line.Length);
        byte value = pool[random.Next(pool.Length)];
        return random.Next(4) switch
        {
            0 => [.. line[..at], .. line[(at + 1)..]],
            1 => [.. line[..at], value, .. line[(at + 1)..]],
            2 => [.. line[..at], value, .. line[at..]],
            _ => line[..at],
        };
    }
}
