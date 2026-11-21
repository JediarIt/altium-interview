using System.Runtime.CompilerServices;

namespace FileSort.Generator.Random;

/// <summary>
/// Reads choices out of a 64-bit random value. One value pays for several choices, each from its own group of bits;
/// bits are numbered from 0, the lowest, to 63, the highest. The bytes of a generated file depend on these exact
/// expressions, so they are part of the content version.
/// </summary>
/// <remarks>
/// Every method is a few instructions and asks to be inlined: a call costs the same as the expression it names, and
/// with constant arguments the masks are computed by the compiler. The machine code of the hot path, printed before
/// and after these methods replaced the bare expressions, has the same size and the same calls (see the README of
/// this project, Design decisions, "Bit operations sit behind named methods").
/// </remarks>
internal static class RandomBits
{
    /// <summary>
    /// The <paramref name="count"/> bits from bit <paramref name="first"/> upward, as a number from 0 to
    /// 2^count - 1. <paramref name="count"/> is 1 to 31.
    /// </summary>
    /// <remarks>
    /// It works like taking digits out of a decimal number. From 987654321, the three digits that start at the third
    /// from the right are 543: drop the two lowest digits (987654321 / 100 = 9876543), then keep the three lowest of
    /// what is left (9876543 % 1000 = 543). Here the digits are bits: <c>Bits(0b11010110, 2, 3)</c> drops the two
    /// lowest bits, which leaves 0b110101, and keeps the three lowest of those, 0b101 = 5.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Bits(ulong value, int first, int count)
    {
        return (int)Field(value, first, count);
    }

    /// <summary>Whether bit <paramref name="index"/> is set: true with a probability of 1/2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSet(ulong value, int index)
    {
        return Field(value, index, 1) != 0;
    }

    /// <summary>
    /// One of <paramref name="choices"/> outcomes, 0 to choices - 1, each about equally likely, from the
    /// <paramref name="count"/> bits from bit <paramref name="first"/> upward.
    /// </summary>
    /// <remarks>
    /// The bits are read as a fraction, r / 2^count, from 0 up to but not including 1. Multiplied by the number of
    /// choices it is below that number, and dropping what follows the point leaves a whole number from 0 to
    /// choices - 1. No division is needed, and no outcome is more likely than another by more than
    /// choices / 2^count. With 8 bits and 3 choices: r from 0 to 85 gives 0, from 86 to 170 gives 1, from 171 to 255
    /// gives 2.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Pick(ulong value, int first, int count, int choices)
    {
        return (int)((Field(value, first, count) * (ulong)choices) >> count);
    }

    /// <summary>
    /// The highest <paramref name="count"/> bits, 1 to 64, as a number from 0 to 2^count - 1:
    /// <c>Highest(value, 4)</c> is bits 60 to 63, a number from 0 to 15.
    /// </summary>
    /// <remarks>
    /// Like taking the first digits of a decimal number: the first three digits of 987654321 are
    /// 987654321 / 10^6 = 987, the six lowest digits dropped. Here the shift right by 64 - count drops all bits but
    /// the highest count.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Highest(ulong value, int count)
    {
        return value >> (64 - count);
    }

    /// <summary>
    /// A number of exactly <paramref name="bitLength"/> bits, 1 to 64: the highest bits of the value, with the top
    /// one of them set. It lies from 2^(bitLength - 1) to 2^bitLength - 1; with 33 bits, from 4294967296 to
    /// 8589934591.
    /// </summary>
    /// <remarks>
    /// Like a random number of exactly three decimal digits, 100 to 999: three random digits are not enough, because
    /// "042" is the two-digit 42, so the first digit must not be 0. A binary digit is 0 or 1, so the first bit must
    /// be 1. With 4 bits: <see cref="Highest"/> gives a number from 0 to 15, say 0b0101 = 5, which has only 3 bits;
    /// <c>1 &lt;&lt; 3</c> is 0b1000 = 8, a number with only bit 3 set, and "|" sets that bit in the result:
    /// 0b0101 | 0b1000 = 0b1101 = 13. Every result is from 0b1000 = 8 to 0b1111 = 15. The big-Numbers edge case
    /// takes 33 bits or more this way, so its Numbers are 2^32 = 4294967296 or more and never fit in 32 bits.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong WithBitLength(ulong value, int bitLength)
    {
        return Highest(value, bitLength) | (1UL << (bitLength - 1));
    }

    // See the example on Bits. "value >> first" drops the lowest first bits, as dividing by 100 drops two decimal
    // digits. "& mask" keeps the lowest count bits, as the remainder of dividing by 1000 keeps three decimal digits.
    // The mask (1 << count) - 1 is to binary what 10^3 - 1 = 999 is to decimal, count ones in a row: 1 << 3 is
    // 0b1000 = 8, and 8 - 1 is 0b111. A bit of the mask that is 1 keeps the bit of the value, a 0 clears it.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Field(ulong value, int first, int count)
    {
        return (value >> first) & ((1UL << count) - 1);
    }
}
