using FileSort.Common;

namespace FileSort.Generator.Cli.Options;

/// <summary>The names of the line endings on the command line.</summary>
internal static class LineEndingNames
{
    public const string Lf = "lf";
    public const string CrLf = "crlf";

    public static LineEnding Parse(string name)
    {
        return name switch
        {
            Lf => LineEnding.Lf,
            CrLf => LineEnding.CrLf,
            _ => throw new ArgumentException($"'{name}' is not a line ending.", nameof(name)),
        };
    }

    public static string Format(LineEnding lineEnding)
    {
        return lineEnding == LineEnding.CrLf ? CrLf : Lf;
    }
}
