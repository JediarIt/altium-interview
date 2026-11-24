using System.Text;

namespace FileSort.Common;

/// <summary>
/// Constants of the <c>Number. String</c> line format. The format itself is specified in
/// <c>openspec/specs/line-format</c>.
/// </summary>
public static class LineFormat
{
    /// <summary>
    /// The largest Number that fits in 64 bits. It is not a limit of the format: a Number is valid with any count of
    /// digits and any value. It is the end of the 64-bit range: the largest Number that the generator writes, and
    /// the largest that a comparison of Numbers as 64-bit integers can handle. The range is defined here and
    /// nowhere else.
    /// </summary>
    public const ulong MaxNumber = ulong.MaxValue;

    /// <summary>
    /// The count of digits of <see cref="MaxNumber"/>: a Number of up to 19 digits always fits in 64 bits, one of 20
    /// digits fits unless it passes <see cref="MaxNumber"/>. It is not a limit of the format: a longer Number is
    /// valid, and its value may or may not fit in 64 bits, depending on its leading zeros.
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
