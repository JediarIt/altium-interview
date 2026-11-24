using FileSort.Common;
using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Tests.Format;

namespace FileSort.Tests.Generator.Content.Blocks;

/// <summary>The block generator, and the scenarios of "Valid output" and "Line ending".</summary>
public class BlockGeneratorTests
{
    private const int Capacity = ContentFormat.BlockCapacity;

    private const EdgeCases AllButCarriageReturn = EdgeCases.All & ~EdgeCases.CarriageReturn;

    private static byte[] Block(ContentOptions options, long index, int capacity = Capacity)
    {
        byte[] buffer = new byte[capacity];
        BlockContent content = new BlockGenerator(options).Generate(index, buffer);
        return buffer[..content.Bytes];
    }

    [Fact]
    public void The_same_inputs_give_the_same_bytes()
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.All);

        Assert.Equal(Block(options, 5), Block(options, 5));
    }

    [Fact]
    public void Different_block_indexes_give_different_bytes()
    {
        ContentOptions options = TestContent.Options();

        Assert.NotEqual(Block(options, 0), Block(options, 1));
    }

    [Fact]
    public void Every_content_option_changes_the_bytes()
    {
        byte[] reference = Block(TestContent.Options(), 0);

        Assert.NotEqual(reference, Block(TestContent.Options(seed: 2), 0));
        Assert.NotEqual(reference, Block(TestContent.Options(duplicates: 31), 0));
        Assert.NotEqual(reference, Block(TestContent.Options(words: "1-6"), 0));
        Assert.NotEqual(reference, Block(TestContent.Options(edgeCases: EdgeCases.Empty), 0));
        Assert.NotEqual(reference, Block(TestContent.Options(lineEnding: LineEnding.CrLf), 0));
    }

    [Theory]
    [InlineData(LineEnding.Lf)]
    [InlineData(LineEnding.CrLf)]
    public void A_block_is_nearly_full_and_ends_with_a_terminator(LineEnding lineEnding)
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.All, lineEnding: lineEnding);
        BlockGenerator generator = new(options);
        byte[] buffer = new byte[Capacity];

        for (long index = 0; index < 8; index++)
        {
            BlockContent content = generator.Generate(index, buffer);
            ReadOnlySpan<byte> block = buffer.AsSpan(0, content.Bytes);

            Assert.InRange(content.Bytes, generator.MinBlockLength(Capacity), Capacity);
            Assert.True(block.EndsWith(lineEnding.Terminator()));
            Assert.Equal(content.Lines, block.Count((byte)'\n'));
        }
    }

    [Fact]
    public void A_buffer_smaller_than_the_longest_line_is_rejected()
    {
        BlockGenerator generator = new(TestContent.Options());

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(0, new byte[generator.MaxLineLength]));
    }

    [Fact]
    public void Without_the_category_carriage_return_no_String_contains_one()
    {
        // With \n nothing writes a carriage return at all; with \r\n it occurs only inside terminators.
        Assert.DoesNotContain((byte)'\r', Block(TestContent.Options(edgeCases: AllButCarriageReturn), 0));

        ContentOptions crlf = TestContent.Options(edgeCases: AllButCarriageReturn, lineEnding: LineEnding.CrLf);
        List<NaiveLine> lines = TestContent.Parse(Block(crlf, 0), LineEnding.CrLf);
        Assert.All(lines, line => Assert.DoesNotContain((byte)'\r', line.Text));
    }

    // Requirement: Valid output

    [Fact]
    public void Default_options()
    {
        byte[] content = TestContent.Generate(TestContent.Options(), 4 * TestContent.Megabyte);

        Assert.NotEmpty(TestContent.Parse(content, LineEnding.Lf));
        Assert.False(content.AsSpan().StartsWith(LineFormat.ByteOrderMark));
    }

    [Fact]
    public void Every_edge_case_at_once()
    {
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.All, lineEnding: LineEnding.CrLf);

        byte[] content = TestContent.Generate(options, 4 * TestContent.Megabyte);

        Assert.NotEmpty(TestContent.Parse(content, LineEnding.CrLf));
        Assert.False(content.AsSpan().StartsWith(LineFormat.ByteOrderMark));
    }

    [Theory]
    [InlineData(0, "1", (int)EdgeCases.None)]
    [InlineData(100, "1", (int)EdgeCases.All)]
    [InlineData(50, "7-9", (int)(EdgeCases.Unicode | EdgeCases.Separator))]
    [InlineData(30, "1024", (int)EdgeCases.All)]
    [InlineData(100, "1000-1024", (int)EdgeCases.None)]
    public void Every_combination_of_options_gives_valid_lines(int duplicates, string words, int edgeCases)
    {
        foreach (LineEnding lineEnding in new[] { LineEnding.Lf, LineEnding.CrLf })
        {
            ContentOptions options = TestContent.Options(7, duplicates, words, (EdgeCases)edgeCases, lineEnding);

            Assert.NotEmpty(TestContent.Lines(options, 2 * TestContent.Megabyte));
        }
    }

    // Requirement: Line ending

    [Fact]
    public void Default_on_Windows()
    {
        ContentOptions options = new() { Seed = 1, EdgeCases = AllButCarriageReturn };

        Assert.Equal(LineEnding.Lf, options.LineEnding);
        Assert.DoesNotContain((byte)'\r', TestContent.Generate(options, TestContent.Megabyte));
    }

    [Theory]
    [InlineData((int)EdgeCases.None)]
    [InlineData((int)AllButCarriageReturn)]
    public void Carriage_return_and_line_feed(int edgeCases)
    {
        byte[] content = TestContent.Generate(
            TestContent.Options(edgeCases: (EdgeCases)edgeCases, lineEnding: LineEnding.CrLf), TestContent.Megabyte);

        Assert.True(content.AsSpan().EndsWith("\r\n"u8));
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                Assert.True(i > 0 && content[i - 1] == '\r', $"A line feed without a carriage return at offset {i}.");
            }
            else if (content[i] == '\r')
            {
                bool lineFeedFollows = i + 1 < content.Length && content[i + 1] == '\n';
                Assert.True(lineFeedFollows, $"A carriage return without a line feed at offset {i}.");
            }
        }
    }

    [Theory]
    [InlineData(LineEnding.Lf)]
    [InlineData(LineEnding.CrLf)]
    public void Line_ending_of_the_first_line(LineEnding lineEnding)
    {
        // Seeds with the category in many lines: whatever the first line rolls, it never ends with a carriage return.
        for (ulong seed = 1; seed <= 64; seed++)
        {
            ContentOptions options = TestContent.Options(
                seed: seed, edgeCases: EdgeCases.CarriageReturn, lineEnding: lineEnding);
            byte[] block = Block(options, 0);
            int firstLineFeed = Array.IndexOf(block, (byte)'\n');

            if (lineEnding == LineEnding.Lf)
            {
                Assert.NotEqual((byte)'\r', block[firstLineFeed - 1]);
            }
            else
            {
                Assert.Equal((byte)'\r', block[firstLineFeed - 1]);
                Assert.NotEqual((byte)'\r', block[firstLineFeed - 2]);
            }
        }
    }

    [Fact]
    public void The_first_line_of_another_block_may_end_with_a_carriage_return()
    {
        // About one block in 16 rolls a carriage return for its first line, and only the first block drops it.
        ContentOptions options = TestContent.Options(edgeCases: EdgeCases.CarriageReturn);

        Assert.Contains(Enumerable.Range(1, 1000), index =>
        {
            byte[] block = Block(options, index, capacity: 4096);
            return block[Array.IndexOf(block, (byte)'\n') - 1] == '\r';
        });
    }
}
