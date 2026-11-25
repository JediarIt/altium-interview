using FileSort.Engine.Runs;

namespace FileSort.Tests.Engine.Runs;

/// <summary>
/// The runs in files: a temporary folder of the sort's own, created with the first run and deleted with everything in
/// it; the output under a temporary name until it is complete; nothing else in the directory touched.
/// </summary>
public sealed class FileRunStorageTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void The_temporary_file_of_the_output_exists_from_the_start()
    {
        string output = _directory.File("sorted.txt");

        using FileRunStorage storage = new(output, _directory.Path, overwrite: true);

        Assert.Equal(["sorted.txt.tmp"], _directory.FileNames());
        Assert.Null(storage.Folder);
    }

    [Fact]
    public void The_folder_is_created_with_the_first_run_and_deleted_with_its_runs()
    {
        string output = _directory.File("sorted.txt");
        File.WriteAllText(_directory.File("mine.txt"), "user data");
        FileRunStorage storage = new(output, _directory.Path, overwrite: true);

        using (Stream run = storage.CreateRun(out int id))
        {
            run.Write("1. Apple\n"u8);
            Assert.Equal(0, id);
        }

        string folder = Assert.IsType<string>(storage.Folder);
        Assert.StartsWith(_directory.File("sorted.txt.sort-"), folder, StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(folder));
        byte[] read = new byte[16];
        Assert.Equal(9, storage.Read(0, 0, read));
        Assert.Equal("1. Apple\n"u8.ToArray(), read[..9]);

        storage.Dispose();

        Assert.False(Directory.Exists(folder));
        Assert.Equal(["mine.txt"], _directory.FileNames());
        Assert.Equal("user data", File.ReadAllText(_directory.File("mine.txt")));
    }

    [Fact]
    public void A_merged_run_is_deleted_at_once()
    {
        using FileRunStorage storage = new(_directory.File("sorted.txt"), _directory.Path, overwrite: true);
        storage.CreateRun(out int id).Dispose();

        storage.Delete(id);

        Assert.Empty(Directory.GetFiles(storage.Folder!));
    }

    [Fact]
    public void The_output_gets_its_name_only_when_committed()
    {
        string output = _directory.File("sorted.txt");
        File.WriteAllText(output, "old output");
        FileRunStorage storage = new(output, _directory.Path, overwrite: true);

        using (Stream stream = storage.CreateOutput())
        {
            // The old output is gone once the new one is being written; the new one is not there yet.
            Assert.False(File.Exists(output));
            stream.Write("1. Apple\n"u8);
        }

        storage.CommitOutput();
        storage.Dispose();

        Assert.Equal("1. Apple\n", File.ReadAllText(output));
        Assert.Equal(["sorted.txt"], _directory.FileNames());
    }

    [Fact]
    public void A_sort_that_ends_before_the_commit_leaves_no_temporary_file()
    {
        string output = _directory.File("sorted.txt");
        FileRunStorage storage = new(output, _directory.Path, overwrite: true);
        using (Stream stream = storage.CreateOutput())
        {
            stream.Write("1. Apple\n"u8);
        }

        storage.Dispose();

        Assert.Empty(_directory.FileNames());
    }

    [Fact]
    public void A_complete_output_that_cannot_get_its_name_is_left_under_its_temporary_name()
    {
        string output = _directory.File("sorted.txt");
        FileRunStorage storage = new(output, _directory.Path, overwrite: false);
        using (Stream stream = storage.CreateOutput())
        {
            stream.Write("1. Apple\n"u8);
        }

        // A file appears at the output path during the final merge, after the last look at it.
        File.WriteAllText(output, "appeared during the merge");

        IOException e = Assert.Throws<IOException>(storage.CommitOutput);
        storage.Dispose();

        Assert.Contains("sorted.txt.tmp", e.Message, StringComparison.Ordinal);
        Assert.Equal("appeared during the merge", File.ReadAllText(output));
        Assert.Equal("1. Apple\n", File.ReadAllText(_directory.File("sorted.txt.tmp")));
    }

    [Fact]
    public void An_output_that_cannot_be_written_fails_at_once_with_its_path()
    {
        string output = Path.Combine(_directory.Path, "missing", "sorted.txt");

        IOException e = Assert.ThrowsAny<IOException>(
            () => new FileRunStorage(output, _directory.Path, overwrite: true));

        Assert.Contains("missing", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_that_appears_at_the_output_path_is_not_replaced_without_consent()
    {
        string output = _directory.File("sorted.txt");
        FileRunStorage storage = new(output, _directory.Path, overwrite: false);
        File.WriteAllText(output, "appeared while the sort ran");

        IOException e = Assert.Throws<IOException>(storage.CreateOutput);
        storage.Dispose();

        Assert.Contains("replacing it was not allowed", e.Message, StringComparison.Ordinal);
        Assert.Equal("appeared while the sort ran", File.ReadAllText(output));
        Assert.Equal(["sorted.txt"], _directory.FileNames());
    }

    [Fact]
    public void A_folder_that_cannot_be_deleted_leaves_the_temporary_file_of_the_output_deleted_all_the_same()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "An open file cannot be deleted on Windows only.");
        string output = _directory.File("sorted.txt");
        FileRunStorage storage = new(output, _directory.Path, overwrite: false);
        storage.CreateRun(out _).Dispose();
        string folder = storage.Folder!;

        // Another program holds a file in the folder, so the folder cannot be deleted.
        using (new FileStream(Path.Combine(folder, "held"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            IOException e = Assert.ThrowsAny<IOException>(storage.Dispose);

            Assert.Contains(folder, e.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(FileRunStorage.TemporaryPath(output)));
        }

        Directory.Delete(folder, recursive: true);
    }
}
