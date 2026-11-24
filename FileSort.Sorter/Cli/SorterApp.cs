using System.Diagnostics;
using System.Security;

using FileSort.Cli;
using FileSort.Cli.Terminal;
using FileSort.Engine;
using FileSort.Engine.Lines.Parsing;
using FileSort.Sorter.Cli.Options;
using FileSort.Sorter.Cli.Reports;
using FileSort.Sorter.Cli.Terminal;

namespace FileSort.Sorter.Cli;

/// <summary>
/// The sorter as a program: parses the command line, asks before an existing output is replaced, runs the engine on
/// another thread while this thread shows the progress, and reports the result. Everything it prints goes through
/// <see cref="IAppConsole"/> and it returns the exit code, so tests run it in-process.
/// </summary>
internal static class SorterApp
{
    /// <summary>
    /// The memory of the process that the engine leaves to everything it does not allocate itself: the runtime and its
    /// compiled code, the stacks of the threads, the console, small objects. <c>--memory</c> less this is what the
    /// engine's buffers get.
    /// </summary>
    public const long Reserve = 128L * 1024 * 1024;

    public static int Run(string[] args, IAppConsole console, CancellationToken cancellationToken)
    {
        return Run(args, console, engine => engine, cancellationToken);
    }

    /// <summary>
    /// Runs the sorter with the settings of the engine passed through <paramref name="tune"/> right before the sort:
    /// tests give the engine a free space of their own or a shorter line limit. The program passes them unchanged.
    /// </summary>
    public static int Run(
        string[] args, IAppConsole console, Func<SortSettings, SortSettings> tune, CancellationToken cancellationToken)
    {
        SorterCommand command = new();
        command.Root.SetAction(result => Sort(command.Bind(result), console, tune, cancellationToken));

        return CommandRunner.Run(command.Root, args, console);
    }

    private static int Sort(
        SorterSettings settings,
        IAppConsole console,
        Func<SortSettings, SortSettings> tune,
        CancellationToken cancellationToken)
    {
        bool lineEndingDetected = settings.LineEndingName == LineEndingNames.Auto;
        try
        {
            return SortFile(settings, console, tune, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            console.TryWriteError("Cancelled. No output was written.");
            return ExitCode.Cancelled;
        }
        catch (ConsoleWriteException e)
        {
            // Before the output is complete: the sort stops, and the cause is the console, not a file.
            return Fail(console, $"Cannot write to the console, so the sort was stopped: {e.Message}");
        }
        catch (InvalidLineException e)
        {
            // The input was opened, so its path is a valid one here; the same holds for a line that is too long.
            string input = Path.GetFullPath(settings.InputPath);
            console.TryWriteError(InputProblemReport.InvalidLine(input, e, lineEndingDetected));
            return ExitCode.InvalidFile;
        }
        catch (LineTooLongException e)
        {
            string input = Path.GetFullPath(settings.InputPath);
            return Fail(console, InputProblemReport.LineTooLong(input, e, lineEndingDetected));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Problems of the file system and of free space: the message names the path, a stack trace would not help.
            return Fail(console, e.Message);
        }
    }

    private static int SortFile(
        SorterSettings settings,
        IAppConsole console,
        Func<SortSettings, SortSettings> tune,
        CancellationToken cancellationToken)
    {
        string input = FullPath(settings.InputPath);
        string output = FullPath(settings.OutputPath);
        string temp = settings.TempDirectory is null ? DirectoryOf(output) : FullPath(settings.TempDirectory);

        // Before anything is read: an existing output is replaced only with consent, --overwrite or a yes. The engine
        // may replace a file at the output path only then, and refuses one that appears while the sort runs.
        bool outputExists = File.Exists(output);
        string? obstacle = OverwriteObstacle(settings, console, output, outputExists, cancellationToken);
        if (obstacle is not null)
        {
            return Fail(console, obstacle);
        }

        WriteSettings(console, settings, input, output, temp);
        FileSorter sorter = new(tune(new SortSettings
        {
            InputPath = input,
            OutputPath = output,
            TempDirectory = temp,
            MemoryBytes = settings.Memory - Reserve,
            Threads = settings.Threads,
            LineEnding = LineEndingNames.Parse(settings.LineEndingName),
            Overwrite = settings.Overwrite || outputExists,
        }));

        Stopwatch elapsed = Stopwatch.StartNew();
        IProgressView view = ProgressViews.For(console);
        SortResult result = BackgroundSort.Run(
            sorter.Sort, sort => view.Follow(sorter.Progress, sort), cancellationToken);
        elapsed.Stop();

        // The output is in place: a console that fails now costs the summary, not the output, so the exit code still
        // says that the output was written, and the error stream says that the summary was lost, if it can.
        try
        {
            SortSummary.Write(console.Out, output, sorter.Progress.InputBytes, result, elapsed.Elapsed);
        }
        catch (IOException e)
        {
            console.TryWriteError($"The output was written, but the summary could not be printed: {e.Message}");
        }

        return ExitCode.Success;
    }


    // An existing output is replaced only with consent: --overwrite, or a yes at the prompt.
    private static string? OverwriteObstacle(
        SorterSettings settings,
        IAppConsole console,
        string output,
        bool outputExists,
        CancellationToken cancellationToken)
    {
        if (settings.Overwrite || !outputExists)
        {
            return null;
        }

        Consent consent = OverwriteConsent.Ask(console, output);
        cancellationToken.ThrowIfCancellationRequested();
        return consent switch
        {
            Consent.Given => null,
            Consent.CannotAsk => $"'{output}' already exists. Use --overwrite to replace it.",
            _ => $"'{output}' was left untouched.",
        };
    }

    // A failure here is the console's, not a file's.
    private static void WriteSettings(
        IAppConsole console, SorterSettings settings, string input, string output, string temp)
    {
        try
        {
            SortSettingsReport.Write(console.Error, settings, input, output, temp);
        }
        catch (IOException e)
        {
            throw new ConsoleWriteException(e);
        }
    }

    // Text that is not a path at all is reported like every other path that cannot be used.
    private static string FullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new IOException($"'{path}' is not a valid path: {e.Message}", e);
        }
    }

    // The directory of a full path; a root is its own directory.
    private static string DirectoryOf(string path)
    {
        return Path.GetDirectoryName(path) ?? path;
    }

    private static int Fail(IAppConsole console, string message)
    {
        console.TryWriteError(message);
        return ExitCode.Failure;
    }
}
