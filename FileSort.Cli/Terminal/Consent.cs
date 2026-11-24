namespace FileSort.Cli.Terminal;

/// <summary>What the user decided about an existing output file.</summary>
public enum Consent
{
    Given,

    /// <summary>The user answered anything but yes.</summary>
    Declined,

    /// <summary>There is no console to ask: the input or the error stream is redirected.</summary>
    CannotAsk,
}
