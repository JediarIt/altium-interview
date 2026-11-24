using System.Text;

using FileSort.Common;
using FileSort.Engine.Lines.Parsing;
using FileSort.Tests.Engine.InMemory;

namespace FileSort.Tests.Engine;

/// <summary>
/// The scenarios of <c>external-sorter</c> that the engine decides, run in memory: what the output holds and in which
/// order, line endings, the byte order mark, invalid input and the length of a line. Each one runs with one chunk and
/// with chunks of a few lines, on one thread and on several.
/// </summary>
public class SortScenarioTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    // One chunk for the whole input, and the smallest chunks, on one thread and on several.
    public static TheoryData<int, bool> Layouts => new() { { 1, false }, { 1, true }, { 7, false }, { 7, true } };

    private static string Sort(string input, int threads, bool smallChunks, LineEnding? lineEnding = null)
    {
        return Text(SortBytes(Bytes(input), threads, smallChunks, lineEnding));
    }

    private static byte[] SortBytes(byte[] input, int threads, bool smallChunks, LineEnding? lineEnding = null)
    {
        int chunkBuffer = smallChunks ? TestSort.SmallestChunkBuffer(threads) : 1 << 20;
        return TestSort.Run(input, TestSort.Settings(chunkBuffer, threads, lineEnding)).Output;
    }

    // Requirement: Sorted output

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Example_of_the_task(int threads, bool smallChunks)
    {
        string output = Sort(
            "415. Apple\n30432. Something something something\n1. Apple\n32. Cherry is the best\n2. Banana is yellow\n",
            threads,
            smallChunks);

        Assert.Equal(
            "1. Apple\n415. Apple\n2. Banana is yellow\n32. Cherry is the best\n30432. Something something something\n",
            output);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Lines_keep_their_bytes(int threads, bool smallChunks)
    {
        string sorted = Sort("007. Apple\n5.  Apple\n5. Ёлка 😀\n", threads, smallChunks);
        Assert.Equal("5.  Apple\n007. Apple\n5. Ёлка 😀\n", sorted);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Repeated_lines(int threads, bool smallChunks)
    {
        string sorted = Sort("7. Apple\n8. Apple\n7. Apple\n7. Apple\n", threads, smallChunks);
        Assert.Equal("7. Apple\n7. Apple\n7. Apple\n8. Apple\n", sorted);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Last_line_without_a_terminator(int threads, bool smallChunks)
    {
        Assert.Equal("1. Apple\n2. Banana\n", Sort("2. Banana\n1. Apple", threads, smallChunks));
    }

    [Fact]
    public void Empty_input()
    {
        SortOutcome outcome = TestSort.Run([], TestSort.Settings(1 << 16, 4));

        Assert.Empty(outcome.Output);
        Assert.Equal(0, outcome.Result.Lines);
        Assert.Equal(0, outcome.Result.Runs);
        Assert.Equal(0, outcome.Result.MergePasses);
    }

    // Requirement: Lines with equal keys

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Equal_keys_different_bytes(int threads, bool smallChunks)
    {
        Assert.Equal(
            "0. Apple\n00. Apple\n007. Apple\n07. Apple\n7. Apple\n",
            Sort("7. Apple\n007. Apple\n07. Apple\n00. Apple\n0. Apple\n", threads, smallChunks));
    }

    // Requirement: Line ending

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Detected_carriage_return_and_line_feed(int threads, bool smallChunks)
    {
        Assert.Equal("1. Apple\r\n2. Banana\r\n", Sort("2. Banana\r\n1. Apple\r\n", threads, smallChunks));
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Carriage_return_at_the_end_of_a_String(int threads, bool smallChunks)
    {
        Assert.Equal("1. Apple\r\n2. Banana\n", Sort("2. Banana\n1. Apple\r\n", threads, smallChunks));
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Line_ending_given_explicitly(int threads, bool smallChunks)
    {
        Assert.Equal("1. Apple\r\n2. Banana\n", Sort("1. Apple\r\n2. Banana\n", threads, smallChunks, LineEnding.Lf));

        InvalidLineException e = Assert.Throws<InvalidLineException>(
            () => Sort("1. Apple\r\n2. Banana\n", threads, smallChunks));
        Assert.Equal(2, e.LineNumber);
        Assert.Equal(LineEnding.CrLf, e.LineEnding);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Input_without_a_line_feed(int threads, bool smallChunks)
    {
        Assert.Equal("1. Apple\n", Sort("1. Apple", threads, smallChunks));
        Assert.Equal("1. Apple\r\n", Sort("1. Apple", threads, smallChunks, LineEnding.CrLf));
    }

    // Requirement: Byte order mark

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Mark_at_the_start_of_the_input(int threads, bool smallChunks)
    {
        byte[] input = [0xEF, 0xBB, 0xBF, .. Bytes("2. Banana\n1. Apple\n")];
        SortOutcome outcome = TestSort.Run(
            input, TestSort.Settings(smallChunks ? TestSort.SmallestChunkBuffer(threads) : 1 << 20, threads));

        Assert.Equal("1. Apple\n2. Banana\n", Text(outcome.Output));
        Assert.True(outcome.Result.ByteOrderMark);
    }

    [Fact]
    public void Mark_and_nothing_else()
    {
        SortOutcome outcome = TestSort.Run([0xEF, 0xBB, 0xBF], TestSort.Settings(1 << 16, 2));

        Assert.Empty(outcome.Output);
        Assert.True(outcome.Result.ByteOrderMark);
    }

    // Requirement: Invalid input

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Line_without_a_separator(int threads, bool smallChunks)
    {
        InvalidLineException e = Assert.Throws<InvalidLineException>(
            () => Sort("1. Apple\nApple\n", threads, smallChunks));

        Assert.Equal(2, e.LineNumber);
        Assert.Equal(9, e.Offset);
        Assert.Equal(LineFault.NoNumber, e.Fault);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Empty_line(int threads, bool smallChunks)
    {
        InvalidLineException e = Assert.Throws<InvalidLineException>(
            () => Sort("1. Apple\n\n2. Banana\n", threads, smallChunks));

        Assert.Equal(2, e.LineNumber);
        Assert.Equal(LineFault.Empty, e.Fault);
    }

    [Fact]
    public void Invalid_line_far_into_the_input()
    {
        // Many chunks of valid lines, then the ill-formed bytes C3 28 in a String near the end.
        StringBuilder lines = new();
        for (int i = 0; i < 3000; i++)
        {
            lines.Append(i).Append(". Apple ").Append(i % 17).Append('\n');
        }

        byte[] valid = Bytes(lines.ToString());
        byte[] input = [.. valid, .. "7. Bad "u8, 0xC3, 0x28, (byte)'\n', .. "1. Apple\n"u8];

        InvalidLineException e = Assert.Throws<InvalidLineException>(
            () => TestSort.Run(input, TestSort.Settings(TestSort.SmallestChunkBuffer(3), 3)));

        Assert.Equal(3001, e.LineNumber);
        Assert.Equal(valid.Length, e.Offset);
        Assert.Equal(LineFault.IllFormedUtf8, e.Fault);
    }

    // Requirement: Line length

    [Theory]
    [MemberData(nameof(Layouts))]
    public void The_longest_line_is_accepted_and_one_byte_more_is_too_long_with_either_line_ending(
        int threads, bool smallChunks)
    {
        foreach (LineEnding lineEnding in new[] { LineEnding.Lf, LineEnding.CrLf })
        {
            string terminator = lineEnding == LineEnding.CrLf ? "\r\n" : "\n";
            string longest = "2. " + new string('x', TestSort.MaxLineLength - 3);

            Assert.Equal(
                $"1. Apple{terminator}{longest}{terminator}",
                Text(SortBytes(Bytes($"{longest}{terminator}1. Apple{terminator}"), threads, smallChunks, lineEnding)));

            LineTooLongException e = Assert.Throws<LineTooLongException>(
                () => Sort($"1. Apple{terminator}{longest}x{terminator}", threads, smallChunks, lineEnding));
            Assert.Equal(2, e.LineNumber);
            Assert.Equal(8 + terminator.Length, e.Offset);
        }
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Line_longer_than_supported(int threads, bool smallChunks)
    {
        string tooLong = "1. " + new string('a', TestSort.MaxLineLength - 2);

        LineTooLongException e = Assert.Throws<LineTooLongException>(
            () => Sort($"2. Apple\n{tooLong}\n3. Banana\n", threads, smallChunks));

        Assert.Equal(2, e.LineNumber);
        Assert.Equal(9, e.Offset);
        Assert.Equal(TestSort.MaxLineLength, e.Limit);
        Assert.Equal(LineEnding.Lf, e.LineEnding);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Invalid_line_before_a_long_line(int threads, bool smallChunks)
    {
        string tooLong = "1. " + new string('a', TestSort.MaxLineLength);

        InvalidLineException e = Assert.Throws<InvalidLineException>(
            () => Sort($"1. Apple\nApple\n{tooLong}\n", threads, smallChunks));

        Assert.Equal(2, e.LineNumber);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Long_line_before_an_invalid_line(int threads, bool smallChunks)
    {
        string tooLong = "1. " + new string('a', TestSort.MaxLineLength);

        LineTooLongException e = Assert.Throws<LineTooLongException>(
            () => Sort($"1. Apple\n{tooLong}\nApple\n", threads, smallChunks));

        Assert.Equal(2, e.LineNumber);
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Longest_line(int threads, bool smallChunks)
    {
        string longest = "1. " + new string('a', TestSort.MaxLineLength - 3);
        string longestCrLf = "2. " + new string('b', TestSort.MaxLineLength - 3);

        Assert.Equal($"5. Apple\n{longest}\n", Sort($"{longest}\n5. Apple\n", threads, smallChunks));
        Assert.Equal($"5. Apple\r\n{longestCrLf}\r\n", Sort($"{longestCrLf}\r\n5. Apple\r\n", threads, smallChunks));
    }
}
