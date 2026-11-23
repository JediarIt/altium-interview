namespace FileSort.Checker.Lines;

/// <summary>What <see cref="LineSplitter.TryRead"/> found in the bytes it has.</summary>
internal enum LineRead
{
    /// <summary>No line is left.</summary>
    None,

    /// <summary>A whole line, without its terminator.</summary>
    Line,

    /// <summary>
    /// A line feed without a carriage return before it, in a file read with <c>\r\n</c>. The bytes returned are the
    /// line up to and including that line feed; the line is not valid, and reading cannot go on.
    /// </summary>
    BareLineFeed,

    /// <summary>
    /// A line longer than the longest line that is read. No bytes are returned, and reading cannot go on. This is
    /// a limit of the tool, not a fault of the line.
    /// </summary>
    TooLong,
}
