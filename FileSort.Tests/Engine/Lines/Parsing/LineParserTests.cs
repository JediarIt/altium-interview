using System.Text;

using FileSort.Common;
using FileSort.Engine.Lines;
using FileSort.Engine.Lines.Parsing;
using FileSort.Generator.Content;
using FileSort.Tests.Format;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Engine.Lines.Parsing;

/// <summary>
/// The engine's parser against the scenarios of "Line structure", "Number" and "String" of <c>line-format</c>, and
/// against the naive parser of the tests on generated content with every edge case. The values of the Numbers are
/// compared by the comparison, from their digits; see <see cref="EntryComparerTests"/>.
/// </summary>
public class LineParserTests
{
    private static LineFault Parse(string line, out IndexEntry entry)
    {
        return LineParser.Parse(Encoding.UTF8.GetBytes(line), 0, out entry);
    }

    private static IndexEntry Valid(string line)
    {
        Assert.Equal(LineFault.None, Parse(line, out IndexEntry entry));
        return entry;
    }

    // The Number and the String of a valid line, read back through its entry.
    private static (string Number, string Text) Parts(string line)
    {
        IndexEntry entry = Valid(line);
        byte[] bytes = Encoding.UTF8.GetBytes(line);
        int digits = EntryShape.StoredNumberLength(entry.Shape);
        int start = digits + LineFormat.SeparatorLength;
        string number = Encoding.ASCII.GetString(bytes, 0, digits);
        string text = Encoding.UTF8.GetString(bytes, start, entry.LineLength - start);
        return (number, text);
    }

    // Requirement: Line structure

    [Fact]
    public void Plain_line()
    {
        Assert.Equal(("415", "Apple"), Parts("415. Apple"));
        Assert.Equal(10, Valid("415. Apple").LineLength);
    }

    [Fact]
    public void Separator_inside_the_String()
    {
        Assert.Equal(("12", "3. Apple"), Parts("12. 3. Apple"));
    }

    [Fact]
    public void Extra_space_after_the_separator()
    {
        Assert.Equal(("5", " Apple"), Parts("5.  Apple"));
    }

    [Theory]
    [InlineData("Apple", LineFault.NoNumber)]
    [InlineData("12.Apple", LineFault.NoSeparator)]
    [InlineData("12 Apple", LineFault.NoSeparator)]
    [InlineData("12", LineFault.NoSeparator)]
    [InlineData("12.", LineFault.NoSeparator)]
    public void No_separator(string line, LineFault expected)
    {
        Assert.Equal(expected, Parse(line, out _));
    }

    // Requirement: Number

    [Theory]
    [InlineData("0. Apple", "0")]
    [InlineData("18446744073709551615. Apple", "18446744073709551615")]
    [InlineData("18446744073709551616. Apple", "18446744073709551616")]
    [InlineData("007. Apple", "007")]
    [InlineData("000000000000000000007. Apple", "000000000000000000007")]
    [InlineData("1234567890123456789012345678901234567890. Apple", "1234567890123456789012345678901234567890")]
    public void A_Number_of_any_value_and_length_is_its_digits(string line, string number)
    {
        Assert.Equal(number, Parts(line).Number);
    }

    [Theory]
    [InlineData("-5. Apple")]
    [InlineData("+5. Apple")]
    [InlineData(" 5. Apple")]
    [InlineData(". Apple")]
    public void Sign_space_or_missing_digits(string line)
    {
        Assert.Equal(LineFault.NoNumber, Parse(line, out _));
    }

    // Requirement: String

    [Fact]
    public void Empty_String()
    {
        Assert.Equal(("5", ""), Parts("5. "));
        Assert.Equal(0UL, Valid("5. ").Prefix);
    }

    [Fact]
    public void Non_ASCII_String()
    {
        Assert.Equal("Ёлка 日本 😀", Parts("5. Ёлка 日本 😀").Text);
    }

    [Fact]
    public void Case_and_spaces_are_significant()
    {
        ulong[] prefixes = [Valid("5. Apple").Prefix, Valid("5. apple").Prefix, Valid("5. Apple ").Prefix];

        Assert.Equal(3, prefixes.Distinct().Count());
    }

    [Fact]
    public void Ill_formed_UTF8()
    {
        Assert.Equal(LineFault.IllFormedUtf8, LineParser.Parse([.. "5. "u8, 0xC3, 0x28], 0, out _));
    }

    [Fact]
    public void Empty_line()
    {
        Assert.Equal(LineFault.Empty, LineParser.Parse([], 0, out _));
    }

    // What the entry holds beyond the scenarios

    [Fact]
    public void The_entry_holds_the_offset_and_the_prefix_of_the_String()
    {
        LineParser.Parse("415. Apple pie"u8, 100, out IndexEntry entry);

        Assert.Equal(100U, entry.Offset);
        Assert.Equal(0x4170706C65207069UL, entry.Prefix);
        Assert.Equal(EntryShape.Pack(14, 3), entry.Shape);
    }

    [Fact]
    public void A_valid_line_parses_to_the_same_entry_without_the_checks()
    {
        string[] lines = ["415. Apple", "5. ", "007. Ёлка", "18446744073709551616. Apple pie", "1. 3. B"];
        foreach (string line in lines)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            LineParser.Parse(bytes, 7, out IndexEntry checkedEntry);

            Assert.Equal(checkedEntry, LineParser.ParseValid(bytes, 7));
        }
    }

    // Against the naive parser

    [Theory]
    [InlineData(LineEnding.Lf)]
    [InlineData(LineEnding.CrLf)]
    public void The_parser_agrees_with_the_naive_parser_on_every_generated_line(LineEnding lineEnding)
    {
        byte[] content = TestContent.Generate(
            TestContent.Options(edgeCases: EdgeCases.All, lineEnding: lineEnding), TestContent.Megabyte);
        List<byte[]> lines = NaiveLineParser.SplitLines(content, lineEnding == LineEnding.CrLf ? "\r\n" : "\n");

        foreach (byte[] line in lines)
        {
            NaiveLine expected = NaiveLineParser.ParseLine(line)!;
            Assert.Equal(LineFault.None, LineParser.Parse(line, 0, out IndexEntry entry));

            int digits = EntryShape.StoredNumberLength(entry.Shape);
            Assert.Equal(expected.NumberText, Encoding.ASCII.GetString(line, 0, digits));
            Assert.Equal(expected.Text, line[(digits + 2)..]);
            Assert.Equal(line.Length, entry.LineLength);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("Apple")]
    [InlineData("12.Apple")]
    [InlineData("12 Apple")]
    [InlineData("-5. Apple")]
    [InlineData("1a. Apple")]
    [InlineData("﻿1. Apple")]
    [InlineData("5")]
    [InlineData("5. Apple")]
    [InlineData("5. ")]
    [InlineData("99999999999999999999999. A")]
    public void Validity_agrees_with_the_naive_parser(string line)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(line);

        Assert.Equal(NaiveLineParser.ParseLine(bytes) is not null, LineParser.Parse(bytes, 0, out _) == LineFault.None);
    }

    [Fact]
    public void Digits_of_any_count_are_counted()
    {
        foreach (int digits in new[] { 1, 19, 20, 21, 39, 1022, 1023, 1024, 5000 })
        {
            byte[] line = Encoding.ASCII.GetBytes(new string('9', digits) + ". Apple");

            Assert.Equal(LineFault.None, LineParser.Parse(line, 0, out IndexEntry entry));
            int stored = EntryShape.StoredNumberLength(entry.Shape);
            Assert.Equal(Math.Min(digits, EntryShape.SaturatedNumberLength), stored);
            Assert.Equal(digits, LineParser.CountDigits(line));
        }
    }
}
