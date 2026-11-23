using FileSort.Cli;
using FileSort.Generator.Cli;

namespace FileSort.Generator;

internal static class Program
{
    // Ctrl+C and the termination signals cancel the run: it stops, removes its temporary file and exits with the
    // code for cancellation instead of being killed in the middle of a write.
    private static int Main(string[] args) => ProgramHost.Run(args, GeneratorApp.Run);
}
