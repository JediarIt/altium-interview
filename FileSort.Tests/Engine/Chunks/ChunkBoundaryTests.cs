using System.Text;

using FileSort.Common;
using FileSort.Engine.Lines.Parsing;
using FileSort.Tests.Engine.InMemory;
using FileSort.Tests.Format;

namespace FileSort.Tests.Engine.Chunks;

/// <summary>
/// Chunk and range boundaries at every line of a small input: tiny chunk buffers of every
/// size from the smallest up, and 1, 2, 7 and 16 threads, which cut the input and its chunks in every place. The
/// output must be the naive sort every time, and an invalid line must be reported with the number and the offset that
/// the naive parser gives it.
/// </summary>
public class ChunkBoundaryTests
{
    // Lines of different lengths and kinds: equal keys, equal Strings, a String with a carriage return, short ones.
    private static readonly string[] Lines =
    [
        "7. Apple", "007. Apple", "15. Banana pie with cream", "2. A", "3. ", "18446744073709551616. Apple",
        "99. Cherry\r", "1. Apple pie", "5. Ёлка", "42. Zebra crossing at night", "8. Apple", "0. Apple",
        "123456. Delta", "6. B", "7. Apple", "31. Echo echo echo echo", "4. Apple", "11. Foxtrot", "9. A b c d e f g",
        "10. Golf", "12. Hotel", "13. India", "14. Juliet", "16. Kilo", "17. Lima Lima", "18. Mike", "19. November",
        "20. Oscar", "21. Papa", "22. Quebec", "23. Romeo and Juliet", "24. Sierra", "25. Tango", "26. Uniform",
        "27. Victor", "28. Whiskey", "29. X-ray", "30. Yankee", "32. Zulu", "33. Apple",
    ];

    public static TheoryData<int> Threads => new() { 1, 2, 7, 16 };

    [Theory]
    [MemberData(nameof(Threads))]
    public void A_line_without_a_line_feed_in_a_whole_read_is_too_long_where_the_reader_finds_it(int threads)
    {
        // Short lines fill a few of the smallest chunks; then comes a line ten times the limit, in which the read of
        // its chunk finds no line feed at all. Its number and offset come from the chunks before it, the byte order
        // mark included.
        StringBuilder shortLines = new();
        for (int i = 1; i <= 60; i++)
        {
            shortLines.Append(System.Globalization.CultureInfo.InvariantCulture, $"{i}. Apple\n");
        }

        byte[] before = [0xEF, 0xBB, 0xBF, .. Encoding.ASCII.GetBytes(shortLines.ToString())];
        string longLine = "61. " + new string('x', 10 * TestSort.MaxLineLength) + "\n";
        byte[] input = [.. before, .. Encoding.ASCII.GetBytes(longLine + "62. Apple\n")];

        LineTooLongException e = Assert.Throws<LineTooLongException>(
            () => TestSort.Run(input, TestSort.Settings(TestSort.SmallestChunkBuffer(threads), threads)));

        Assert.Equal(61, e.LineNumber);
        Assert.Equal(before.Length, e.Offset);
    }

    private static byte[] Input(string terminator, int skipTerminatorOfLast = 0)
    {
        string text = string.Concat(Lines.Select(line => line + terminator));
        return Encoding.UTF8.GetBytes(skipTerminatorOfLast > 0 ? text[..^terminator.Length] : text);
    }

    [Theory]
    [MemberData(nameof(Threads))]
    public void A_chunk_ends_after_every_line_for_some_size_of_the_buffer(int threads)
    {
        // A line limit just above the longest line, so that a chunk can be as short as two or three lines.
        const int MaxLineLength = 32;
        byte[] input = Input("\n");
        HashSet<long> cuts = [];
        int smallest = TestSort.SmallestChunkBuffer(threads, MaxLineLength);

        for (int extra = 0; extra < 2000; extra++)
        {
            foreach ((long offset, int length) in TestChunks.Cuts(input, smallest + extra, threads, MaxLineLength))
            {
                cuts.Add(offset + length);
            }
        }

        // The first chunk reads at least about half of what the smallest buffer has for lines and entries, four longest
        // lines, so it never ends within the first few lines; after them, every line end is a cut for some size.
        List<long> ends = [.. Lines.Select(line => Encoding.UTF8.GetByteCount(line) + 1L)];
        for (int i = 1; i < ends.Count; i++)
        {
            ends[i] += ends[i - 1];
        }

        List<long> reachable = [.. ends.SkipLast(1).Where(end => end >= 4 * (MaxLineLength + 2))];
        List<long> missing = [.. reachable.Where(end => !cuts.Contains(end))];
        Assert.True(reachable.Count >= 30, $"Only {reachable.Count} lines end after the first chunk can.");
        string cutList = string.Join(", ", cuts.Order());
        Assert.True(missing.Count == 0, $"Never a cut: {string.Join(", ", missing)}. Cuts: {cutList}.");
    }

    [Theory]
    [MemberData(nameof(Threads))]
    public void Every_cut_sorts_into_the_naive_order(int threads)
    {
        (string Terminator, LineEnding LineEnding)[] endings = [("\n", LineEnding.Lf), ("\r\n", LineEnding.CrLf)];
        foreach ((string terminator, LineEnding lineEnding) in endings)
        {
            foreach (int unterminated in new[] { 0, 1 })
            {
                byte[] input = Input(terminator, unterminated);
                byte[] expected = NaiveSort.SortFile(input, terminator);
                int smallest = TestSort.SmallestChunkBuffer(threads);

                for (int extra = 0; extra < 1200; extra += 23)
                {
                    SortOutcome outcome = TestSort.Run(input, TestSort.Settings(smallest + extra, threads, lineEnding));
                    Assert.Equal(expected, outcome.Output);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Threads))]
    public void An_invalid_line_anywhere_is_reported_with_its_number_and_offset(int threads)
    {
        int smallest = TestSort.SmallestChunkBuffer(threads);
        for (int invalid = 0; invalid < Lines.Length; invalid++)
        {
            string[] lines = [.. Lines];
            lines[invalid] = invalid % 2 == 0 ? "Apple without a Number" : "12.no separator";
            string text = string.Concat(lines.Select(line => line + "\n"));
            byte[] input = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)];
            long offset = 3 + lines.Take(invalid).Sum(line => Encoding.UTF8.GetByteCount(line) + 1L);

            foreach (int extra in new[] { 0, 101, 400, 1 << 16 })
            {
                InvalidLineException e = Assert.Throws<InvalidLineException>(
                    () => TestSort.Run(input, TestSort.Settings(smallest + extra, threads)));
                Assert.Equal(invalid + 1, e.LineNumber);
                Assert.Equal(offset, e.Offset);
                Assert.Equal(invalid % 2 == 0 ? LineFault.NoNumber : LineFault.NoSeparator, e.Fault);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Threads))]
    public void The_first_problem_by_offset_wins_whatever_the_chunks(int threads)
    {
        string tooLong = "1. " + new string('x', TestSort.MaxLineLength);
        int smallest = TestSort.SmallestChunkBuffer(threads);
        string head = string.Concat(Lines.Take(20).Select(line => line + "\n"));
        byte[] invalidFirst = Encoding.UTF8.GetBytes(head + "No number\n" + tooLong + "\n1. Apple\n");
        byte[] tooLongFirst = Encoding.UTF8.GetBytes(head + tooLong + "\nNo number\n1. Apple\n");

        foreach (int extra in new[] { 0, 7, 50, 333, 1000, 1 << 16 })
        {
            InvalidLineException invalid = Assert.Throws<InvalidLineException>(
                () => TestSort.Run(invalidFirst, TestSort.Settings(smallest + extra, threads)));
            Assert.Equal(21, invalid.LineNumber);

            LineTooLongException tooLongError = Assert.Throws<LineTooLongException>(
                () => TestSort.Run(tooLongFirst, TestSort.Settings(smallest + extra, threads)));
            Assert.Equal(21, tooLongError.LineNumber);
            Assert.Equal(Encoding.UTF8.GetByteCount(head), tooLongError.Offset);
        }
    }

    [Theory]
    [InlineData(3, LineFault.BareLineFeed)]
    [InlineData(TestSort.MaxLineLength - 1, LineFault.BareLineFeed)]
    [InlineData(TestSort.MaxLineLength, LineFault.None)]
    [InlineData(TestSort.MaxLineLength + 1, LineFault.None)]
    public void A_bare_line_feed_in_a_crlf_file_is_invalid_within_the_limit_and_too_long_beyond(
        int before, LineFault expected)
    {
        // Line 2 has a line feed without a carriage return after its first bytes.
        string line = "2. " + new string('a', before - 3);
        byte[] input = Encoding.UTF8.GetBytes($"1. Apple\r\n{line}\nrest of it\r\n3. Banana\r\n");

        Exception e = Assert.ThrowsAny<Exception>(
            () => TestSort.Run(input, TestSort.Settings(1 << 16, 2, LineEnding.CrLf)));

        if (expected == LineFault.BareLineFeed)
        {
            InvalidLineException invalid = Assert.IsType<InvalidLineException>(e);
            Assert.Equal(LineFault.BareLineFeed, invalid.Fault);
            Assert.Equal(2, invalid.LineNumber);
        }
        else
        {
            Assert.Equal(2, Assert.IsType<LineTooLongException>(e).LineNumber);
        }
    }
}
