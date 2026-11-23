using FileSort.Checker.Lines;

namespace FileSort.Checker.Cli.Options;

/// <summary>The values of <c>--line-ending</c>: a line ending, or its detection from the input.</summary>
internal static class LineEndingNames
{
    /// <summary>Take the line ending from the first line feed of the input.</summary>
    public const string Auto = "auto";

    public const string Lf = "lf";

    public const string CrLf = "crlf";

    /// <summary>The line ending that a value of the option names; null for <see cref="Auto"/>.</summary>
    public static LineEnding? Parse(string name)
    {
        return name switch
        {
            Auto => null,
            Lf => LineEnding.Lf,
            CrLf => LineEnding.CrLf,
            _ => throw new ArgumentException($"'{name}' is not a value of --line-ending.", nameof(name)),
        };
    }

    public static string Format(LineEnding lineEnding)
    {
        return lineEnding == LineEnding.CrLf ? CrLf : Lf;
    }
}
