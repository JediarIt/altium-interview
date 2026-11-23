using FileSort.Checker.Lines;

namespace FileSort.Checker.Checks;

/// <summary>
/// Finds the line ending of a file from its first line feed: a carriage return right before it means <c>\r\n</c>,
/// anything else means <c>\n</c>, and so does a file without a line feed. Only the input is asked: in a sorted
/// file the first line can be one whose String ends with a carriage return, and it would look like <c>\r\n</c>.
/// </summary>
internal static class LineEndingDetector
{
    private const byte LineFeed = (byte)'\n';
    private const byte CarriageReturn = (byte)'\r';
    private const int BufferSize = 64 * 1024;

    /// <summary>
    /// Reads the stream from its start to the first line feed and returns to the start. The search ends after
    /// <paramref name="searchLimit"/> bytes: a first line that long is longer than the checker reads, and the scan
    /// reports it.
    /// </summary>
    public static LineEnding Detect(Stream stream, long searchLimit)
    {
        byte[] buffer = new byte[BufferSize];
        bool afterCarriageReturn = false;
        long searched = 0;
        stream.Position = 0;
        try
        {
            while (searched <= searchLimit && stream.Read(buffer) is > 0 and int read)
            {
                ReadOnlySpan<byte> bytes = buffer.AsSpan(0, read);
                int feed = bytes.IndexOf(LineFeed);
                if (feed >= 0)
                {
                    // A line feed at the very start of what was read follows the last byte of the read before.
                    bool carriageReturn = feed > 0 ? bytes[feed - 1] == CarriageReturn : afterCarriageReturn;
                    return carriageReturn ? LineEnding.CrLf : LineEnding.Lf;
                }

                afterCarriageReturn = bytes[^1] == CarriageReturn;
                searched += read;
            }

            return LineEnding.Lf;
        }
        finally
        {
            stream.Position = 0;
        }
    }
}
