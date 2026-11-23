using System.Diagnostics;

using FileSort.Checker.Checks;
using FileSort.Checker.Checks.Order;
using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Cli.Options;
using FileSort.Checker.Cli.Reports;
using FileSort.Cli;
using FileSort.Cli.Reports;
using FileSort.Cli.Terminal;

namespace FileSort.Checker.Cli;

/// <summary>
/// The checker as a program: parses the command line, opens the two files, runs the check and reports what it
/// found. Everything it prints goes through <see cref="IAppConsole"/> and it returns the exit code, so tests run
/// it in-process. What it says about the content of the files goes to standard output; settings, progress and
/// errors go to the error stream.
/// </summary>
internal static class CheckerApp
{
    // How often the thread that owns the console looks at what the scans have found and redraws the progress.
    private static readonly TimeSpan WatchInterval = TimeSpan.FromMilliseconds(100);

    public static int Run(string[] args, IAppConsole console, CancellationToken cancellationToken)
    {
        return Run(args, console, ScanSettings.Default, cancellationToken);
    }

    /// <summary>
    /// Runs the checker with other settings of the scans than the defaults. The number of threads comes from the
    /// command line; the other settings have no option.
    /// </summary>
    public static int Run(string[] args, IAppConsole console, ScanSettings scan, CancellationToken cancellationToken)
    {
        CheckerCommand command = new();
        command.Root.SetAction(result => Check(command.Bind(result), scan, console, cancellationToken));

        return CommandRunner.Run(command.Root, args, console);
    }

    private static int Check(CheckerSettings settings, ScanSettings scan, IAppConsole console, CancellationToken cancellationToken)
    {
        try
        {
            return CheckFiles(settings, scan, console, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            console.TryWriteError("Cancelled. The check was not finished.");
            return ExitCode.Cancelled;
        }
        catch (ConsoleWriteException e)
        {
            // The settings or the progress could not be printed: the console that would show the result is broken.
            return Fail(console, $"Cannot write to the console, so the check was stopped: {e.Message}");
        }
        catch (UnreadableFileException e)
        {
            return Fail(console, e.Message);
        }
    }

    private static int CheckFiles(CheckerSettings settings, ScanSettings scan, IAppConsole console, CancellationToken cancellationToken)
    {
        string inputPath = FullPath(settings.InputPath);
        string sortedPath = FullPath(settings.SortedPath);

        // Before anything is read: a check that is cancelled or fails prints no summary.
        WriteSettings(console, inputPath, sortedPath, settings);
        using PairFiles files = PairFiles.Open(inputPath, sortedPath);

        FindingsOutput findings = new(console);
        Stopwatch elapsed = Stopwatch.StartNew();
        CheckResult result = RunCheck(files, settings, scan, findings, console, cancellationToken);
        elapsed.Stop();

        findings.Write(writer => WriteReport(writer, files, result, elapsed.Elapsed));
        ReportFailure(console, files.InputPath, result.Input);
        ReportFailure(console, files.SortedPath, result.Output);
        findings.ReportLoss();
        return ExitCodeOf(result.Outcome);
    }

    // The scans run on other threads. This thread is the only one that writes to the console: while it waits, it
    // prints the order violations that the scans have found and redraws the progress line.
    private static CheckResult RunCheck(
        PairFiles files,
        CheckerSettings settings,
        ScanSettings scan,
        FindingsOutput findings,
        IAppConsole console,
        CancellationToken cancellationToken)
    {
        ScanFeed feed = new();
        using ProgressLine progress = new(console, files.TotalSize);

        CheckResult result = BackgroundWork.Run(
            token => PairCheck.Run(
                files.Input, files.Sorted, settings.LineEnding, scan with { Threads = settings.Threads }, feed, token),
            () =>
            {
                PrintViolations(feed, findings, progress);
                progress.Report(feed.BytesRead);
            },
            WatchInterval,
            cancellationToken);

        // The violations found since the last look, so that they stand before the report that follows.
        PrintViolations(feed, findings, progress);
        return result;
    }

    private static void PrintViolations(ScanFeed feed, FindingsOutput findings, ProgressLine progress)
    {
        while (feed.TryTakeViolation(out OrderViolation? violation))
        {
            // Both streams may be the same terminal: the progress line makes room for the finding.
            progress.Clear();
            findings.Write(writer => ViolationReport.Write(writer, violation));
        }
    }

    // After an invalid line there is no summary: the report of that line, or of one line of each file with the
    // input's first, and the result. A check that could not be done says nothing here; its failures go to the
    // error stream.
    private static void WriteReport(TextWriter writer, PairFiles files, CheckResult result, TimeSpan elapsed)
    {
        if (result.Outcome is CheckOutcome.Correct or CheckOutcome.NotCorrect)
        {
            CheckSummary.Write(writer, files, result, elapsed);
            return;
        }

        if (result.Input.InvalidLine is not null)
        {
            InvalidLineReport.Write(writer, "input", files.InputPath, result.Input.InvalidLine, result);
        }

        if (result.Output.InvalidLine is not null)
        {
            InvalidLineReport.Write(writer, "output", files.SortedPath, result.Output.InvalidLine, result);
        }

        if (result.Outcome == CheckOutcome.InvalidFile)
        {
            writer.WriteLine("Result:           INVALID FILE");
        }
    }

    // A file that could not be read to its end, or a line longer than the checker reads.
    private static void ReportFailure(IAppConsole console, string path, ScanResult scan)
    {
        if (scan.End == ScanEnd.Failed)
        {
            console.TryWriteError($"Cannot check '{path}': {scan.Failure}");
        }
    }

    private static int ExitCodeOf(CheckOutcome outcome)
    {
        return outcome switch
        {
            CheckOutcome.Correct => ExitCode.Success,
            CheckOutcome.NotCorrect => ExitCode.NotCorrect,
            CheckOutcome.InvalidFile => ExitCode.InvalidFile,
            _ => ExitCode.Failure,
        };
    }

    // A failure here is the console's: the progress line reports its own failures the same way.
    private static void WriteSettings(IAppConsole console, string inputPath, string sortedPath, CheckerSettings settings)
    {
        try
        {
            CheckSettingsReport.Write(console.Error, inputPath, sortedPath, settings.LineEndingName, settings.Threads);
        }
        catch (IOException e)
        {
            throw new ConsoleWriteException(e);
        }
    }

    // Text that is not a path at all is reported like every other file that cannot be read.
    private static string FullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            throw new UnreadableFileException(path, e);
        }
    }

    private static int Fail(IAppConsole console, string message)
    {
        console.TryWriteError(message);
        return ExitCode.Failure;
    }
}
