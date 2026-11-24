using System.Globalization;
using System.Text;

using FileSort.Common;
using FileSort.Generator.Content.Lines;

namespace FileSort.Tests.Generator.Content.Lines;

/// <summary>
/// The scenarios of "Canonical form of a written line", checked on the parts that the generator composes a line
/// from, plus zero-padded Numbers and the <c>\r\n</c> terminator.
/// </summary>
public class LineWriterTests
{
    // A line put together the way the generator does it: the Number, the separator, the String, the terminator.
    private static byte[] Line(ulong number, string text, LineEnding lineEnding)
    {
        byte[] buffer = new byte[64];
        int length = LineWriter.WriteNumber(buffer, number);
        length += LineWriter.WriteSeparator(buffer.AsSpan(length));
        length += Encoding.UTF8.GetBytes(text, buffer.AsSpan(length));
        length += LineWriter.WriteTerminator(buffer.AsSpan(length), lineEnding);
        return buffer[..length];
    }

    private static string Text(byte[] line) => Encoding.UTF8.GetString(line);

    [Fact]
    public void Writing_a_line()
    {
        Assert.Equal(
            new byte[] { 0x34, 0x31, 0x35, 0x2E, 0x20, 0x41, 0x70, 0x70, 0x6C, 0x65, 0x0A },
            Line(415, "Apple", LineEnding.Lf));
    }

    [Fact]
    public void Writing_zero()
    {
        Assert.Equal("0. Apple\n", Text(Line(0, "Apple", LineEnding.Lf)));
    }

    [Fact]
    public void Writing_the_largest_Number()
    {
        Assert.Equal("18446744073709551615. Apple\n", Text(Line(ulong.MaxValue, "Apple", LineEnding.Lf)));
    }

    [Fact]
    public void Writing_with_the_carriage_return_and_line_feed_terminator()
    {
        Assert.Equal("415. Apple\r\n", Text(Line(415, "Apple", LineEnding.CrLf)));
    }

    [Fact]
    public void Writing_an_empty_String()
    {
        Assert.Equal("5. \n", Text(Line(5, "", LineEnding.Lf)));
    }

    [Fact]
    public void The_separator_and_the_terminators_are_those_of_the_format()
    {
        byte[] buffer = new byte[2];

        Assert.Equal(LineFormat.SeparatorLength, LineWriter.WriteSeparator(buffer));
        Assert.Equal(LineFormat.Separator.ToArray(), buffer);

        Assert.Equal(LineEnding.CrLf.TerminatorLength(), LineWriter.WriteTerminator(buffer, LineEnding.CrLf));
        Assert.Equal(LineEnding.CrLf.Terminator().ToArray(), buffer);

        Assert.Equal(LineEnding.Lf.TerminatorLength(), LineWriter.WriteTerminator(buffer, LineEnding.Lf));
        Assert.Equal(LineEnding.Lf.Terminator().ToArray(), buffer[..1]);
    }

    [Fact]
    public void Numbers_of_every_length_are_written_like_the_invariant_culture_does()
    {
        byte[] buffer = new byte[LineFormat.MaxNumberDigits];
        List<ulong> values = [0, ulong.MaxValue];

        // Every power of ten with its neighbours: the values where the number of digits changes.
        for (ulong power = 1; ; power *= 10)
        {
            values.AddRange([power - 1, power, power + 1]);
            if (power == 10_000_000_000_000_000_000)
            {
                break;
            }
        }

        // Every power of two with its neighbours.
        for (int bit = 0; bit < 64; bit++)
        {
            values.AddRange([(1UL << bit) - 1, 1UL << bit, (1UL << bit) + 1]);
        }

        foreach (ulong value in values)
        {
            string expected = value.ToString(CultureInfo.InvariantCulture);

            int length = LineWriter.WriteNumber(buffer, value);

            Assert.Equal(expected, Encoding.ASCII.GetString(buffer, 0, length));
            Assert.Equal(expected.Length, LineWriter.CountDigits(value));
        }
    }

    [Theory]
    [InlineData("7", 2, "007")]
    [InlineData("0", 1, "00")]
    [InlineData("415", 0, "415")]
    [InlineData("1", 19, "00000000000000000001")]
    [InlineData("1844674407370955161", 1, "01844674407370955161")]
    [InlineData("18446744073709551615", 3, "00018446744073709551615")]
    [InlineData("18446744073709551616", 0, "18446744073709551616")]
    [InlineData("999999999999999999999999999999", 3, "000999999999999999999999999999999")]
    public void Zero_padded_Numbers_of_up_to_128_bits(string value, int leadingZeros, string expected)
    {
        byte[] buffer = new byte[LineComposer.MaxNumberDigits];

        int length = LineWriter.WriteNumber(buffer, UInt128.Parse(value, CultureInfo.InvariantCulture), leadingZeros);

        Assert.Equal(expected, Encoding.ASCII.GetString(buffer, 0, length));
    }

    [Fact]
    public void A_negative_count_of_zeros_or_a_destination_too_small_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LineWriter.WriteNumber(new byte[64], 7UL, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LineWriter.WriteNumber(new byte[2], 7UL, 3));
        Assert.Throws<ArgumentException>(() => LineWriter.WriteNumber(new byte[20], UInt128.MaxValue, 0));
    }
}
