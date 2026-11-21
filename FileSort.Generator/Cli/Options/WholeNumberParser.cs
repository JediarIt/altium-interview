using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FileSort.Generator.Cli.Options;

/// <summary>Parses a whole number written with digits only: no sign, no spaces, no separators.</summary>
internal static class WholeNumberParser
{
    public static bool TryParse(string text, ulong min, ulong max, out ulong value, [NotNullWhen(false)] out string? error)
    {
        // NumberStyles.None accepts the digits 0 to 9 and nothing else: no sign, no white space, no separators.
        if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value >= min
            && value <= max)
        {
            error = null;
            return true;
        }

        value = 0;
        error = $"'{text}' is not accepted. Expected a whole number from {min} to {max}.";
        return false;
    }
}
