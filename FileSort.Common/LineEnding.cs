namespace FileSort.Common;

/// <summary>The line ending of a file. A file has one line ending for all its lines.</summary>
public enum LineEnding
{
    /// <summary>A line feed, <c>\n</c>.</summary>
    Lf,

    /// <summary>A carriage return followed by a line feed, <c>\r\n</c>.</summary>
    CrLf,
}
