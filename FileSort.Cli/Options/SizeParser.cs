using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FileSort.Cli.Options;

/// <summary>
/// Parses a size such as <c>1048576</c>, <c>500MB</c> or <c>1.5GB</c> into bytes. Units are binary in both
/// spellings (<c>KB</c> and <c>KiB</c> are 1024 bytes) and case-insensitive; a fraction is rounded up to a whole
/// byte, so the result is never below what was asked for. The sizes of every program are parsed here, so
/// <c>1.5GB</c> means the same everywhere.
/// </summary>
public static class SizeParser
{
    public const string Expected =
        "a number greater than zero with an optional unit B, KB, MB, GB or TB (binary; also KiB, MiB, GiB, TiB), "
        + "such as 500MB or 1.5GB";

    public static bool TryParse(string text, out long bytes, [NotNullWhen(false)] out string? error)
    {
        bytes = 0;
        error = $"'{text}' is not a size. Expected {Expected}.";

        // The number is digits with at most one period. A comma is rejected: it is a decimal mark in some cultures
        // and a thousands separator in others. An exponent is rejected as well.
        int numberLength = 0;
        while (numberLength < text.Length && (char.IsAsciiDigit(text[numberLength]) || text[numberLength] == '.'))
        {
            numberLength++;
        }

        ReadOnlySpan<char> number = text.AsSpan(0, numberLength);
        if (number.IsEmpty
            || !char.IsAsciiDigit(number[0])
            || !char.IsAsciiDigit(number[^1])
            || number.Count('.') > 1
            || !TryGetMultiplier(text.AsSpan(numberLength), out long multiplier))
        {
            return false;
        }

        // System.Decimal keeps the fraction exact, so the rounding is the same on every platform.
        if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal value))
        {
            return false;
        }

        decimal exact;
        try
        {
            exact = decimal.Ceiling(value * multiplier);
        }
        catch (OverflowException)
        {
            exact = decimal.MaxValue;
        }

        if (exact <= 0)
        {
            error = $"'{text}' is not a size: it must be greater than zero.";
            return false;
        }

        if (exact > long.MaxValue)
        {
            error = $"'{text}' is too large: the largest size is {long.MaxValue} bytes.";
            return false;
        }

        bytes = (long)exact;
        error = null;
        return true;
    }

    private static bool TryGetMultiplier(ReadOnlySpan<char> unit, out long multiplier)
    {
        Span<char> upper = stackalloc char[3];
        if (unit.Length > upper.Length)
        {
            multiplier = 0;
            return false;
        }

        int length = unit.ToUpperInvariant(upper);
        multiplier = upper[..length] switch
        {
            "" or "B" => 1,
            "KB" or "KIB" => 1L << 10,
            "MB" or "MIB" => 1L << 20,
            "GB" or "GIB" => 1L << 30,
            "TB" or "TIB" => 1L << 40,
            _ => 0,
        };
        return multiplier != 0;
    }
}
