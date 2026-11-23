using System.Runtime.InteropServices;

namespace FileSort.Cli.Terminal;

/// <summary>
/// Ctrl+C and the termination signals as one cancellation token: the program stops its work, cleans up and exits
/// with the code for cancellation instead of being killed in the middle of it.
/// </summary>
public sealed class CancellationSignals : IDisposable
{
    // Never disposed: it lives as long as the process, because a signal can arrive at any moment, also after the
    // registrations below are gone.
    private readonly CancellationTokenSource _cancellation = new();
    private readonly PosixSignalRegistration _termination;
    private readonly PosixSignalRegistration _hangup;

    public CancellationSignals()
    {
        Console.CancelKeyPress += OnCancelKeyPress;

        // SIGTERM is a request to terminate; SIGHUP is the terminal going away, which on Windows is the console
        // window being closed.
        _termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnSignal);
        _hangup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, OnSignal);
    }

    /// <summary>Cancelled by the first Ctrl+C or termination signal.</summary>
    public CancellationToken Token => _cancellation.Token;

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _termination.Dispose();
        _hangup.Dispose();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        // The process is not killed: the program ends by itself once it sees the token.
        e.Cancel = true;
        _cancellation.Cancel();
    }

    private void OnSignal(PosixSignalContext context)
    {
        context.Cancel = true;
        _cancellation.Cancel();
    }
}
