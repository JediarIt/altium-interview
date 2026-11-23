using System.Security;

namespace FileSort.Checker.Cli;

/// <summary>
/// The two files of a check, opened for reading only. Other programs may read them at the same time; nothing is
/// created, changed or deleted.
/// </summary>
internal sealed class PairFiles : IDisposable
{
    private PairFiles(string inputPath, FileStream input, long inputSize, string sortedPath, FileStream sorted, long sortedSize)
    {
        InputPath = inputPath;
        Input = input;
        InputSize = inputSize;
        SortedPath = sortedPath;
        Sorted = sorted;
        SortedSize = sortedSize;
    }

    /// <summary>The full path of the input.</summary>
    public string InputPath { get; }

    public FileStream Input { get; }

    public long InputSize { get; }

    /// <summary>The full path of the sorted file.</summary>
    public string SortedPath { get; }

    public FileStream Sorted { get; }

    public long SortedSize { get; }

    public long TotalSize => InputSize + SortedSize;

    /// <exception cref="UnreadableFileException">One of the files cannot be opened.</exception>
    public static PairFiles Open(string inputPath, string sortedPath)
    {
        (FileStream input, long inputSize) = OpenForReading(inputPath);
        try
        {
            (FileStream sorted, long sortedSize) = OpenForReading(sortedPath);
            return new PairFiles(inputPath, input, inputSize, sortedPath, sorted, sortedSize);
        }
        catch
        {
            input.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Input.Dispose();
        Sorted.Dispose();
    }

    // Sequential reads into the checker's own buffer: FileStream keeps no second buffer of its own. Only a regular
    // file will do: the checker needs its size, and reads the start of the input twice to find its line ending, so a
    // device or a pipe, which can be read once from where it is, is refused with its path.
    private static (FileStream Stream, long Size) OpenForReading(string path)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = 0,
                Options = FileOptions.SequentialScan,
            });
            if (!stream.CanSeek)
            {
                throw new NotSupportedException("it is a device or a pipe, not a regular file.");
            }

            return (stream, stream.Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException
            or ArgumentException or NotSupportedException)
        {
            // A missing file, a directory, a device, no permission, or text that is not a path: the message names the
            // cause.
            stream?.Dispose();
            throw new UnreadableFileException(path, e);
        }
    }
}
