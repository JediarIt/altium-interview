using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace FileSort.Generator.Content.Lines;

/// <summary>
/// Writes a Number that fits in 32 bits, in canonical form, without a loop. The generator draws Numbers of every
/// length with equal probability, so a digit loop ends at an unpredictable point on almost every line and the
/// processor mispredicts it; here all eight low digits are computed at once with integer arithmetic, and the leading
/// zeros are shifted out. The result is the same bytes as <see cref="LineWriter.WriteNumber(Span{byte}, ulong)"/>
/// (a test compares them); the choice is recorded in the README of this project, Performance.
/// </summary>
/// <remarks>
/// The digits are built as characters packed into one number, the first character in the lowest byte. A
/// little-endian store writes the lowest byte first, so the number lands in memory as the text: "345" is the bytes
/// 0x33, 0x34, 0x35.
/// </remarks>
internal static class PlainNumberWriter
{
    /// <summary>The bytes that a destination needs, whatever the value: more than the digits themselves.</summary>
    public const int RequiredLength = 10;

    private const uint EightDigits = 100_000_000;

    /// <summary>
    /// Writes the digits and returns their number. Bytes after them, up to <see cref="RequiredLength"/> in total,
    /// may be overwritten.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Write(Span<byte> destination, uint value)
    {
        int digits = LineWriter.CountDigits(value);

        if (value < EightDigits)
        {
            // 345 is "00000345" without its first five characters. The store writes "345" and five zero bytes,
            // which the rest of the line overwrites.
            BinaryPrimitives.WriteUInt64LittleEndian(destination, DropFirstCharacters(EightAsciiDigits(value), 8 - digits));
            return digits;
        }

        // Nine or ten digits: the one or two above the low eight come first. 4294967295 is "42" and then "94967295";
        // 123456789 is "01" without its zero and then "23456789", which starts at the second byte.
        uint top = value / EightDigits;
        BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)DropFirstCharacters(TwoAsciiDigits(top), 10 - digits));
        BinaryPrimitives.WriteUInt64LittleEndian(destination[(digits - 8)..], EightAsciiDigits(value - (top * EightDigits)));
        return digits;
    }

    /// <summary>
    /// Removes the first <paramref name="count"/> characters from characters packed into a number, the first in the
    /// lowest byte: a shift right by whole bytes drops them from the front. "00000345" without its first five
    /// characters is "345".
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong DropFirstCharacters(ulong characters, int count)
    {
        return characters >> (count * 8);
    }

    /// <summary>
    /// The two decimal digits of a value below 100 as characters, the first in the lowest byte: 42 becomes "42" and
    /// 7 becomes "07".
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint TwoAsciiDigits(uint value)
    {
        // value / 10 without a division, exact below 100: 103 / 1024 is a little more than 1/10.
        uint tens = (value * 103) >> 10;
        uint ones = value - (tens * 10);

        // The tens digit goes into the lower byte and the ones digit into the upper one; "| 0x3030" turns the two
        // bytes into characters, as ToCharacters does for eight.
        return tens | (ones << 8) | 0x3030;
    }

    /// <summary>
    /// The eight decimal digits of a value below 10^8 as characters, the first in the lowest byte. The value is
    /// split in halves three times: into two numbers of four digits, four of two digits and eight digits. Each split
    /// divides all halves at once by a multiplication, in separate bit lanes of one 64-bit word.
    /// </summary>
    /// <remarks>
    /// A lane is a group of bits of the word that holds one number. One multiplication works on all lanes at once
    /// because no lane grows into its neighbour. For 12345678: 1234 and 5678 in two lanes of 32 bits, then 12, 34, 56
    /// and 78 in four lanes of 16 bits, then the digits 1 to 8 in eight lanes of 8 bits, which become the characters
    /// "12345678".
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong EightAsciiDigits(uint value)
    {
        // Two numbers below 10^4, in lanes of 32 bits. The upper digits go to the lower lane: 1234 into bits 0 to 31,
        // 5678 into bits 32 to 63.
        ulong high = value / 10_000;
        ulong fours = high | ((value - (high * 10_000)) << 32);

        // Four numbers below 100, in lanes of 16 bits: x / 100 is (x * 10486) >> 20 for x below 10^4, because
        // 10486 / 2^20 is a little more than 1/100. Each 32-bit lane splits into its quotient by 100, which stays in
        // the lower half of the lane, and its remainder, which moves to the upper half: 1234 into 12 and 34, 5678 into
        // 56 and 78. The mask keeps the 7 bits of each quotient and drops the rest of the product.
        ulong hundreds = ((fours * 10486) >> 20) & 0x0000_007F_0000_007F;
        ulong twos = hundreds | ((fours - (hundreds * 100)) << 16);

        // Eight digits, in lanes of 8 bits: x / 10 is (x * 103) >> 10 for x below 100. The same split once more, 12
        // into 1 and 2 and so on, with a mask of the 4 bits of each quotient.
        ulong tens = ((twos * 103) >> 10) & 0x000F_000F_000F_000F;
        ulong ones = tens | ((twos - (tens * 10)) << 8);

        return ToCharacters(ones);
    }

    /// <summary>
    /// Turns digits, one per byte, into their characters: 0x30 is '0', so each digit from 0 to 9 becomes '0' to '9'.
    /// The digits are below 16, so setting the bits of 0x30 adds it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ToCharacters(ulong digits)
    {
        return digits | 0x3030_3030_3030_3030;
    }
}
