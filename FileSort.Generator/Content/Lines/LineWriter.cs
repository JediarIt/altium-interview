using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

using FileSort.Common;

namespace FileSort.Generator.Content.Lines;

/// <summary>
/// Writes the parts of a <c>Number. String</c> line into a span as UTF-8 bytes: a Number, the separator, the
/// terminator. Every method returns the number of bytes written and expects a destination that is large enough.
/// It writes any Number, with leading zeros if asked, through the framework's formatter; the Numbers of plain
/// lines, which fit in 32 bits, take the faster <see cref="PlainNumberWriter"/>.
/// </summary>
internal static class LineWriter
{
    private static ReadOnlySpan<ulong> PowersOfTen =>
    [
        1,
        10,
        100,
        1_000,
        10_000,
        100_000,
        1_000_000,
        10_000_000,
        100_000_000,
        1_000_000_000,
        10_000_000_000,
        100_000_000_000,
        1_000_000_000_000,
        10_000_000_000_000,
        100_000_000_000_000,
        1_000_000_000_000_000,
        10_000_000_000_000_000,
        100_000_000_000_000_000,
        1_000_000_000_000_000_000,
        10_000_000_000_000_000_000,
    ];

    /// <summary>
    /// The number of decimal digits of a value written without leading zeros. The framework has no public method
    /// for this, and it is needed before the digits are written: for the shift in <see cref="PlainNumberWriter"/>
    /// and for the limit on leading zeros.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDigits(ulong value)
    {
        // Setting the lowest bit never changes the number of digits and makes zero a one-digit value.
        value |= 1;

        // 1233 / 4096 approximates log10(2): the bit length gives the number of digits, or one less. For 1000, which
        // takes 10 bits, the guess is (10 * 1233) >> 12 = 3, and 1000 >= 10^3 adds one: 4 digits. For 999, also 10
        // bits, the guess is the same 3, and 999 < 10^3 leaves it.
        int guess = ((BitOperations.Log2(value) + 1) * 1233) >> 12;
        return guess + (value >= PowersOfTen[guess] ? 1 : 0);
    }

    /// <summary>Writes a Number in canonical form: decimal digits with no leading zeros.</summary>
    public static int WriteNumber(Span<byte> destination, ulong value)
    {
        return Format(destination, value, default);
    }

    /// <summary>
    /// Writes a Number with zeros in front of its canonical form; with no zeros, the canonical form itself. The value
    /// may be any of 128 bits, so this also writes the Numbers beyond 64 bits of the category big-numbers.
    /// </summary>
    public static int WriteNumber(Span<byte> destination, UInt128 value, int leadingZeros)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(leadingZeros);
        destination[..leadingZeros].Fill((byte)'0');
        Span<byte> digits = destination[leadingZeros..];

        // A value of 64 bits takes the formatter of 64-bit values, the faster one. A larger value is written by
        // UInt128 itself, which writes UTF-8 like the other integer types; the invariant culture writes plain digits
        // on every machine.
        if (value <= ulong.MaxValue)
        {
            return leadingZeros + Format(digits, (ulong)value, default);
        }

        return value.TryFormat(digits, out int written, default, CultureInfo.InvariantCulture)
            ? leadingZeros + written
            : throw new ArgumentException("The destination is too small for the Number.", nameof(destination));
    }

    /// <summary>Writes the separator between the Number and the String.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteSeparator(Span<byte> destination)
    {
        LineFormat.Separator.CopyTo(destination);
        return LineFormat.SeparatorLength;
    }

    /// <summary>Writes the terminator that follows a line.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTerminator(Span<byte> destination, LineEnding lineEnding)
    {
        // Two fixed-size stores instead of a copy whose length depends on the line ending: this runs on every line.
        if (lineEnding == LineEnding.CrLf)
        {
            destination[1] = LineFormat.LineFeed;
            destination[0] = LineFormat.CarriageReturn;
            return 2;
        }

        destination[0] = LineFormat.LineFeed;
        return 1;
    }

    // Utf8Formatter is culture-invariant by definition, so the digits are the same on every machine.
    private static int Format(Span<byte> destination, ulong value, StandardFormat format)
    {
        return Utf8Formatter.TryFormat(value, destination, out int written, format)
            ? written
            : throw new ArgumentException("The destination is too small for the Number.", nameof(destination));
    }
}
