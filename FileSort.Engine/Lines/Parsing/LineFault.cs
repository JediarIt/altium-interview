namespace FileSort.Engine.Lines.Parsing;

/// <summary>Why a line of the input is not a valid <c>Number. String</c> line.</summary>
public enum LineFault
{
    /// <summary>The line is valid.</summary>
    None,

    /// <summary>The line has no bytes.</summary>
    Empty,

    /// <summary>The line does not start with a digit, so it has no Number.</summary>
    NoNumber,

    /// <summary>The digits of the Number are not followed by a period and a space.</summary>
    NoSeparator,

    /// <summary>The String is not well-formed UTF-8.</summary>
    IllFormedUtf8,

    /// <summary>
    /// The line contains a line feed without a carriage return before it, in a file read with <c>\r\n</c>: such a
    /// line feed does not end a line, so it is a byte of a String, which a String cannot contain.
    /// </summary>
    BareLineFeed,
}
