using FileSort.Generator.Content.Lines;
using FileSort.Generator.Random;

namespace FileSort.Generator.Content.Blocks;

/// <summary>
/// Fills a block with whole lines. The content of a block is a pure function of the content options, the block index
/// and the capacity of the buffer, so any thread can produce any block and the file is the same for every thread
/// count. An instance is immutable and is shared by all threads.
/// </summary>
internal sealed class BlockGenerator
{
    private readonly ulong _seed;
    private readonly LineComposer _lines;
    private readonly int _reserve;

    public BlockGenerator(ContentOptions options)
    {
        _seed = options.Seed;
        _lines = new LineComposer(options);
        _reserve = _lines.MaxLineLength + StringComposer.Slack;
    }

    /// <summary>The length of the longest line that the options can produce, terminator included.</summary>
    public int MaxLineLength => _lines.MaxLineLength;

    /// <summary>The fewest bytes that a block of the given capacity holds.</summary>
    public int MinBlockLength(int capacity)
    {
        return capacity - _reserve + 1;
    }

    /// <summary>
    /// Fills the buffer with the lines of one block: lines are added while the longest possible line still fits.
    /// The block ends with a terminator.
    /// </summary>
    public BlockContent Generate(long blockIndex, Span<byte> buffer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(blockIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(buffer.Length, _reserve);

        Xoshiro256 random = Xoshiro256.Create(_seed, (ulong)blockIndex);
        int limit = buffer.Length - _reserve;
        int position = 0;
        int lines = 0;

        while (position <= limit)
        {
            position += _lines.WriteLine(ref random, buffer[position..]);
            lines++;
        }

        return new BlockContent(position, lines);
    }
}
