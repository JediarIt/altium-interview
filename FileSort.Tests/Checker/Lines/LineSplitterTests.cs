using System.Text;

using FileSort.Checker.Lines;

namespace FileSort.Tests.Checker.Lines;

/// <summary>
/// The checker's reading of a file as lines, together with its parser, against every scenario of "Line terminator"
/// and "File structure" of the <c>line-format</c> spec; then the same reading with chunks of every size, and at the
/// limit on the length of a line. A file is read as chunks of whole lines, and every chunk is split into lines.
/// </summary>
public class LineSplitterTests
{
    private static readonly byte[] Mark = [0xEF, 0xBB, 0xBF];

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    // The checker's types are internal, so a theory names a line ending and gets the value here.
    private static LineEnding Ending(string name) => name == "crlf" ? LineEnding.CrLf : LineEnding.Lf;

    private static List<ReadLine> Lines(string file, LineEnding lineEnding)
    {
        return ReadFile.From(Utf8(file), lineEnding).Lines;
    }

    // Requirement: Line terminator

    [Fact]
    public void Line_feed()
    {
        List<ReadLine> lines = Lines("1. Apple\n2. Banana\n", LineEnding.Lf);

        Assert.Equal(["1. Apple", "2. Banana"], lines.Select(l => l.Text));
        Assert.All(lines, line => Assert.Equal(LineFault.None, line.Fault));
        Assert.All(lines, line => Assert.True(line.HasTerminator));
    }

    [Fact]
    public void Carriage_return_and_line_feed()
    {
        List<ReadLine> lines = Lines("1. Apple\r\n2. Banana\r\n", LineEnding.CrLf);

        Assert.Equal(["1. Apple", "2. Banana"], lines.Select(l => l.Text));
        Assert.Equal(["Apple", "Banana"], lines.Select(l => Encoding.UTF8.GetString(l.StringBytes)));
        Assert.Equal([0L, 10L], lines.Select(l => l.Offset));
    }

    [Theory]
    [InlineData("lf", "1. Apple\n2. Banana")]
    [InlineData("crlf", "1. Apple\r\n2. Banana")]
    public void Last_line_without_a_terminator(string ending, string file)
    {
        LineEnding lineEnding = Ending(ending);
        List<ReadLine> lines = Lines(file, lineEnding);

        Assert.Equal(["1. Apple", "2. Banana"], lines.Select(l => l.Text));
        Assert.All(lines, line => Assert.Equal(LineFault.None, line.Fault));
        Assert.Equal([true, false], lines.Select(l => l.HasTerminator));
    }

    [Fact]
    public void Carriage_return_inside_a_String()
    {
        ReadLine line = Assert.Single(Lines("1. A\rB\n", LineEnding.Lf));

        Assert.Equal("A\rB"u8.ToArray(), line.StringBytes);
    }

    [Fact]
    public void Carriage_return_at_the_end_of_a_String()
    {
        ReadLine line = Assert.Single(Lines("1. Apple\r\n", LineEnding.Lf));

        Assert.Equal("Apple\r"u8.ToArray(), line.StringBytes);
    }

    [Fact]
    public void Carriage_return_before_the_terminator()
    {
        ReadLine line = Assert.Single(Lines("1. Apple\r\r\n", LineEnding.CrLf));

        Assert.Equal("Apple\r"u8.ToArray(), line.StringBytes);
    }

    [Fact]
    public void Line_feed_without_a_carriage_return()
    {
        List<ReadLine> lines = Lines("1. Apple\r\n2. Banana\n3. Cherry\r\n", LineEnding.CrLf);

        // The reader rejects line 2 at its bare line feed: it gives the line up to there and does not gather the
        // rest, "3. Cherry", which would only be more bytes of a line that is already not valid.
        Assert.Equal(2, lines.Count);
        Assert.Equal(LineFault.None, lines[0].Fault);
        Assert.Equal(LineRead.BareLineFeed, lines[1].Read);
        Assert.Equal(LineFault.BareLineFeed, lines[1].Fault);
        Assert.Equal("2. Banana\n", lines[1].Text);
        Assert.Equal(2, lines[1].Number);
        Assert.Equal(10, lines[1].Offset);
    }

    [Theory]
    [InlineData("\n1. Apple\r\n")]
    [InlineData("1. Apple\r\n\n")]
    [InlineData("1. Apple\n")]
    public void A_line_feed_at_the_start_of_a_line_has_no_carriage_return_of_its_own(string file)
    {
        // The byte before it, if there is one, is the line feed of the terminator of the line before.
        ReadLine last = Lines(file, LineEnding.CrLf)[^1];

        Assert.Equal(LineRead.BareLineFeed, last.Read);
    }

    [Fact]
    public void A_carriage_return_at_the_end_of_the_file_is_a_byte_of_the_last_String()
    {
        ReadLine line = Assert.Single(Lines("1. Apple\r", LineEnding.CrLf));

        Assert.Equal("Apple\r"u8.ToArray(), line.StringBytes);
        Assert.False(line.HasTerminator);
    }

    // Requirement: File structure

    [Theory]
    [InlineData("lf")]
    [InlineData("crlf")]
    public void Empty_file(string ending)
    {
        LineEnding lineEnding = Ending(ending);
        ReadFile file = ReadFile.From([], lineEnding);

        Assert.Empty(file.Lines);
        Assert.False(file.HasByteOrderMark);
    }

    [Fact]
    public void Empty_line()
    {
        List<ReadLine> lines = Lines("1. Apple\n\n2. Banana\n", LineEnding.Lf);

        Assert.Equal(3, lines.Count);
        Assert.Equal([LineFault.None, LineFault.Empty, LineFault.None], lines.Select(l => l.Fault));
        Assert.Equal(9, lines[1].Offset);
    }

    [Fact]
    public void Byte_order_mark_at_the_start()
    {
        ReadFile file = ReadFile.From([.. Mark, .. "1. Apple\n"u8], LineEnding.Lf);

        ReadLine line = Assert.Single(file.Lines);
        Assert.True(file.HasByteOrderMark);
        Assert.Equal("1. Apple", line.Text);
        Assert.Equal("Apple"u8.ToArray(), line.StringBytes);

        // The offset counts the bytes of the file, the mark included.
        Assert.Equal(3, line.Offset);
        Assert.Equal(1, line.Number);
    }

    [Fact]
    public void Byte_order_mark_and_nothing_else()
    {
        ReadFile file = ReadFile.From(Mark, LineEnding.Lf);

        Assert.Empty(file.Lines);
        Assert.True(file.HasByteOrderMark);
    }

    [Fact]
    public void Byte_order_mark_at_the_start_of_a_later_line()
    {
        ReadFile file = ReadFile.From([.. "1. Apple\n"u8, .. Mark, .. "2. Banana\n"u8], LineEnding.Lf);

        Assert.False(file.HasByteOrderMark);
        Assert.Equal(2, file.Lines.Count);
        Assert.Equal(LineFault.None, file.Lines[0].Fault);
        Assert.Equal(LineFault.NoNumber, file.Lines[1].Fault);
    }

    [Theory]
    [InlineData(new byte[] { 0xEF })]
    [InlineData(new byte[] { 0xEF, 0xBB })]
    [InlineData(new byte[] { 0xEF, 0xBB, 0x31, 0x2E, 0x20 })]
    public void A_part_of_a_byte_order_mark_is_not_a_mark(byte[] content)
    {
        ReadFile file = ReadFile.From(content, LineEnding.Lf);

        Assert.False(file.HasByteOrderMark);
        ReadLine line = Assert.Single(file.Lines);
        Assert.Equal(content, line.Bytes);
        Assert.Equal(0, line.Offset);
    }

    [Fact]
    public void A_second_byte_order_mark_is_part_of_the_first_line()
    {
        ReadFile file = ReadFile.From([.. Mark, .. Mark, .. "1. Apple\n"u8], LineEnding.Lf);

        Assert.True(file.HasByteOrderMark);
        Assert.Equal(LineFault.NoNumber, Assert.Single(file.Lines).Fault);
    }

    // Chunks of every size

    public static TheoryData<string, string> Contents => new()
    {
        { "1. Apple\n2. Banana is yellow\n\n3. \n4. A\rB\r\n5. Ёлка 日本 😀\n6. last", "lf" },
        { "1. Apple\r\n2. Banana is yellow\r\n\r\n3. \r\n4. A\rB\r\r\n5. Ёлка 日本 😀\r\n6. last\r", "crlf" },
        { "1. Apple\r\n2. Banana\r\n3. Cherry is the best\n4. never read\r\n", "crlf" },
        { "\uFEFF1. Apple\n2. Banana\n", "lf" },
        { "\uFEFF1. Apple\r\n2. Banana\r\n", "crlf" },
        { "\uFEFF", "crlf" },
        { "", "lf" },
        { "\n", "lf" },
        { "\r\n\r\n", "crlf" },
    };

    [Theory]
    [MemberData(nameof(Contents))]
    public void A_small_chunk_gives_the_same_lines_as_a_large_one(string content, string ending)
    {
        LineEnding lineEnding = Ending(ending);
        byte[] file = Utf8(content);
        ReadFile expected = ReadFile.From(file, lineEnding, chunkSize: 4096);

        // Every size from one byte up: the end of a read falls on every byte of the content in turn, so a
        // terminator, a character of several bytes and the byte order mark are all split between two reads.
        for (int chunkSize = 1; chunkSize <= file.Length + 2; chunkSize++)
        {
            ReadFile actual = ReadFile.From(file, lineEnding, chunkSize);

            Assert.Equal(expected.HasByteOrderMark, actual.HasByteOrderMark);
            Assert.Equal(expected.Lines, actual.Lines);
        }
    }

    [Theory]
    [MemberData(nameof(Contents))]
    public void A_stream_that_gives_a_few_bytes_per_read_gives_the_same_lines(string content, string ending)
    {
        LineEnding lineEnding = Ending(ending);
        byte[] file = Utf8(content);
        ReadFile expected = ReadFile.From(file, lineEnding);

        foreach (int chunk in new[] { 1, 2, 3, 7 })
        {
            using ChunkedStream stream = new(file, chunk);

            ReadFile actual = ReadFile.From(stream, lineEnding, chunkSize: 16);

            Assert.Equal(expected.HasByteOrderMark, actual.HasByteOrderMark);
            Assert.Equal(expected.Lines, actual.Lines);
        }
    }

    [Fact]
    public void A_line_longer_than_a_chunk_gets_a_chunk_as_large_as_the_line()
    {
        string longText = new('x', 5000);
        byte[] file = Utf8($"1. Apple\n2. {longText}\n3. Banana\n");

        ReadFile read = ReadFile.From(file, LineEnding.Lf, chunkSize: 8, maxLineLength: 1 << 20);

        Assert.Equal(["1. Apple", $"2. {longText}", "3. Banana"], read.Lines.Select(l => l.Text));
        Assert.Equal([0L, 9L, 9L + 3 + 5000 + 1], read.Lines.Select(l => l.Offset));
    }

    // The limit on the length of a line

    public static TheoryData<string, string, int> ChunkSizes
    {
        get
        {
            TheoryData<string, string, int> data = [];
            foreach (int chunkSize in new[] { 1, 3, 10, 11, 12, 13, 64, 4096 })
            {
                data.Add("lf", "\n", chunkSize);
                data.Add("crlf", "\r\n", chunkSize);
            }

            return data;
        }
    }

    // The last thing the reading gave: the line that is too long, with no bytes, its number and its offset.
    private static ReadLine AssertTooLong(ReadFile read)
    {
        ReadLine last = read.Lines[^1];
        Assert.Equal(LineRead.TooLong, last.Read);
        Assert.Empty(last.Bytes);
        Assert.All(read.Lines.SkipLast(1), line => Assert.Equal(LineRead.Line, line.Read));
        return last;
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void A_line_of_exactly_the_limit_is_read(string ending, string terminator, int chunkSize)
    {
        // "1. 3456789" is ten bytes.
        byte[] file = Utf8($"1. 3456789{terminator}2. 3456789{terminator}3. 3456789");

        ReadFile read = ReadFile.From(file, Ending(ending), chunkSize, maxLineLength: 10);

        Assert.Equal(["1. 3456789", "2. 3456789", "3. 3456789"], read.Lines.Select(l => l.Text));
        Assert.All(read.Lines, line => Assert.Equal(LineRead.Line, line.Read));
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void A_line_above_the_limit_ends_the_reading(string ending, string terminator, int chunkSize)
    {
        // The second line is eleven bytes.
        byte[] file = Utf8($"1. 3456789{terminator}2. 34567890{terminator}3. 3456789{terminator}");

        ReadLine tooLong = AssertTooLong(ReadFile.From(file, Ending(ending), chunkSize, maxLineLength: 10));

        Assert.Equal(2, tooLong.Number);
        Assert.Equal(10 + terminator.Length, tooLong.Offset);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void A_last_line_above_the_limit_ends_the_reading(string ending, string terminator, int chunkSize)
    {
        byte[] file = Utf8($"1. 3456789{terminator}2. 34567890");

        ReadLine tooLong = AssertTooLong(ReadFile.From(file, Ending(ending), chunkSize, maxLineLength: 10));

        Assert.Equal(2, tooLong.Number);
        Assert.Equal(10 + terminator.Length, tooLong.Offset);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void A_line_that_is_too_long_and_not_valid_is_too_long(string ending, string terminator, int chunkSize)
    {
        // The limit is on the bytes of a line, whatever they are: this one starts with a letter.
        byte[] file = Utf8($"1. Apple{terminator}{new string('x', 100)}{terminator}");

        ReadLine tooLong = AssertTooLong(ReadFile.From(file, Ending(ending), chunkSize, maxLineLength: 10));

        Assert.Equal(2, tooLong.Number);
        Assert.Equal(8 + terminator.Length, tooLong.Offset);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(4096)]
    public void A_bare_line_feed_within_the_limit_is_noticed_before_the_limit(int chunkSize)
    {
        // Read with \r\n the second line never ends: it would grow past the limit, but its bare line feed comes first.
        byte[] file = Utf8($"1. Apple\r\n2. Ba\n{new string('x', 100)}");

        ReadFile read = ReadFile.From(file, LineEnding.CrLf, chunkSize, maxLineLength: 10);

        Assert.Equal(LineRead.BareLineFeed, read.Lines[^1].Read);
        Assert.Equal("2. Ba\n", read.Lines[^1].Text);
        Assert.Equal(2, read.Lines[^1].Number);
        Assert.Equal(10, read.Lines[^1].Offset);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(4096)]
    public void A_bare_line_feed_beyond_the_limit_is_a_line_that_is_too_long(int chunkSize)
    {
        byte[] file = Utf8($"1. Apple\r\n2. {new string('x', 20)}\n3. Cherry\r\n");

        ReadLine tooLong = AssertTooLong(ReadFile.From(file, LineEnding.CrLf, chunkSize, maxLineLength: 10));

        Assert.Equal(2, tooLong.Number);
        Assert.Equal(10, tooLong.Offset);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(4096)]
    [InlineData(4 * 1024 * 1024)]
    public void A_file_read_with_the_wrong_line_ending_hits_the_limit_instead_of_filling_memory(int chunkSize)
    {
        // A file with no line feed at all, a megabyte of it, against a limit of a kilobyte.
        byte[] file = Utf8("1. " + new string('x', 1 << 20));

        ReadFile read = ReadFile.From(file, LineEnding.Lf, chunkSize, maxLineLength: 1024);

        ReadLine tooLong = AssertTooLong(read);
        Assert.Equal(1, tooLong.Number);
        Assert.Equal(0, tooLong.Offset);
    }

    [Fact]
    public void What_is_said_about_a_line_above_the_limit()
    {
        string text = LineLimit.Describe(lineNumber: 2, offset: 9, limit: 67108864);

        Assert.Contains("line 2", text);
        Assert.Contains("starts at byte 9", text);
        Assert.Contains("longer than 67108864 bytes", text);
        Assert.Contains("--line-ending", text);
    }

    // The splitter on bytes that are not a whole file

    [Fact]
    public void Bytes_that_do_not_reach_the_end_of_the_file_give_only_their_terminated_lines()
    {
        // A part of a file ends with a terminator; if it did not, what follows the last one would not be a line.
        LineSplitter splitter = new("1. Apple\n2. Ban"u8, LineEnding.Lf, endsFile: false, maxLineLength: 100);

        Assert.Equal(LineRead.Line, splitter.TryRead(out ReadOnlySpan<byte> line));
        Assert.Equal("1. Apple"u8.ToArray(), line.ToArray());
        Assert.Equal(1, splitter.LineNumber);
        Assert.Equal(0, splitter.LineOffset);
        Assert.True(splitter.LineHasTerminator);
        Assert.Equal(LineRead.None, splitter.TryRead(out _));
    }

    [Fact]
    public void Lines_are_numbered_from_one_and_their_offsets_count_from_the_start_of_the_bytes()
    {
        LineSplitter splitter = new(
            "1. Apple\r\n2. Banana\r\n3. Cherry"u8, LineEnding.CrLf, endsFile: true, maxLineLength: 100);
        List<(int Number, int Offset, bool HasTerminator)> lines = [];

        while (splitter.TryRead(out _) == LineRead.Line)
        {
            lines.Add((splitter.LineNumber, splitter.LineOffset, splitter.LineHasTerminator));
        }

        Assert.Equal([(1, 0, true), (2, 10, true), (3, 21, false)], lines);
    }
}
