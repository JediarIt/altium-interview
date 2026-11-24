using System.Diagnostics.CodeAnalysis;

using FileSort.Generator.Content;

namespace FileSort.Generator.Cli.Options;

/// <summary>The names of the edge case categories on the command line.</summary>
internal static class EdgeCaseNames
{
    public const string All = "all";

    private static readonly (string Name, EdgeCases Category)[] Names =
    [
        ("unicode", EdgeCases.Unicode),
        ("separator", EdgeCases.Separator),
        ("case", EdgeCases.Case),
        ("big-numbers", EdgeCases.BigNumbers),
        ("leading-zeros", EdgeCases.LeadingZeros),
        ("empty", EdgeCases.Empty),
        ("carriage-return", EdgeCases.CarriageReturn),
    ];

    /// <summary>Every accepted name, for messages and the help text.</summary>
    public static string Accepted => string.Join(", ", Names.Select(entry => entry.Name)) + ", " + All;

    public static bool TryParse(string text, out EdgeCases categories, [NotNullWhen(false)] out string? error)
    {
        categories = EdgeCases.None;
        foreach (string name in text.Split(','))
        {
            EdgeCases category = name == All
                ? EdgeCases.All
                : Names.FirstOrDefault(entry => entry.Name == name).Category;
            if (category == EdgeCases.None)
            {
                error = $"'{name}' is not an edge case category. Expected names separated by commas: {Accepted}.";
                return false;
            }

            categories |= category;
        }

        error = null;
        return true;
    }

    /// <summary>The list that parses back into the same categories.</summary>
    public static string Format(EdgeCases categories)
    {
        if (categories == EdgeCases.All)
        {
            return All;
        }

        IEnumerable<string> names = Names
            .Where(entry => categories.HasFlag(entry.Category))
            .Select(entry => entry.Name);
        return string.Join(',', names);
    }
}
