using FileSort.Cli.Terminal;

namespace FileSort.Tests.Cli.Terminal;

/// <summary>
/// The question before an existing output is replaced, which the generator and the sorter share. The programs'
/// own tests check what follows from each answer.
/// </summary>
public class OverwriteConsentTests
{
    [Theory]
    [InlineData("y")]
    [InlineData("Y")]
    [InlineData("yes")]
    [InlineData(" YES ")]
    public void A_yes_is_consent(string answer)
    {
        FakeConsole console = new(answer);

        Assert.Equal(Consent.Given, OverwriteConsent.Ask(console, "data/out.txt"));
        Assert.Equal("'data/out.txt' already exists. Replace it? [y/N] ", console.ErrorText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("n")]
    [InlineData("no")]
    [InlineData("yess")]
    [InlineData(null)]
    public void Anything_else_declines(string? answer)
    {
        FakeConsole console = answer is null ? new() : new(answer);

        Assert.Equal(Consent.Declined, OverwriteConsent.Ask(console, "data/out.txt"));
        Assert.Equal(1, console.PromptsAnswered);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Without_a_console_nobody_is_asked(bool inputRedirected, bool errorRedirected)
    {
        FakeConsole console = new("y") { IsInputRedirected = inputRedirected, IsErrorRedirected = errorRedirected };

        Assert.Equal(Consent.CannotAsk, OverwriteConsent.Ask(console, "data/out.txt"));
        Assert.Equal(0, console.PromptsAnswered);
        Assert.Empty(console.ErrorText);
    }
}
