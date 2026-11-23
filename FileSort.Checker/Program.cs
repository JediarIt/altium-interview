using FileSort.Checker.Cli;
using FileSort.Cli;

namespace FileSort.Checker;

internal static class Program
{
    // Ctrl+C and the termination signals cancel the check: it stops between two buffers and exits with the code for
    // cancellation. The checker only reads files, so there is nothing to clean up.
    private static int Main(string[] args) => ProgramHost.Run(args, CheckerApp.Run);
}
