namespace FileSort.Generator.Cli.Terminal;

/// <summary>The console of the process.</summary>
internal sealed class SystemConsole : IAppConsole
{
    public TextWriter Out => Console.Out;

    public TextWriter Error => Console.Error;

    public bool IsInputRedirected => Console.IsInputRedirected;

    public bool IsErrorRedirected => Console.IsErrorRedirected;

    public string? ReadLine()
    {
        return Console.ReadLine();
    }
}
