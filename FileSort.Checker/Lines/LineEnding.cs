namespace FileSort.Checker.Lines;

/// <summary>
/// The line ending that a file is read with. A file has one for all its lines, and only that terminator ends a
/// line. This is the checker's own type: the checker shares no line code with the other programs.
/// </summary>
internal enum LineEnding
{
    /// <summary>A line feed, <c>\n</c>.</summary>
    Lf,

    /// <summary>A carriage return followed by a line feed, <c>\r\n</c>.</summary>
    CrLf,
}
