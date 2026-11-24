using FileSort.Cli;
using FileSort.Sorter.Cli;

namespace FileSort.Sorter;

internal static class Program
{
    // Ctrl+C and the termination signals cancel the sort: it stops, deletes its temporary folder and the partly written
    // output, and exits with the code for cancellation instead of being killed in the middle of a write.
    private static int Main(string[] args) => ProgramHost.Run(args, SorterApp.Run);
}
