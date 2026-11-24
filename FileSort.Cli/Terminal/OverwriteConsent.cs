namespace FileSort.Cli.Terminal;

/// <summary>
/// Asks before an existing output file is replaced, the same way in every program that writes one. Only <c>y</c> or
/// <c>yes</c> is consent.
/// </summary>
public static class OverwriteConsent
{
    public static Consent Ask(IAppConsole console, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(console);

        // The prompt goes to the error stream, so it needs that stream and the input to be a console.
        if (console.IsInputRedirected || console.IsErrorRedirected)
        {
            return Consent.CannotAsk;
        }

        console.Error.Write($"'{outputPath}' already exists. Replace it? [y/N] ");
        string? answer = console.ReadLine()?.Trim();

        return answer?.ToUpperInvariant() is "Y" or "YES" ? Consent.Given : Consent.Declined;
    }
}
