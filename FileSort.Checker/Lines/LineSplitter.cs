namespace FileSort.Checker.Lines;

/// <summary>
/// Splits bytes into lines with one line ending, one line per call, as spans of those bytes: nothing is copied
/// and nothing is allocated. The bytes are a part of a file that starts at the start of a line; when they reach
/// the end of the file, what follows the last terminator is the last line, which needs no terminator.
/// </summary>
internal ref struct LineSplitter
{
    private const byte LineFeed = (byte)'\n';
    private const byte CarriageReturn = (byte)'\r';

    private readonly ReadOnlySpan<byte> _bytes;
    private readonly int _terminatorLength;
    private readonly int _maxLineLength;
    private readonly bool _endsFile;

    // Where the next line starts.
    private int _position;

    /// <param name="bytes">The bytes to split; they start at the start of a line.</param>
    /// <param name="lineEnding">The line ending of the file.</param>
    /// <param name="endsFile">The bytes reach the end of the file.</param>
    /// <param name="maxLineLength">The longest line that is read, in bytes without its terminator.</param>
    public LineSplitter(ReadOnlySpan<byte> bytes, LineEnding lineEnding, bool endsFile, int maxLineLength)
    {
        _bytes = bytes;
        _terminatorLength = lineEnding == LineEnding.CrLf ? 2 : 1;
        _endsFile = endsFile;
        _maxLineLength = maxLineLength;
    }

    /// <summary>The number of the line that <see cref="TryRead"/> found last, from 1 for the first line of the bytes.</summary>
    public int LineNumber { get; private set; }

    /// <summary>The offset of the first byte of that line from the start of the bytes.</summary>
    public int LineOffset { get; private set; }

    /// <summary>Whether that line is followed by a terminator; only the last line of a file may lack it.</summary>
    public bool LineHasTerminator { get; private set; }

    /// <summary>Takes the next line, without its terminator.</summary>
    public LineRead TryRead(out ReadOnlySpan<byte> line)
    {
        line = default;
        ReadOnlySpan<byte> rest = _bytes[_position..];
        if (rest.IsEmpty)
        {
            return LineRead.None;
        }

        int feed = rest.IndexOf(LineFeed);
        return feed >= 0 ? TakeUpToLineFeed(rest, feed, out line) : TakeLastLine(rest, out line);
    }

    // A line feed was found at rest[feed]. With the line ending \n it ends the line. With \r\n it ends the line
    // only after a carriage return; without one it is no terminator, and the line that contains it is not valid.
    private LineRead TakeUpToLineFeed(ReadOnlySpan<byte> rest, int feed, out ReadOnlySpan<byte> line)
    {
        line = default;
        bool terminated = _terminatorLength == 1 || (feed > 0 && rest[feed - 1] == CarriageReturn);
        int length = terminated ? feed + 1 - _terminatorLength : feed;
        Describe(terminated);

        // The limit is on the bytes of a line up to its end, or up to where it stops being a line: it comes
        // before everything else, so a line that is both too long and not valid is too long.
        if (length > _maxLineLength)
        {
            return LineRead.TooLong;
        }

        if (!terminated)
        {
            // The bytes up to the line feed, the line feed included, so that a report can show it.
            line = rest[..(feed + 1)];
            return LineRead.BareLineFeed;
        }

        line = rest[..length];
        _position += feed + 1;
        return LineRead.Line;
    }

    // No line feed is left. At the end of the file the rest is the last line; before that there is no rest,
    // because a part of a file ends with a terminator.
    private LineRead TakeLastLine(ReadOnlySpan<byte> rest, out ReadOnlySpan<byte> line)
    {
        line = default;
        if (!_endsFile)
        {
            return LineRead.None;
        }

        Describe(hasTerminator: false);
        if (rest.Length > _maxLineLength)
        {
            return LineRead.TooLong;
        }

        line = rest;
        _position += rest.Length;
        return LineRead.Line;
    }

    // The line that starts at _position is the one being returned.
    private void Describe(bool hasTerminator)
    {
        LineNumber++;
        LineOffset = _position;
        LineHasTerminator = hasTerminator;
    }
}
