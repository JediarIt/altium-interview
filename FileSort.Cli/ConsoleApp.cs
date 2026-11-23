using FileSort.Cli.Terminal;

namespace FileSort.Cli;

/// <summary>
/// A console program as a function: the arguments, the console and the token that Ctrl+C cancels go in, the exit
/// code comes out. Tests call it in-process with a console of their own.
/// </summary>
public delegate int ConsoleApp(string[] args, IAppConsole console, CancellationToken cancellationToken);
