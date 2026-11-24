using System.Globalization;
using System.Security;

using Microsoft.Win32.SafeHandles;

namespace FileSort.Engine.Runs;

/// <summary>
/// The runs in files of a temporary folder of the sort's own, and the output written as <c>&lt;output&gt;.tmp</c> next
/// to the output and renamed when it is complete (D14 of the design). The folder is created inside the temporary
/// directory when the first run is written, so an input that fits in one chunk gets none, and it is deleted with
/// everything in it when the storage is disposed, whatever ended the sort. In that directory nothing else is created,
/// changed or deleted. Every failure is an <see cref="IOException"/> that names the file or the directory it concerns.
/// </summary>
internal sealed class FileRunStorage : IRunStorage, IDisposable
{
    private readonly string _outputPath;
    private readonly string _temporaryOutputPath;
    private readonly string _tempDirectory;
    private readonly bool _overwrite;
    private readonly Dictionary<int, SafeFileHandle> _readers = [];
    private string? _folder;
    private int _nextId;
    private bool _committed;

    /// <summary>
    /// Creates the temporary file of the output, empty, so that an output path that cannot be written fails at once
    /// and not after the runs of a large input are made. A stale one from an earlier sort is replaced.
    /// </summary>
    /// <param name="outputPath">The full path of the output.</param>
    /// <param name="tempDirectory">The full path of the directory in which the temporary folder is created.</param>
    /// <param name="overwrite">Whether a file at the output path may be replaced: the user has agreed to it.</param>
    public FileRunStorage(string outputPath, string tempDirectory, bool overwrite)
    {
        _outputPath = outputPath;
        _temporaryOutputPath = TemporaryPath(outputPath);
        _tempDirectory = tempDirectory;
        _overwrite = overwrite;
        Named(_temporaryOutputPath, "create", () => File.Create(_temporaryOutputPath).Dispose());
    }

    /// <summary>The temporary folder, once the first run has created it.</summary>
    public string? Folder => _folder;

    /// <summary>The path that the output is written under until it is complete.</summary>
    public static string TemporaryPath(string outputPath)
    {
        return outputPath + ".tmp";
    }

    public Stream CreateRun(out int id)
    {
        _folder ??= Named(_tempDirectory, "create a folder in", CreateFolder);
        string path = RunPath(_nextId);
        id = _nextId++;
        return Named(path, "create", () => new NamedFileStream(path, FileMode.CreateNew));
    }

    public int Read(int id, long position, Span<byte> destination)
    {
        string path = RunPath(id);
        try
        {
            return ReadAt(Reader(id, path), position, destination);
        }
        catch (Exception e) when (IsFileSystemFailure(e))
        {
            throw new IOException($"Cannot read '{path}': {e.Message}", e);
        }
    }

    public void Delete(int id)
    {
        if (_readers.Remove(id, out SafeFileHandle? handle))
        {
            handle.Dispose();
        }

        string path = RunPath(id);
        Named(path, "delete", () => File.Delete(path));
    }

    /// <summary>
    /// Deletes an old output, which the user has agreed to replace, and opens the temporary file of the output. A file
    /// at the output path that the user has not agreed to replace, one that appeared while the sort ran, stops the
    /// sort here, before anything is written.
    /// </summary>
    public Stream CreateOutput()
    {
        if (File.Exists(_outputPath))
        {
            if (!_overwrite)
            {
                throw new IOException(
                    $"'{_outputPath}' appeared while the sort ran, and replacing it was not allowed.");
            }

            Named(_outputPath, "delete", () => File.Delete(_outputPath));
        }

        return Named(_temporaryOutputPath, "write", () => new NamedFileStream(_temporaryOutputPath, FileMode.Create));
    }

    public void CommitOutput()
    {
        Named(_outputPath, "write", () => File.Move(_temporaryOutputPath, _outputPath));
        _committed = true;
    }

    /// <summary>
    /// Deletes the temporary folder and, unless the output was committed, its temporary file. Both are tried whatever
    /// happens to the other, and the first failure leaves afterwards.
    /// </summary>
    public void Dispose()
    {
        foreach (SafeFileHandle handle in _readers.Values)
        {
            handle.Dispose();
        }

        _readers.Clear();
        IOException? failure = null;
        if (_folder is not null)
        {
            string folder = _folder;
            _folder = null;
            failure = Attempt(folder, "delete", () => Directory.Delete(folder, recursive: true));
        }

        if (!_committed)
        {
            string temporaryOutputPath = _temporaryOutputPath;
            IOException? temporaryFile = Attempt(temporaryOutputPath, "delete", () => File.Delete(temporaryOutputPath));
            failure ??= temporaryFile;
        }

        if (failure is not null)
        {
            throw failure;
        }
    }

    private static bool IsFileSystemFailure(Exception e)
    {
        return e is IOException or UnauthorizedAccessException or SecurityException;
    }

    // Runs the action and returns its failure, named, instead of throwing it.
    private static IOException? Attempt(string path, string verb, Action action)
    {
        try
        {
            Named(path, verb, action);
            return null;
        }
        catch (IOException e)
        {
            return e;
        }
    }

    // Runs the action, and turns its failure into an IOException that names the path, unless it already is one.
    private static void Named(string path, string verb, Action action)
    {
        Named(path, verb, () =>
        {
            action();
            return true;
        });
    }

    private static T Named<T>(string path, string verb, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception e) when (IsFileSystemFailure(e) && !NamesPath(e, path))
        {
            throw new IOException($"Cannot {verb} '{path}': {e.Message}", e);
        }
    }

    // An IOException that already names the path, like those of NamedFileStream, needs no second name.
    private static bool NamesPath(Exception e, string path)
    {
        return e is IOException && e.Message.Contains(path, StringComparison.Ordinal);
    }

    private static int ReadAt(SafeFileHandle handle, long position, Span<byte> destination)
    {
        int total = 0;
        while (total < destination.Length)
        {
            int read = RandomAccess.Read(handle, destination[total..], position + total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    private SafeFileHandle Reader(int id, string path)
    {
        if (!_readers.TryGetValue(id, out SafeFileHandle? handle))
        {
            handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
            _readers[id] = handle;
        }

        return handle;
    }

    // The output's file name, ".sort-" and a random name, so that a folder left by a killed process says what it is.
    private string CreateFolder()
    {
        string name = $"{Path.GetFileName(_outputPath)}.sort-{Path.GetRandomFileName()}";
        return Directory.CreateDirectory(Path.Combine(_tempDirectory, name)).FullName;
    }

    private string RunPath(int id)
    {
        return Path.Combine(_folder ?? _tempDirectory, string.Create(CultureInfo.InvariantCulture, $"run-{id:D6}"));
    }
}
