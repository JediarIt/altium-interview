using System.CommandLine;
using System.CommandLine.Parsing;

using FileSort.Cli.Terminal;

namespace FileSort.Cli;

/// <summary>
/// Runs a command line against a command of <c>System.CommandLine</c>, the same way in every program: errors of the
/// command line go to the error stream, each naming its option, with the exit code for invalid arguments; the help
/// and the version go to standard output.
/// </summary>
public static class CommandRunner
{
    /// <summary>Returns the exit code: of the action of the command, 0 for help or version, 2 for a parse
    /// error.</summary>
    public static int Run(RootCommand root, string[] args, IAppConsole console)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(console);

        ParseResult parsed = root.Parse(args);
        if (parsed.Errors.Count > 0)
        {
            foreach (string message in parsed.Errors.Select(Describe).Distinct())
            {
                console.Error.WriteLine(message);
            }

            console.Error.WriteLine("Run with --help to see the options.");
            return ExitCode.InvalidArguments;
        }

        // Runs the action of the command, or prints the help or the version when that was asked for.
        return parsed.Invoke(new InvocationConfiguration
        {
            Output = console.Out,
            Error = console.Error,
            EnableDefaultExceptionHandler = false,
        });
    }

    // Every error about the command line names its option. The library's messages about a value do not always do
    // that ("Argument 'cr' not recognized"), so the name is added here, in one place, for every option.
    private static string Describe(ParseError error)
    {
        SymbolResult? result = error.SymbolResult;
        while (result is not null and not OptionResult)
        {
            result = result.Parent;
        }

        return result is OptionResult option && !error.Message.Contains(option.Option.Name, StringComparison.Ordinal)
            ? $"Option '{option.Option.Name}': {error.Message}"
            : error.Message;
    }
}
