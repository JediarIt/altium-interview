using System.Numerics;
using System.Text;

namespace FileSort.Tests.Format;

/// <summary>
/// A line as the naive parser sees it: the value of the Number, which has no upper limit, the digits as they were
/// written, and the bytes of the String.
/// </summary>
internal sealed record NaiveLine(BigInteger Number, string NumberText, byte[] Text)
{
    public string TextAsString => Encoding.UTF8.GetString(Text);

    /// <summary>The bytes of the line as it was read, without a terminator.</summary>
    public byte[] ToBytes()
    {
        return [.. Encoding.ASCII.GetBytes(NumberText), .. ". "u8, .. Text];
    }
}
