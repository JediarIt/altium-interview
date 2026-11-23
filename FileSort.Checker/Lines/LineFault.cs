namespace FileSort.Checker.Lines;

/// <summary>Why a line is not a valid <c>Number. String</c> line.</summary>
internal enum LineFault
{
    /// <summary>The line is valid.</summary>
    None,

    /// <summary>
    /// The line contains a line feed without a carriage return before it, in a file read with <c>\r\n</c>. The
    /// reader finds it while it looks for the end of the line, so it comes before every other reason.
    /// </summary>
    BareLineFeed,

    /// <summary>The line has no bytes.</summary>
    Empty,

    /// <summary>The line does not start with a digit, so it has no Number.</summary>
    NoNumber,

    /// <summary>The digits are not followed by a period and a space.</summary>
    NoSeparator,

    /// <summary>The String is not well-formed UTF-8.</summary>
    IllFormedString,
}
