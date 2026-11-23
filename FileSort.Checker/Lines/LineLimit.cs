using System.Globalization;

namespace FileSort.Checker.Lines;

/// <summary>
/// The limit on the length of a line. It is a limit of the tool, not of the format: it protects memory when a file
/// is read with the wrong line ending, in which case a "line" can be the whole file.
/// </summary>
internal static class LineLimit
{
    /// <summary>What is said about a line above the limit: where it is, the limit, and the likely cause.</summary>
    /// <param name="lineNumber">The number of the line, from 1.</param>
    /// <param name="offset">The offset of the first byte of the line from the start of the file, from 0.</param>
    /// <param name="limit">The longest line that is read, in bytes without its terminator.</param>
    public static string Describe(long lineNumber, long offset, int limit)
    {
        string line = string.Create(
            CultureInfo.InvariantCulture,
            $"line {lineNumber}, which starts at byte {offset}, is longer than {limit} bytes");
        return line + ", the longest line that the checker reads. A wrong line ending is a likely cause: see --line-ending.";
    }
}
