using System.Diagnostics;
using System.Globalization;
using System.Security;

using FileSort.Cli;
using FileSort.Cli.Reports;
using FileSort.Cli.Terminal;
using FileSort.Generator.Cli.Options;
using FileSort.Generator.Cli.Reports;
using FileSort.Generator.Content.Blocks;
using FileSort.Generator.Output;
using FileSort.Generator.Pipeline;

namespace FileSort.Generator.Cli;

/// <summary>
/// The generator as a program: parses the command line, guards the output file, runs the pipeline and reports the
/// result. Everything it prints goes through <see cref="IAppConsole"/> and it returns the exit code, so tests run
/// it in-process.
/// </summary>
internal static class GeneratorApp
{
    public static int Run(string[] args, IAppConsole console, CancellationToken cancellationToken)
    {
        GeneratorCommand command = new();
        command.Root.SetAction(result => Generate(command.Bind(result), console, cancellationToken));

        return CommandRunner.Run(command.Root, args, console);
    }

    private static int Generate(GeneratorSettings settings, IAppConsole console, CancellationToken cancellationToken)
    {
        try
        {
            return GenerateFile(settings, console, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            console.TryWriteError("Cancelled. No file was written.");
            return ExitCode.Cancelled;
        }
        catch (ConsoleWriteException e)
        {
            // Before the file is complete: the run stops, and the cause is the console, not the output file.
            return Fail(console, $"Cannot write to the console, so no file was written: {e.Message}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Problems of the file system: the message names the cause, a stack trace would not help.
            return Fail(console, $"Cannot write '{settings.OutputPath}': {e.Message}");
        }
    }

    private static int GenerateFile(
        GeneratorSettings settings, IAppConsole console, CancellationToken cancellationToken)
    {
        string output = FullPath(settings.OutputPath);
        BlockGenerator generator = new(settings.Content);
        long required = OutputSpace.Required(settings.Size, generator.MaxLineLength);
        bool unbuffered = !settings.BufferedIo;

        // The checks run in this order and stop at the first obstacle. Nothing is deleted before all of them pass.
        string? obstacle = DirectoryObstacle(output)
            ?? SpaceObstacle(output, required)
            ?? OverwriteObstacle(settings, console, output, cancellationToken);
        if (obstacle is not null)
        {
            return Fail(console, obstacle);
        }

        DeleteOldOutput(output, unbuffered);

        // Everything the run works with, defaults and the chosen seed included, before the first byte: a run
        // that is cancelled or fails prints no summary.
        WriteSettings(console, settings, output, FileSink.SupportsUnbuffered && unbuffered);

        Stopwatch elapsed = Stopwatch.StartNew();
        using FileSink sink = new(output, required, unbuffered);
        using ProgressLine progress = new(console, settings.Size);
        GenerationResult result = GenerationPipeline.Run(
            generator, settings.Size, settings.Threads, sink, cancellationToken, progress.Report);
        sink.Commit();
        elapsed.Stop();

        // The file is complete and in place: a console that fails now costs the summary, not the file, so the exit
        // code still says that the file was written, and the error stream says that the summary was lost, if it can.
        progress.Clear();
        try
        {
            RunSummary.Write(console.Out, settings, output, result, sink.Unbuffered, elapsed.Elapsed);
        }
        catch (IOException e)
        {
            console.TryWriteError($"The file was written, but the summary could not be printed: {e.Message}");
        }

        return ExitCode.Success;
    }

    // A failure here is the console's, not the output file's: the progress line reports its own failures the same way.
    private static void WriteSettings(IAppConsole console, GeneratorSettings settings, string output, bool unbuffered)
    {
        try
        {
            RunSettings.Write(console.Error, settings, output, unbuffered);
        }
        catch (IOException e)
        {
            throw new ConsoleWriteException(e);
        }
    }

    // Text that is not a path at all is reported like every other problem with the output file.
    private static string FullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException)
        {
            throw new IOException(e.Message, e);
        }
    }

    private static string? DirectoryObstacle(string output) =>
        Directory.Exists(output) || Path.EndsInDirectorySeparator(output)
            ? $"Cannot write '{output}': it is a directory."
            : null;

    // The files that this run replaces count as available: they are gone before the new content is written.
    private static string? SpaceObstacle(string output, long required)
    {
        long replaced = OutputSpace.LengthOrZero(output) + OutputSpace.LengthOrZero(FileSink.TemporaryPath(output));
        if (OutputSpace.Available(output) is not long free || OutputSpace.Fits(required, free, replaced))
        {
            return null;
        }

        long available = free + replaced;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Not enough free space for '{output}': {required} bytes are required, {available} are available.");
    }

    // An existing file is replaced only with consent: --overwrite, or a yes at the prompt.
    private static string? OverwriteObstacle(
        GeneratorSettings settings, IAppConsole console, string output, CancellationToken cancellationToken)
    {
        if (settings.Overwrite || !File.Exists(output))
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

    // The old file is deleted before the new content is written: the volume needs no room for both files. From
    // here on a failed run leaves no file at the output path, so first the temporary file is created: a run that
    // cannot even start must not cost the old file.
    private static void DeleteOldOutput(string output, bool unbuffered)
    {
        if (File.Exists(output))
        {
            FileSink.EnsureWritable(output, unbuffered);
            File.Delete(output);
        }
    }

    private static int Fail(IAppConsole console, string message)
    {
        console.TryWriteError(message);
        return ExitCode.Failure;
    }
}
