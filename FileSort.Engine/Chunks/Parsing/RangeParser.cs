using FileSort.Common;
using FileSort.Engine.Lines;
using FileSort.Engine.Lines.Parsing;

namespace FileSort.Engine.Chunks.Parsing;

/// <summary>
/// Parses the lines of one range of a chunk into its slice of the index, on the thread of that range. It stops at the
/// first line that stops the sort, because no later line of the range can be the first problem of the file.
/// </summary>
internal static class RangeParser
{
    // The cancellation is looked at once per this many lines: often enough to stop within a fraction of a second,
    // rarely enough to cost nothing.
    private const int LinesBetweenCancellationChecks = 1 << 16;

    /// <summary>
    /// Parses the bytes from <paramref name="start"/> to <paramref name="end"/>: whole lines, each ending with a line
    /// feed, except the last line of a file without a terminator.
    /// </summary>
    /// <remarks>
    /// A line is too long when its content, the terminator not counted, passes the limit. In a file read with
    /// <c>\r\n</c>, a line feed without a carriage return before it is not a line end but a byte of the line, which
    /// makes the line invalid; when it lies at or beyond the limit, the line is too long before it, and it is reported
    /// as such.
    /// </remarks>
    public static RangeResult Parse(
        byte[] bytes, int start, int end, LineEnding lineEnding, int maxLineLength, Span<IndexEntry> slice,
        CancellationToken cancellationToken)
    {
        int position = start;
        int line = 0;
        while (position < end)
        {
            if (line % LinesBetweenCancellationChecks == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            LineBounds bounds = Bounds(bytes, position, end, lineEnding);
            int length = bounds.ContentEnd - position;
            if (bounds.BareLineFeed)
            {
                LineFault fault = length < maxLineLength ? LineFault.BareLineFeed : LineFault.None;
                return new RangeResult(line, new RangeProblem(position, line, fault));
            }

            if (length > maxLineLength)
            {
                return new RangeResult(line, new RangeProblem(position, line, LineFault.None));
            }

            LineFault invalid = LineParser.Parse(bytes.AsSpan(position, length), (uint)position, out slice[line]);
            if (invalid != LineFault.None)
            {
                return new RangeResult(line, new RangeProblem(position, line, invalid));
            }

            line++;
            position = bounds.Next;
        }

        return new RangeResult(line, null);
    }

    // Where the content of the line at the position ends and the next line starts. A line without a line feed is the
    // last line of a file without a terminator. In a \r\n file a line feed is a line end only after a carriage
    // return; otherwise the content runs up to the line feed, which is then a byte of the line.
    private static LineBounds Bounds(byte[] bytes, int position, int end, LineEnding lineEnding)
    {
        int lineFeed = bytes.AsSpan(position, end - position).IndexOf(LineFormat.LineFeed);
        if (lineFeed < 0)
        {
            return new LineBounds(end, end, BareLineFeed: false);
        }

        int at = position + lineFeed;
        if (lineEnding == LineEnding.Lf)
        {
            return new LineBounds(at, at + 1, BareLineFeed: false);
        }

        bool carriageReturn = lineFeed > 0 && bytes[at - 1] == LineFormat.CarriageReturn;
        return new LineBounds(carriageReturn ? at - 1 : at, at + 1, BareLineFeed: !carriageReturn);
    }

    /// <summary>The bounds of one line in the chunk buffer.</summary>
    private readonly record struct LineBounds(int ContentEnd, int Next, bool BareLineFeed);
}
