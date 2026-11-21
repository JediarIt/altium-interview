using System.Diagnostics.CodeAnalysis;

using FileSort.Generator.Content;

namespace FileSort.Generator.Cli.Options;

/// <summary>Parses the number of words of a String: <c>4</c>, or a range such as <c>1-5</c>.</summary>
internal static class WordRangeParser
{
    public static bool TryParse(string text, out WordRange range, [NotNullWhen(false)] out string? error)
    {
        int dash = text.IndexOf('-', StringComparison.Ordinal);
        string minText = dash < 0 ? text : text[..dash];
        string maxText = dash < 0 ? text : text[(dash + 1)..];

        if (WholeNumberParser.TryParse(minText, 1, WordRange.Limit, out ulong min, out _)
            && WholeNumberParser.TryParse(maxText, min, WordRange.Limit, out ulong max, out _))
        {
            range = new WordRange((int)min, (int)max);
            error = null;
            return true;
        }

        range = default;
        error = $"'{text}' is not accepted. Expected a number of words such as 4, or a range such as 1-5, "
            + $"from 1 to {WordRange.Limit}.";
        return false;
    }
}
