using FileSort.Cli.Terminal;

namespace FileSort.Checker.Cli.Reports;

/// <summary>
/// Standard output, for what the checker says about the content of the files: order violations, the report of an
/// invalid line, the summary. A write that fails, for example to a file on a full disk, does not stop the check and
/// does not change its exit code, which still carries the verdict: what was to be printed is lost, and the error
/// stream says so once.
/// </summary>
internal sealed class FindingsOutput(IAppConsole console)
{
    private readonly IAppConsole _console = console;

    // Why the output failed; nothing more is written to it after that.
    private string? _failure;

    public void Write(Action<TextWriter> print)
    {
        if (_failure is not null)
        {
            return;
        }

        try
        {
            print(_console.Out);
            _console.Out.Flush();
        }
        catch (IOException e)
        {
            _failure = e.Message;
        }
    }

    /// <summary>Says on the error stream that findings were lost, if they were and if that stream can be written to.</summary>
    public void ReportLoss()
    {
        if (_failure is not null)
        {
            _console.TryWriteError($"The check was done, but its report could not be printed: {_failure}");
        }
    }
}
