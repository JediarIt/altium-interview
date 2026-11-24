using System.Text;

using FileSort.Engine;
using FileSort.Engine.Lines.Parsing;
using FileSort.Engine.Runs;
using FileSort.Tests.Format;
using FileSort.Tests.Generator;

namespace FileSort.Tests.Engine;

/// <summary>
/// The sort of files, with the engine's own file storage: the scenarios of "Temporary files", "Free space" and
/// "Output file" of <c>external-sorter</c> as far as the engine decides them, and the failures that name a path. The
/// files are kilobytes.
/// </summary>
public sealed class FileSorterTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly string _input;
    private readonly string _output;
    private readonly byte[] _content;

    public FileSorterTests()
    {
        _input = _directory.File("input.txt");
        _output = _directory.File("sorted.txt");
        _content = TestContent.Generate(TestContent.Options(seed: 3), 64 * 1024);
        File.WriteAllBytes(_input, _content);
    }

    public void Dispose()
    {
        _directory.Dispose();
    }

    // Tiny memory: chunks of under a kilobyte and room for three runs in a merge, so that the input makes dozens of
    // runs and the merge has groups.
    private SortSettings Settings(Func<string, long?>? freeSpace = null, string? temp = null, bool overwrite = false)
    {
        return new SortSettings
        {
            InputPath = _input,
            OutputPath = _output,
            TempDirectory = temp,
            MemoryBytes = 1600 + 4096,
            WindowBytes = 4096,
            Threads = 3,
            MaxLineLength = 200,
            Overwrite = overwrite,
            FreeSpace = freeSpace ?? (_ => null),
        };
    }

    private static SortResult Sort(SortSettings settings)
    {
        return new FileSorter(settings).Sort(TestContext.Current.CancellationToken);
    }

    // Requirement: Sorted output, through files

    [Fact]
    public void A_file_sorts_into_its_naive_sort()
    {
        SortResult result = Sort(Settings());

        Assert.Equal(NaiveSort.SortFile(_content, "\n"), File.ReadAllBytes(_output));
        Assert.True(result.Runs > 2);
        Assert.True(result.MergePasses > 1);
    }

    // Requirement: Temporary files

    [Fact]
    public void Default_place()
    {
        // The free space is asked again before every group of runs, while the runs exist.
        List<string[]> foldersSeen = [];
        Sort(Settings(freeSpace: directory =>
        {
            foldersSeen.Add([.. Directory.GetDirectories(_directory.Path).Select(Path.GetFileName)!]);
            return null;
        }));

        Assert.Contains(
            foldersSeen,
            folders => folders.Length == 1 && folders[0]!.StartsWith("sorted.txt.sort-", StringComparison.Ordinal));
    }

    [Fact]
    public void Nothing_left_behind_in_another_directory()
    {
        using TempDirectory temp = new();
        File.WriteAllText(temp.File("mine.txt"), "user data");

        Sort(Settings(temp: temp.Path));
        Assert.Equal(["mine.txt"], temp.FileNames());
        Assert.Empty(Directory.GetDirectories(temp.Path));

        File.WriteAllBytes(_input, [.. _content, .. "Not a line\n"u8]);
        Assert.Throws<InvalidLineException>(() => Sort(Settings(temp: temp.Path, overwrite: true)));
        Assert.Equal(["mine.txt"], temp.FileNames());
        Assert.Empty(Directory.GetDirectories(temp.Path));
        Assert.Equal("user data", File.ReadAllText(temp.File("mine.txt")));
    }

    [Fact]
    public void Nothing_left_behind_after_a_cancellation()
    {
        using CancellationTokenSource cancellation = new();
        int asked = 0;
        SortSettings settings = Settings(freeSpace: _ =>
        {
            // The second question comes before the first group of runs: the runs exist by then.
            if (++asked == 2)
            {
                cancellation.Cancel();
            }

            return null;
        });

        Assert.ThrowsAny<OperationCanceledException>(() => new FileSorter(settings).Sort(cancellation.Token));
        Assert.Equal(["input.txt"], _directory.FileNames());
        Assert.Empty(Directory.GetDirectories(_directory.Path));
    }

    // Requirement: Free space

    [Fact]
    public void Not_enough_disk_space()
    {
        NotEnoughSpaceException e = Assert.Throws<NotEnoughSpaceException>(() => Sort(Settings(freeSpace: _ => 1000)));

        // The runs alone do not fit: they are the first need of the sort.
        Assert.Equal(1000, e.Available);
        Assert.Equal(["input.txt"], _directory.FileNames());
    }

    [Fact]
    public void An_old_output_makes_no_room_for_the_runs()
    {
        // The runs are written while the old output exists; it is deleted only right before the output is written.
        File.WriteAllBytes(_output, new byte[3 * _content.Length]);

        NotEnoughSpaceException e = Assert.Throws<NotEnoughSpaceException>(
            () => Sort(Settings(freeSpace: _ => 10, overwrite: true)));

        Assert.Equal(_content.Length, e.Required);
        Assert.Equal(3 * _content.Length, new FileInfo(_output).Length);
        Assert.Equal(["input.txt", "sorted.txt"], _directory.FileNames());
    }

    [Fact]
    public void An_output_that_is_replaced_counts_as_free_space()
    {
        File.WriteAllBytes(_output, new byte[_content.Length]);

        Sort(Settings(freeSpace: _ => _content.Length + 10, overwrite: true));

        Assert.Equal(NaiveSort.SortFile(_content, "\n"), File.ReadAllBytes(_output));
    }

    // Requirement: Output file

    [Fact]
    public void An_existing_output_is_not_replaced_without_consent()
    {
        File.WriteAllText(_output, "old output");

        Assert.ThrowsAny<IOException>(() => Sort(Settings()));
        Assert.Equal("old output", File.ReadAllText(_output));
    }

    [Fact]
    public void An_invalid_input_leaves_an_old_output_untouched()
    {
        File.WriteAllText(_output, "old output");
        File.WriteAllBytes(_input, [.. _content, .. "Not a line\n"u8]);

        Assert.Throws<InvalidLineException>(() => Sort(Settings(overwrite: true)));
        Assert.Equal("old output", File.ReadAllText(_output));
        Assert.Equal(["input.txt", "sorted.txt"], _directory.FileNames());
    }

    [Fact]
    public void Successful_sort_with_overwrite()
    {
        File.WriteAllText(_output, "old output");

        Sort(Settings(overwrite: true));

        Assert.Equal(NaiveSort.SortFile(_content, "\n"), File.ReadAllBytes(_output));
        Assert.Equal(["input.txt", "sorted.txt"], _directory.FileNames());
    }

    // Requirement: Exit codes and error messages, the engine's part: every failure names its path

    [Fact]
    public void Missing_input_file()
    {
        File.Delete(_input);

        IOException e = Assert.ThrowsAny<IOException>(() => Sort(Settings()));

        Assert.Contains(_input, e.Message, StringComparison.Ordinal);
        Assert.Empty(_directory.FileNames());
    }

    [Fact]
    public void A_path_that_is_not_a_path()
    {
        IOException e = Assert.ThrowsAny<IOException>(() => Sort(Settings() with { InputPath = "input\0.txt" }));

        Assert.Contains("is not a valid path", e.Message, StringComparison.Ordinal);
        Assert.Equal(["input.txt"], _directory.FileNames());
    }

    [Fact]
    public void An_input_that_cannot_be_read_is_named_and_leaves_nothing_behind()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Locks on ranges of a file are enforced on Windows only.");

        // Another handle locks the start of the input, so the first read of the sort fails as a bad sector would.
        using (FileStream locker = new(_input, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (OperatingSystem.IsWindows())
            {
                locker.Lock(0, _content.Length);
            }


            IOException e = Assert.ThrowsAny<IOException>(() => Sort(Settings()));

            Assert.StartsWith($"Cannot read '{_input}'", e.Message, StringComparison.Ordinal);
        }

        Assert.Equal(["input.txt"], _directory.FileNames());
        Assert.Empty(Directory.GetDirectories(_directory.Path));
    }

    [Fact]
    public void Output_path_that_is_a_directory()
    {
        Directory.CreateDirectory(_output);

        IOException e = Assert.ThrowsAny<IOException>(() => Sort(Settings()));

        Assert.Contains(_output, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Output_in_a_directory_that_does_not_exist()
    {
        string output = _directory.File(Path.Combine("no such directory", "sorted.txt"));
        SortSettings settings = Settings() with { OutputPath = output };

        IOException e = Assert.ThrowsAny<IOException>(() => Sort(settings));

        // The message names the output, which the user gave, and not the temporary directory, which is the same
        // directory by default.
        Assert.StartsWith($"Cannot write '{output}'", e.Message, StringComparison.Ordinal);
        Assert.Equal(["input.txt"], _directory.FileNames());
    }

    [Fact]
    public void Temporary_directory_that_does_not_exist()
    {
        string missing = _directory.File("no such directory");

        IOException e = Assert.ThrowsAny<IOException>(() => Sort(Settings(temp: missing)));

        Assert.Contains(missing, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_input_itself_as_the_output()
    {
        SortSettings settings = Settings(overwrite: true) with { OutputPath = _input };

        Assert.ThrowsAny<IOException>(() => Sort(settings));
        Assert.Equal(_content, File.ReadAllBytes(_input));
    }

    [Fact]
    public void An_input_that_is_a_device_is_refused()
    {
        Assert.SkipUnless(
            OperatingSystem.IsWindows(), "Windows names a device NUL; elsewhere /dev/null seeks like a file.");

        IOException e = Assert.ThrowsAny<IOException>(() => Sort(Settings() with { InputPath = "NUL" }));

        Assert.Contains("device", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_line_feed_of_another_line_ending_names_the_line()
    {
        File.WriteAllText(_input, "1. Apple\r\n2. Banana\n3. Cherry\r\n", Encoding.ASCII);

        InvalidLineException e = Assert.Throws<InvalidLineException>(() => Sort(Settings()));

        Assert.Equal(2, e.LineNumber);
        Assert.Equal(LineFault.BareLineFeed, e.Fault);
    }
}
