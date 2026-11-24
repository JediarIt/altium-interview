using FileSort.Common;

namespace FileSort.Engine.Chunks.Parsing;

/// <summary>
/// The bytes of a chunk cut into ranges, one per thread, each made of whole lines, and the count of lines of every
/// range (D4 of the design, "Counting before parsing"). The counts give every range its slice of the index before a
/// line is parsed, and show whether all the entries fit.
/// </summary>
internal sealed class LineRanges
{
    private readonly int[] _starts;
    private readonly int[] _lines;

    private LineRanges(int[] starts, int[] lines)
    {
        _starts = starts;
        _lines = lines;
    }

    public int Count => _lines.Length;

    /// <summary>The lines of all ranges together.</summary>
    public long TotalLines => _lines.Sum(lines => (long)lines);

    public int Start(int range) => _starts[range];

    public int End(int range) => _starts[range + 1];

    public int Lines(int range) => _lines[range];

    /// <summary>
    /// Cuts the first <paramref name="length"/> bytes into ranges of about equal size and counts their lines on all
    /// threads. A range ends right after a line feed; a range may be empty when lines are long. The last line of the
    /// chunk has no terminator when <paramref name="lastLineUnterminated"/> is set, at the end of a file without one.
    /// </summary>
    public static LineRanges Split(byte[] bytes, int length, bool lastLineUnterminated, int count, ParallelWork work)
    {
        int[] starts = new int[count + 1];
        for (int range = 1; range < count; range++)
        {
            // An equal share of the bytes, moved on to the start of the next line, and never behind the range before.
            int equalShare = (int)((long)length * range / count);
            starts[range] = NextLineStart(bytes, Math.Max(starts[range - 1], equalShare), length);
        }

        starts[count] = length;

        // A vectorized search counts the line feeds; the line of a file that ends without one counts too.
        int[] lines = new int[count];
        work.For(count, range => lines[range] = CountLineFeeds(bytes, starts[range], starts[range + 1]));
        if (lastLineUnterminated && length > 0)
        {
            lines[RangeOf(starts, length - 1)]++;
        }

        return new LineRanges(starts, lines);
    }

    // The start of the line after the one that holds the byte at the position: right after the next line feed.
    private static int NextLineStart(byte[] bytes, int position, int length)
    {
        int lineFeed = bytes.AsSpan(position, length - position).IndexOf(LineFormat.LineFeed);
        return lineFeed < 0 ? length : position + lineFeed + 1;
    }

    // The range that holds a byte: the last one that starts at or before it, of those that are not empty.
    private static int RangeOf(int[] starts, int position)
    {
        int range = starts.Length - 2;
        while (starts[range] > position)
        {
            range--;
        }

        return range;
    }

    private static int CountLineFeeds(byte[] bytes, int start, int end)
    {
        return bytes.AsSpan(start, end - start).Count(LineFormat.LineFeed);
    }
}
