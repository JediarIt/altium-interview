using FileSort.Engine.Runs;

namespace FileSort.Tests.Engine.Runs;

/// <summary>
/// A failure to write names the file. A real full disk cannot be made in a test; a range of the file locked by another
/// handle makes a write fail the same way, on Windows, where such locks are enforced.
/// </summary>
public sealed class NamedFileStreamTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void A_failed_write_of_an_array_names_the_file()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Locks on ranges of a file are enforced on Windows only.");
        string path = _directory.File("run");
        using NamedFileStream stream = new(path, FileMode.CreateNew);
        using FileStream locker = LockStart(path);

        IOException e = Assert.Throws<IOException>(() => stream.Write(new byte[16], 0, 16));

        Assert.StartsWith($"Cannot write '{path}'", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_write_of_a_span_names_the_file()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Locks on ranges of a file are enforced on Windows only.");
        string path = _directory.File("run");
        using NamedFileStream stream = new(path, FileMode.CreateNew);
        using FileStream locker = LockStart(path);

        IOException e = Assert.Throws<IOException>(() => stream.Write(new byte[16].AsSpan()));

        Assert.StartsWith($"Cannot write '{path}'", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_write_that_succeeds_writes_its_bytes()
    {
        string path = _directory.File("run");
        using (NamedFileStream stream = new(path, FileMode.CreateNew))
        {
            stream.Write([1, 2], 0, 2);
            stream.Write([3]);
        }

        Assert.Equal([1, 2, 3], File.ReadAllBytes(path));
    }

    // Another handle locks the first kilobyte of the file, which a write at its start then cannot touch.
    private static FileStream LockStart(string path)
    {
        FileStream locker = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (OperatingSystem.IsWindows())
        {
            locker.Lock(0, 1024);
        }

        return locker;
    }
}
