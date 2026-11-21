using FileSort.Common;
using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;

namespace FileSort.Generator.Pipeline;

/// <summary>
/// Generates blocks in parallel and hands them to a sink in order. At most <c>threads</c> blocks are in flight: the
/// coordinator waits for the oldest one, so the order of the output comes from the order of waiting, the parallelism
/// from the tasks, and the bound on memory from the size of the window. A sink slower than generation makes finished
/// blocks wait, and no new work starts.
/// </summary>
internal static class GenerationPipeline
{
    /// <summary>
    /// Writes the content of at least <paramref name="size"/> bytes to the sink: blocks 0, 1, 2 and so on, cut at the
    /// first line boundary at or after the size.
    /// </summary>
    /// <param name="generator">Produces the content of a block from its index.</param>
    /// <param name="size">The requested size in bytes.</param>
    /// <param name="threads">The upper limit on blocks generated at the same time.</param>
    /// <param name="sink">Receives the output.</param>
    /// <param name="cancellationToken">Checked for every block.</param>
    /// <param name="progress">Called with the number of bytes written so far, after every block.</param>
    /// <param name="blockCapacity">
    /// The capacity of a block. The content depends on it, so only experiments pass anything but the default.
    /// </param>
    public static GenerationResult Run(
        BlockGenerator generator,
        long size,
        int threads,
        IBlockSink sink,
        CancellationToken cancellationToken,
        Action<long>? progress = null,
        int blockCapacity = ContentFormat.BlockCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(threads, 1);

        // Blocks end on line boundaries, so their number is not known in advance; this is an upper bound. It keeps
        // a small file from starting blocks that can never be needed.
        long maxBlocks = (size / generator.MinBlockLength(blockCapacity)) + 1;
        using BlockWindow window = new(generator, blockCapacity, threads, maxBlocks, cancellationToken);
        long written = 0;
        long lines = 0;

        while (written < size)
        {
            (byte[] buffer, BlockContent block) = window.TakeNext();
            cancellationToken.ThrowIfCancellationRequested();

            ReadOnlySpan<byte> data = buffer.AsSpan(0, block.Bytes);
            int blockLines = block.Lines;
            if (data.Length >= size - written)
            {
                // The last block of the file.
                data = ThroughLineEnd(data, size - written);
                blockLines = data.Count(LineFormat.LineFeed);
            }

            sink.Write(data);
            written += data.Length;
            lines += blockLines;
            window.Recycle(buffer);
            progress?.Invoke(written);
        }

        return new GenerationResult(written, lines);
    }

    /// <summary>
    /// The start of a block through the first line end at or after <paramref name="minLength"/> bytes. A String
    /// never contains a line feed, so every line feed ends a line. With the block <c>1. A\n22. B\n3. C\n</c> and a
    /// minimum of 7 bytes the result is <c>1. A\n22. B\n</c>, 11 bytes: byte 7 is in the middle of the second
    /// line, so that line is kept whole.
    /// </summary>
    private static ReadOnlySpan<byte> ThroughLineEnd(ReadOnlySpan<byte> block, long minLength)
    {
        int lastRequired = (int)minLength - 1;
        int lineEnd = lastRequired + block[lastRequired..].IndexOf(LineFormat.LineFeed);
        return block[..(lineEnd + 1)];
    }
}
