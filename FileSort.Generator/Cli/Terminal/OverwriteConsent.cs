namespace FileSort.Generator.Cli.Terminal;

/// <summary>Asks before an existing output file is replaced. Only <c>y</c> or <c>yes</c> is consent.</summary>
internal static class OverwriteConsent
{
    public static Consent Ask(IAppConsole console, string outputPath)
    {
        // The prompt goes to the error stream, so it needs that stream and the input to be a console.
        if (console.IsInputRedirected || console.IsErrorRedirected)
        {
            return Consent.CannotAsk;
        }

        console.Error.Write($"'{outputPath}' already exists. Replace it? [y/N] ");
        string? answer = console.ReadLine()?.Trim();

        return answer is not null
            && (answer.Equals("y", StringComparison.OrdinalIgnoreCase) || answer.Equals("yes", StringComparison.OrdinalIgnoreCase))
            ? Consent.Given
            : Consent.Declined;
    }
}
