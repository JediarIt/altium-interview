using System.Text;

using FileSort.Common;
using FileSort.Engine.Lines.Parsing;
using FileSort.Tests.Engine.InMemory;

namespace FileSort.Tests.Engine.Chunks.Reading;

/// <summary>
/// The scenarios of "Line terminator" and "File structure" of <c>line-format</c>, run against the engine's reading: the
/// lines that a sort reads from the input are the lines that it writes, so the output shows them.
/// </summary>
public class ReadingTests
{
    private static string Sort(string input, LineEnding lineEnding)
    {
        return Sort(Encoding.UTF8.GetBytes(input), lineEnding);
    }

    private static string Sort(byte[] input, LineEnding lineEnding)
    {
        // Chunks of a few lines on two threads, so that the lines also cross chunks and ranges.
        SortOutcome outcome = TestSort.Run(input, TestSort.Settings(TestSort.SmallestChunkBuffer(2), 2, lineEnding));
        return Encoding.UTF8.GetString(outcome.Output);
    }

    // Requirement: Line terminator

    [Fact]
    public void Line_feed()
    {
        Assert.Equal("1. Apple\n2. Banana\n", Sort("2. Banana\n1. Apple\n", LineEnding.Lf));
    }

    [Fact]
    public void Carriage_return_and_line_feed()
    {
        Assert.Equal("1. Apple\r\n2. Banana\r\n", Sort("2. Banana\r\n1. Apple\r\n", LineEnding.CrLf));
    }

    [Fact]
    public void Last_line_without_a_terminator()
    {
        Assert.Equal("1. Apple\n2. Banana\n", Sort("1. Apple\n2. Banana", LineEnding.Lf));
        Assert.Equal("1. Apple\r\n2. Banana\r\n", Sort("1. Apple\r\n2. Banana", LineEnding.CrLf));
    }

    [Fact]
    public void Carriage_return_inside_a_String()
    {
        // A\rB comes after A, whose String is its beginning.
        Assert.Equal("1. A\n1. A\rB\n", Sort("1. A\rB\n1. A\n", LineEnding.Lf));
    }

    [Fact]
    public void Carriage_return_at_the_end_of_a_String()
    {
        Assert.Equal("1. Apple\n1. Apple\r\n", Sort("1. Apple\r\n1. Apple\n", LineEnding.Lf));
    }

    [Fact]
    public void Carriage_return_before_the_terminator()
    {
        Assert.Equal("1. Apple\r\n1. Apple\r\r\n", Sort("1. Apple\r\r\n1. Apple\r\n", LineEnding.CrLf));
    }

    [Fact]
    public void Line_feed_without_a_carriage_return()
    {
        InvalidLineException e = Assert.Throws<InvalidLineException>(
            () => Sort("1. Apple\r\n2. Banana\n3. Cherry\r\n", LineEnding.CrLf));

        Assert.Equal(2, e.LineNumber);
        Assert.Equal(10, e.Offset);
        Assert.Equal(LineFault.BareLineFeed, e.Fault);
    }

    // Requirement: File structure

    [Fact]
    public void Empty_file()
    {
        Assert.Equal("", Sort("", LineEnding.Lf));
    }

    [Fact]
    public void Empty_line()
    {
        InvalidLineException e = Assert.Throws<InvalidLineException>(
            () => Sort("1. Apple\n\n2. Banana\n", LineEnding.Lf));

        Assert.Equal(2, e.LineNumber);
        Assert.Equal(LineFault.Empty, e.Fault);
    }

    [Fact]
    public void Byte_order_mark_at_the_start()
    {
        Assert.Equal("1. Apple\n", Sort([0xEF, 0xBB, 0xBF, .. "1. Apple\n"u8], LineEnding.Lf));
    }

    [Fact]
    public void Byte_order_mark_and_nothing_else()
    {
        Assert.Equal("", Sort([0xEF, 0xBB, 0xBF], LineEnding.Lf));
    }

    [Fact]
    public void Byte_order_mark_at_the_start_of_a_later_line()
    {
        InvalidLineException e = Assert.Throws<InvalidLineException>(
            () => Sort([.. "1. Apple\n"u8, 0xEF, 0xBB, 0xBF, .. "2. Banana\n"u8], LineEnding.Lf));

        Assert.Equal(2, e.LineNumber);
        Assert.Equal(9, e.Offset);
        Assert.Equal(LineFault.NoNumber, e.Fault);
    }

    // The line ending taken from the input

    [Theory]
    [InlineData("1. Apple\n", LineEnding.Lf)]
    [InlineData("1. Apple\r\n", LineEnding.CrLf)]
    [InlineData("1. Apple", LineEnding.Lf)]
    [InlineData("1. Apple\r", LineEnding.Lf)]
    [InlineData("\r\n", LineEnding.CrLf)]
    public void The_line_ending_comes_from_the_first_line_feed(string input, LineEnding expected)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        LineEnding detected;
        try
        {
            detected = TestSort.Run(bytes, TestSort.Settings(1 << 16, 1)).Result.LineEnding;
        }
        catch (InvalidLineException e)
        {
            detected = e.LineEnding;
        }

        Assert.Equal(expected, detected);
    }

    [Fact]
    public void A_first_line_feed_beyond_the_longest_line_leaves_the_line_too_long_with_either_line_ending()
    {
        byte[] input = Encoding.UTF8.GetBytes("1. " + new string('a', TestSort.MaxLineLength + 5) + "\r\n2. B\r\n");

        LineTooLongException e = Assert.Throws<LineTooLongException>(
            () => TestSort.Run(input, TestSort.Settings(1 << 16, 2)));

        Assert.Equal(1, e.LineNumber);
        Assert.Equal(0, e.Offset);
    }
}
