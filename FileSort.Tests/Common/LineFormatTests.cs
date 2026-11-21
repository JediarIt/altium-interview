using System.Globalization;

using FileSort.Common;

namespace FileSort.Tests.Common;

/// <summary>The constants of the line format that the programs share.</summary>
public class LineFormatTests
{
    [Fact]
    public void Format_constants()
    {
        Assert.Equal(". "u8.ToArray(), LineFormat.Separator.ToArray());
        Assert.Equal(LineFormat.SeparatorLength, LineFormat.Separator.Length);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, LineFormat.ByteOrderMark.ToArray());
        Assert.Equal(ulong.MaxValue, LineFormat.MaxNumber);
        Assert.Equal(LineFormat.MaxNumberDigits, LineFormat.MaxNumber.ToString(CultureInfo.InvariantCulture).Length);
        Assert.Equal((byte)'\n', LineFormat.LineFeed);
        Assert.Equal((byte)'\r', LineFormat.CarriageReturn);
    }

    [Fact]
    public void Line_endings_and_their_bytes()
    {
        Assert.Equal("\n"u8.ToArray(), LineEnding.Lf.Terminator().ToArray());
        Assert.Equal("\r\n"u8.ToArray(), LineEnding.CrLf.Terminator().ToArray());
        Assert.Equal(1, LineEnding.Lf.TerminatorLength());
        Assert.Equal(2, LineEnding.CrLf.TerminatorLength());
    }
}
