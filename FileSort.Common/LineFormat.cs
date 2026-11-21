using System.Text;

namespace FileSort.Common;

/// <summary>
/// Constants of the <c>Number. String</c> line format. The format itself is specified in
/// <c>openspec/specs/line-format</c>.
/// </summary>
public static class LineFormat
{
    /// <summary>The largest value of a Number. The two limits of a Number are defined here and nowhere else.</summary>
    public const ulong MaxNumber = ulong.MaxValue;

    /// <summary>
    /// The longest Number, in digits: the length of <see cref="MaxNumber"/>. Leading zeros count, so a Number with
    /// zeros in front still has at most this many digits.
    /// </summary>
    public const int MaxNumberDigits = 20;

    /// <summary>The length of <see cref="Separator"/> in bytes.</summary>
    public const int SeparatorLength = 2;

    public const byte LineFeed = (byte)'\n';

    public const byte CarriageReturn = (byte)'\r';

    /// <summary>Stands between the Number and the String: a period and one space.</summary>
    public static ReadOnlySpan<byte> Separator => ". "u8;

    /// <summary>
    /// The UTF-8 byte order mark, the bytes <c>EF BB BF</c>, as the framework defines it. A file may start with it;
    /// it is not part of the first line.
    /// </summary>
    public static ReadOnlySpan<byte> ByteOrderMark => Encoding.UTF8.Preamble;
}
