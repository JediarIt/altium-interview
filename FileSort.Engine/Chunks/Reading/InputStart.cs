using FileSort.Common;

namespace FileSort.Engine.Chunks.Reading;

/// <summary>
/// What the start of the input says about all of it: whether it begins with a byte order mark, which is skipped, and,
/// unless one is given, the line ending, taken from the first line feed after the mark: <c>\r\n</c> when a carriage
/// return stands right before it, <c>\n</c> otherwise and when the input has no line feed.
/// </summary>
internal readonly record struct InputStart(bool ByteOrderMark, LineEnding LineEnding)
{
    /// <summary>Where the first line starts: after the byte order mark, if there is one.</summary>
    public long FirstLineOffset => ByteOrderMark ? LineFormat.ByteOrderMark.Length : 0;

    /// <summary>
    /// Reads the start of the input into <paramref name="scratch"/>. The line feed is looked for within the longest
    /// line and its terminator: an input whose first line feed lies further has a first line that is too long with
    /// either line ending, and the sort stops at it whatever the line ending is; it is then <c>\n</c>.
    /// </summary>
    public static InputStart Read(IInputSource input, LineEnding? lineEnding, int maxLineLength, Span<byte> scratch)
    {
        // A byte order mark, a longest line and its terminator, or less if the input or the scratch is shorter.
        int wanted = LineFormat.ByteOrderMark.Length + maxLineLength + 2;
        int length = (int)Math.Min(input.Length, Math.Min(scratch.Length, wanted));
        Span<byte> start = scratch[..input.Read(0, scratch[..length])];

        bool byteOrderMark = start.StartsWith(LineFormat.ByteOrderMark);
        if (byteOrderMark)
        {
            start = start[LineFormat.ByteOrderMark.Length..];
        }

        return new InputStart(byteOrderMark, lineEnding ?? Detect(start));
    }

    private static LineEnding Detect(ReadOnlySpan<byte> start)
    {
        int lineFeed = start.IndexOf(LineFormat.LineFeed);
        return lineFeed > 0 && start[lineFeed - 1] == LineFormat.CarriageReturn ? LineEnding.CrLf : LineEnding.Lf;
    }
}
