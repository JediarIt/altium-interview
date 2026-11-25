using System.Security;

using FileSort.Engine.Chunks.Reading;
using FileSort.Engine.Lines.Parsing;
using FileSort.Engine.Runs;

namespace FileSort.Engine;

/// <summary>
/// Sorts a file into another file, within the memory of its settings, with runs in a temporary folder that it deletes
/// however the sort ends. The output path never holds a partial file: the output is
/// written under a temporary name and renamed when it is complete.
/// </summary>
public sealed class FileSorter
{
    private readonly SortSettings _settings;

    /// <exception cref="ArgumentException">The settings are out of range.</exception>
    public FileSorter(SortSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _ = new SortJob(settings, new SortProgress());
        _settings = settings;
    }

    /// <summary>Counters for a progress display, which another thread may read while the sort runs.</summary>
    public SortProgress Progress { get; } = new();

    /// <summary>Sorts the input into the output.</summary>
    /// <exception cref="InvalidLineException">A line of the input is not valid; no output is written.</exception>
    /// <exception cref="LineTooLongException">A line of the input is too long; no output is written.</exception>
    /// <exception cref="NotEnoughSpaceException">A volume has too little free space for the sort.</exception>
    /// <exception cref="IOException">A file or directory cannot be read or written; the message names it.</exception>
    /// <exception cref="OperationCanceledException">The sort was cancelled; nothing is left behind.</exception>
    public SortResult Sort(CancellationToken cancellationToken)
    {
        string inputPath = FullPath(_settings.InputPath);
        string outputPath = FullPath(_settings.OutputPath);
        string tempDirectory = FullPath(_settings.TempDirectory ?? Path.GetDirectoryName(outputPath) ?? outputPath);
        CheckOutput(inputPath, outputPath);
        CheckTempDirectory(tempDirectory);

        using InputFile input = OpenInput(inputPath);
        SpaceCheck space = new(tempDirectory, outputPath, _settings.FreeSpace);
        long oldOutput = LengthOrZero(outputPath);
        long staleTemporaryFile = LengthOrZero(FileRunStorage.TemporaryPath(outputPath));
        space.BeforeReading(input.Length, oldOutput, staleTemporaryFile);

        FileRunStorage storage = new(outputPath, tempDirectory, _settings.Overwrite);
        SortResult result;
        try
        {
            SortJob job = new(_settings, Progress);
            result = job.Sort(new NamedInput(input, inputPath), storage, space, cancellationToken);
            storage.CommitOutput();
        }
        catch
        {
            CleanUpQuietly(storage);
            throw;
        }

        // The output is in place: a temporary folder that cannot be deleted now, because another program holds it,
        // stays behind, and the sort has succeeded all the same.
        CleanUpQuietly(storage);
        return result;
    }

    // A failure to clean up after a failure must not hide the failure itself.
    private static void CleanUpQuietly(FileRunStorage storage)
    {
        try
        {
            storage.Dispose();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            // The temporary folder or file stays; the failure of the sort is what the user needs to read.
        }
    }

    private void CheckOutput(string inputPath, string outputPath)
    {
        // The host refuses this as an invalid argument; here it only must never get as far as losing the input, whose
        // old self, as the old output, is deleted before the output is written.
        StringComparison names = OperatingSystem.IsLinux()
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        if (string.Equals(inputPath, outputPath, names))
        {
            throw new IOException($"'{outputPath}' is the input itself: a sort cannot write over its input.");
        }

        if (Directory.Exists(outputPath) || Path.EndsInDirectorySeparator(outputPath))
        {
            throw new IOException($"Cannot write '{outputPath}': it is a directory.");
        }

        // Named here, before the temporary directory, which is the same directory by default: the user named the
        // output, not a temporary directory.
        string? directory = Path.GetDirectoryName(outputPath);
        if (directory is not null && !Directory.Exists(directory))
        {
            throw new IOException($"Cannot write '{outputPath}': the directory '{directory}' does not exist.");
        }

        if (!_settings.Overwrite && File.Exists(outputPath))
        {
            throw new IOException($"'{outputPath}' already exists, and replacing it was not allowed.");
        }
    }

    private static void CheckTempDirectory(string tempDirectory)
    {
        if (!Directory.Exists(tempDirectory))
        {
            throw new IOException($"The temporary directory '{tempDirectory}' does not exist.");
        }
    }

    private static InputFile OpenInput(string inputPath)
    {
        try
        {
            return InputFile.Open(inputPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            throw new IOException($"Cannot read '{inputPath}': {e.Message}", e);
        }
    }

    // Text that is not a path at all fails like a path that cannot be used.
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

    private static long LengthOrZero(string path)
    {
        FileInfo file = new(path);
        return file.Exists ? file.Length : 0;
    }

    /// <summary>The input file whose failures to read name it.</summary>
    private sealed class NamedInput(InputFile input, string path) : IInputSource
    {
        private readonly InputFile _input = input;
        private readonly string _path = path;

        public long Length => _input.Length;

        public int Read(long position, Span<byte> destination)
        {
            try
            {
                return _input.Read(position, destination);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"Cannot read '{_path}': {e.Message}", e);
            }
        }
    }
}
