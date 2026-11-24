using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace FileSort.Engine.Lines;

/// <summary>
/// The first 8 bytes of a String as one number, so that most comparisons of Strings are one comparison of two
/// numbers. The first byte of the String is the highest byte of the number (big-endian), so the numbers compare the
/// way the bytes compare one by one, as unsigned bytes.
/// </summary>
internal static class StringPrefix
{
    /// <summary>The bytes of a String that the prefix holds.</summary>
    public const int Length = sizeof(ulong);

    /// <summary>
    /// <c>Apple pie</c> gives 0x4170706C65207069, the bytes of <c>Apple pi</c>. A String shorter than 8 bytes is
    /// padded with zero bytes after its end: <c>AB</c> gives 0x4142000000000000, and the empty String 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Read(ReadOnlySpan<byte> text)
    {
        return text.Length >= Length ? BinaryPrimitives.ReadUInt64BigEndian(text) : ReadPadded(text);
    }

    // A String of fewer than 8 bytes: copied in front of zero bytes, then read like a long one.
    private static ulong ReadPadded(ReadOnlySpan<byte> text)
    {
        Span<byte> padded = stackalloc byte[Length];
        padded.Clear();
        text.CopyTo(padded);
        return BinaryPrimitives.ReadUInt64BigEndian(padded);
    }
}
