using System.Globalization;

using FileSort.Common;

namespace FileSort.Engine.Lines.Parsing;

/// <summary>
/// A line of the input is longer than the sorter accepts. A file read with the wrong line ending can look like one
/// line of a hundred gigabytes, so the line ending is part of the report.
/// </summary>
public sealed class LineTooLongException : Exception
{
    public LineTooLongException()
    {
    }

    public LineTooLongException(string message)
        : base(message)
    {
    }

    public LineTooLongException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal LineTooLongException(long lineNumber, long offset, int limit, LineEnding lineEnding)
        : base(string.Create(
            CultureInfo.InvariantCulture, $"Line {lineNumber}, at offset {offset}, is longer than {limit} bytes."))
    {
        LineNumber = lineNumber;
        Offset = offset;
        Limit = limit;
        LineEnding = lineEnding;
    }

    /// <summary>The number of the line, counted from 1.</summary>
    public long LineNumber { get; }

    /// <summary>The offset of the first byte of the line from the start of the file, a byte order mark
    /// included.</summary>
    public long Offset { get; }

    /// <summary>The longest line accepted, in bytes, its terminator not counted.</summary>
    public int Limit { get; }

    /// <summary>The line ending that the input was read with.</summary>
    public LineEnding LineEnding { get; }
}
