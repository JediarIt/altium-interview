using System.Globalization;

using FileSort.Common;

namespace FileSort.Engine.Lines.Parsing;

/// <summary>
/// A line of the input is not valid. The sort stops at the first such line in the order of the file, writes no output
/// and leaves no temporary file.
/// </summary>
public sealed class InvalidLineException : Exception
{
    public InvalidLineException()
    {
    }

    public InvalidLineException(string message)
        : base(message)
    {
    }

    public InvalidLineException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal InvalidLineException(long lineNumber, long offset, LineFault fault, LineEnding lineEnding)
        : base(string.Create(
            CultureInfo.InvariantCulture, $"Line {lineNumber}, at offset {offset}, is not valid: {fault}."))
    {
        LineNumber = lineNumber;
        Offset = offset;
        Fault = fault;
        LineEnding = lineEnding;
    }

    /// <summary>The number of the line, counted from 1.</summary>
    public long LineNumber { get; }

    /// <summary>The offset of the first byte of the line from the start of the file, a byte order mark
    /// included.</summary>
    public long Offset { get; }

    public LineFault Fault { get; }

    /// <summary>The line ending that the input was read with.</summary>
    public LineEnding LineEnding { get; }
}
