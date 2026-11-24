using System.Text;

using FileSort.Checker.Checks.Chunks;
using FileSort.Checker.Lines;

namespace FileSort.Tests.Checker.Checks.Chunks;

/// <summary>
/// The reader that cuts a stream into chunks of whole lines. What the lines of the chunks are is tested with the
/// splitter, in <c>LineSplitterTests</c>; these tests are about the chunks themselves.
/// </summary>
public class ChunkReaderTests
{
    private sealed record Piece(string Text, long Offset, bool EndsFile, bool IsLineWithoutEnd);

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static List<Piece> Chunks(byte[] file, LineEnding lineEnding, int chunkSize, int maxLineLength = 1 << 20)
    {
        using MemoryStream stream = new(file, writable: false);
        ChunkReader reader = new(stream, lineEnding, chunkSize, maxLineLength);
        List<Piece> pieces = [];
        while (reader.Read() is Chunk chunk)
        {
            string text = Encoding.UTF8.GetString(chunk.Bytes);
            pieces.Add(new Piece(text, chunk.Offset, chunk.EndsFile, chunk.IsLineWithoutEnd));
            ChunkReader.Release(chunk);
        }

        return pieces;
    }

    [Fact]
    public void A_file_that_fits_in_one_chunk_is_one_chunk_that_ends_the_file()
    {
        Piece chunk = Assert.Single(Chunks(Utf8("1. Apple\n2. Banana\n3. Cherry"), LineEnding.Lf, chunkSize: 4096));

        Assert.Equal("1. Apple\n2. Banana\n3. Cherry", chunk.Text);
        Assert.Equal(0, chunk.Offset);
        Assert.True(chunk.EndsFile);
    }

    [Fact]
    public void A_read_takes_from_the_stream_until_the_chunk_is_full_or_the_stream_ends()
    {
        using ChunkedStream stream = new(Utf8("1. Apple\n2. Banana\n"), chunk: 2);
        ChunkReader reader = new(stream, LineEnding.Lf, chunkSize: 1024, maxLineLength: 1024);

        Chunk? chunk = reader.Read();

        // One read took the whole stream, piece by piece, and saw its end: a file that fits in a chunk is handled
        // as one chunk, however the stream gives it.
        Assert.NotNull(chunk);
        Assert.True(chunk.EndsFile);
        Assert.Equal(19, chunk.Bytes.Length);
        Assert.Equal(19, reader.ReadBytes);
        Assert.True(stream.Reads > 9);
        ChunkReader.Release(chunk);

        Assert.Null(reader.Read());
        Assert.Equal(0, reader.ReadBytes);
    }

    [Theory]
    [InlineData("lf", "\n")]
    [InlineData("crlf", "\r\n")]
    public void Every_chunk_but_the_last_ends_with_a_line_feed_and_the_chunks_are_the_file(
        string ending, string terminator)
    {
        LineEnding lineEnding = ending == "crlf" ? LineEnding.CrLf : LineEnding.Lf;
        IEnumerable<string> lines = Enumerable.Range(1, 200).Select(i => $"{i}. Line number {i}{terminator}");
        string content = string.Concat(lines) + "9. last";
        byte[] file = Utf8(content);

        for (int chunkSize = 1; chunkSize < 300; chunkSize += 7)
        {
            List<Piece> chunks = Chunks(file, lineEnding, chunkSize);

            Assert.Equal(content, string.Concat(chunks.Select(chunk => chunk.Text)));
            Assert.All(chunks.SkipLast(1), chunk => Assert.EndsWith("\n", chunk.Text, StringComparison.Ordinal));
            Assert.All(chunks.SkipLast(1), chunk => Assert.False(chunk.EndsFile));
            Assert.True(chunks[^1].EndsFile);

            // Every chunk starts where the one before it ended.
            long offset = 0;
            foreach (Piece chunk in chunks)
            {
                Assert.Equal(offset, chunk.Offset);
                offset += Encoding.UTF8.GetByteCount(chunk.Text);
            }
        }
    }

    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    public void A_file_of_exactly_one_chunk_is_one_chunk_that_ends_the_file(int chunkSize)
    {
        // 18 bytes without a terminator at the end, 19 with one: either way the file fills the chunk exactly.
        string content = chunkSize == 18 ? "1. Apple\n2. Banana" : "1. Apple\n2. Banana\n";

        Piece chunk = Assert.Single(Chunks(Utf8(content), LineEnding.Lf, chunkSize));

        Assert.Equal(content, chunk.Text);
        Assert.True(chunk.EndsFile);
    }

    [Fact]
    public void A_stream_that_cannot_tell_its_length_ends_after_an_empty_read()
    {
        // A pipe cannot say that the chunk took its last byte; the next read, which finds nothing, says it.
        using NonSeekableStream stream = new(Utf8("1. Apple\n2. Banana"));
        ChunkReader reader = new(stream, LineEnding.Lf, chunkSize: 18, maxLineLength: 100);

        Chunk? first = reader.Read();
        Chunk? second = reader.Read();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("1. Apple\n", Encoding.UTF8.GetString(first.Bytes));
        Assert.False(first.EndsFile);
        Assert.Equal("2. Banana", Encoding.UTF8.GetString(second.Bytes));
        Assert.True(second.EndsFile);
        Assert.Null(reader.Read());
        ChunkReader.Release(first);
        ChunkReader.Release(second);
    }

    [Theory]
    [InlineData("lf", "\n")]
    [InlineData("crlf", "\r\n")]
    public void Each_chunk_can_carry_the_last_line_of_the_chunk_before(string ending, string terminator)
    {
        LineEnding lineEnding = ending == "crlf" ? LineEnding.CrLf : LineEnding.Lf;
        IEnumerable<string> fileLines = Enumerable.Range(1, 60)
            .Select(i => $"{i}. Line {new string('x', i % 7)}{terminator}");
        string content = string.Concat(fileLines);
        using MemoryStream stream = new(Utf8(content));
        ChunkReader reader = new(stream, lineEnding, chunkSize: 37, maxLineLength: 100, keepPreviousLine: true);
        string? lastLineBefore = null;
        int chunks = 0;

        while (reader.Read() is Chunk chunk)
        {
            chunks++;
            string bytes = Encoding.UTF8.GetString(chunk.Bytes);

            // The copy in front is exactly the last line of the chunk before, terminator included; the first chunk
            // has none.
            Assert.Equal(lastLineBefore ?? "", Encoding.UTF8.GetString(chunk.PreviousLine));
            Assert.Equal(chunk.PreviousLine.Length, chunk.BytesStart);
            string[] lines = bytes.Split(terminator, StringSplitOptions.RemoveEmptyEntries);
            lastLineBefore = lines[^1] + terminator;
            ChunkReader.Release(chunk);
        }

        Assert.True(chunks > 20);
    }

    [Fact]
    public void Without_the_order_check_no_line_is_carried()
    {
        using MemoryStream stream = new(Utf8("1. Apple\n2. Banana\n3. Cherry\n"));
        ChunkReader reader = new(stream, LineEnding.Lf, chunkSize: 9, maxLineLength: 100);

        while (reader.Read() is Chunk chunk)
        {
            Assert.True(chunk.PreviousLine.IsEmpty);
            Assert.Equal(0, chunk.BytesStart);
            ChunkReader.Release(chunk);
        }
    }

    [Fact]
    public void A_file_that_ends_with_a_terminator_has_no_empty_last_chunk()
    {
        List<Piece> chunks = Chunks(Utf8("1. Apple\n2. Banana\n"), LineEnding.Lf, chunkSize: 9);

        Assert.Equal(["1. Apple\n", "2. Banana\n"], chunks.Select(chunk => chunk.Text));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4096)]
    public void A_byte_order_mark_is_not_part_of_the_first_chunk(int chunkSize)
    {
        byte[] file = [0xEF, 0xBB, 0xBF, .. "1. Apple\n2. Banana\n"u8];
        using MemoryStream stream = new(file);
        ChunkReader reader = new(stream, LineEnding.Lf, chunkSize, maxLineLength: 100);

        Chunk? first = reader.Read();

        // The offset counts the bytes of the file, the mark included.
        Assert.NotNull(first);
        Assert.True(reader.HasByteOrderMark);
        Assert.Equal(3, first.Offset);
        Assert.StartsWith("1. Apple\n", Encoding.UTF8.GetString(first.Bytes), StringComparison.Ordinal);
        ChunkReader.Release(first);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF })]
    public void A_file_without_lines_has_no_chunks(byte[] file)
    {
        Assert.Empty(Chunks(file, LineEnding.Lf, chunkSize: 4096));
        Assert.Empty(Chunks(file, LineEnding.CrLf, chunkSize: 1));
    }

    [Fact]
    public void A_mark_later_in_the_file_stays_in_its_chunk()
    {
        byte[] file = [.. "1. Apple\n"u8, 0xEF, 0xBB, 0xBF, .. "2. Banana\n"u8];

        List<Piece> chunks = Chunks(file, LineEnding.Lf, chunkSize: 9);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("\uFEFF2. Banana\n", chunks[1].Text);
        Assert.Equal(9, chunks[1].Offset);
    }

    [Fact]
    public void A_line_without_an_end_within_the_limit_is_a_chunk_of_its_own_kind()
    {
        byte[] file = Utf8("1. Apple\n2. " + new string('x', 1000) + "\n3. Cherry\n");

        List<Piece> chunks = Chunks(file, LineEnding.Lf, chunkSize: 16, maxLineLength: 100);

        // The first line, then the line that has no end within a hundred bytes; nothing is read after it.
        Assert.Equal(2, chunks.Count);
        Assert.Equal("1. Apple\n", chunks[0].Text);
        Assert.True(chunks[1].IsLineWithoutEnd);
        Assert.Equal(9, chunks[1].Offset);
        Assert.Empty(chunks[1].Text);
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkReader(Stream.Null, LineEnding.Lf, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkReader(Stream.Null, LineEnding.Lf, 10, 0));
    }
}
