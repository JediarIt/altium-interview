using System.Runtime.InteropServices;

using FileSort.Generator.Cli;
using FileSort.Generator.Cli.Terminal;

namespace FileSort.Generator;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Ctrl+C and the termination signal cancel one token: the run stops, removes its temporary file and exits
        // with the code for cancellation instead of being killed in the middle of a write. The token source lives
        // as long as the process, because a signal can arrive at any moment.
        CancellationTokenSource cancellation = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };
        Action<PosixSignalContext> cancel = context =>
        {
            context.Cancel = true;
            cancellation.Cancel();
        };

        // SIGTERM is a request to terminate; SIGHUP is the terminal going away, which on Windows is the console
        // window being closed.
        using PosixSignalRegistration termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, cancel);
        using PosixSignalRegistration hangup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, cancel);

        try
        {
            return GeneratorApp.Run(args, new SystemConsole(), cancellation.Token);
        }
        catch (Exception e)
        {
            // Anything unexpected still ends with a message and an exit code, never with a stack trace.
            Console.Error.WriteLine($"Unexpected error: {e.Message}");
            return ExitCode.Failure;
        }
    }
}
