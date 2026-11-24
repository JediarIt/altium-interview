using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;

namespace FileSort.Cli.Options;

/// <summary>
/// Custom parsers for the options of <c>System.CommandLine</c>, built from the value parsers of this library. A
/// value is parsed by a custom parser, not checked by a validator afterwards: a validator that asks for the value of
/// a text that does not parse throws instead of reporting the error.
/// </summary>
public static class CustomParsers
{
    /// <summary>The custom parser of an option whose value the given parser reads.</summary>
    public static Func<ArgumentResult, T?> From<T>(ValueParser<T> parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        return result =>
        {
            if (parser(result.Tokens[0].Value, out T value, out string? error))
            {
                return value;
            }

            result.AddError(error);
            return default;
        };
    }

    /// <summary>The custom parser of an option that takes a whole number from <paramref name="min"/> to
    /// <paramref name="max"/>.</summary>
    public static Func<ArgumentResult, int> WholeNumber(int min, int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(min);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, min);
        return From((string text, out int value, [NotNullWhen(false)] out string? error) =>
        {
            bool parsed = WholeNumberParser.TryParse(text, (ulong)min, (ulong)max, out ulong number, out error);
            value = (int)number;
            return parsed;
        });
    }
}
