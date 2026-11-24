using FileSort.Engine.Runs;

namespace FileSort.Tests.Engine.Runs;

/// <summary>
/// The free space check of D14 with the free space given by the test: the runs and the output on one volume or on
/// two, the files that the sort replaces, the check before a group of runs, and a volume that cannot be asked.
/// </summary>
public class SpaceCheckTests
{
    private const long Input = 1000;

    private static readonly string Here = Path.GetTempPath();

    private static readonly string Output = Path.Combine(Here, "sorted.txt");

    [Fact]
    public void On_one_volume_the_runs_and_the_output_add_up()
    {
        List<string> asked = [];
        SpaceCheck check = new(Here, Output, directory =>
        {
            asked.Add(directory);
            return (2 * Input) - 1;
        });

        NotEnoughSpaceException e = Assert.Throws<NotEnoughSpaceException>(() => check.BeforeReading(Input, 0, 0));

        Assert.Equal(2 * Input, e.Required);
        Assert.Equal((2 * Input) - 1, e.Available);
        string expected = $"{2 * Input} bytes are needed, {(2 * Input) - 1} are free";
        Assert.Contains(expected, e.Message, StringComparison.Ordinal);
        Assert.All(asked, directory => Assert.Equal(Path.GetDirectoryName(Output), directory));
    }

    [Fact]
    public void The_files_that_the_sort_replaces_count_as_free()
    {
        SpaceCheck check = new(Here, Output, _ => Input);

        check.BeforeReading(Input, oldOutputBytes: Input, staleBytes: 0);
    }

    [Fact]
    public void On_one_volume_an_old_output_makes_no_room_for_the_runs()
    {
        // The runs are written while the old output still exists: it is deleted only before the output is written.
        SpaceCheck check = new(Here, Output, _ => 10);

        NotEnoughSpaceException e = Assert.Throws<NotEnoughSpaceException>(
            () => check.BeforeReading(Input, oldOutputBytes: 3 * Input, staleBytes: 0));

        Assert.Equal(Input, e.Required);
        Assert.Equal(10, e.Available);
    }

    [Fact]
    public void A_stale_temporary_file_of_the_output_makes_room_for_the_runs()
    {
        // It is emptied before the first run is written.
        SpaceCheck check = new(Here, Output, _ => Input / 2);

        check.BeforeReading(Input, oldOutputBytes: Input, staleBytes: (Input / 2) + 1);
    }

    [Fact]
    public void On_two_volumes_each_needs_the_size_of_the_input()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Volumes are told apart by the root of a path on Windows only.");

        // The directory of the runs is on another drive: it is never touched, only named.
        string temp = Path.GetPathRoot(Here)!.StartsWith('Z') ? @"Y:\runs" : @"Z:\runs";
        Dictionary<string, long> free = new() { [temp] = Input, [Path.GetDirectoryName(Output)!] = Input - 1 };
        SpaceCheck check = new(temp, Output, directory => free[directory]);

        // The output's volume is one byte short; an old output on it counts there, and not on the volume of the runs.
        NotEnoughSpaceException e = Assert.Throws<NotEnoughSpaceException>(() => check.BeforeReading(Input, 0, 0));
        Assert.Equal(Input, e.Required);
        Assert.Equal(Path.GetDirectoryName(Output), e.Directory);
        check.BeforeReading(Input, oldOutputBytes: 1, staleBytes: 0);

        free[temp] = Input - 1;
        e = Assert.Throws<NotEnoughSpaceException>(() => check.BeforeReading(Input, oldOutputBytes: 1, staleBytes: 0));
        Assert.Equal(temp, e.Directory);
    }

    [Fact]
    public void Before_a_group_only_the_volume_of_the_runs_is_asked()
    {
        List<string> asked = [];
        SpaceCheck check = new(Here, Output, directory =>
        {
            asked.Add(directory);
            return 10;
        });

        check.BeforeGroup(10);
        Assert.Throws<NotEnoughSpaceException>(() => check.BeforeGroup(11));
        Assert.Equal([Here, Here], asked);
    }

    [Fact]
    public void A_volume_that_cannot_be_asked_is_not_checked()
    {
        SpaceCheck check = new(Here, Output, _ => null);

        check.BeforeReading(long.MaxValue / 2, 0, 0);
        check.BeforeGroup(long.MaxValue);
    }

    [Fact]
    public void The_free_space_of_a_path_that_is_no_path_cannot_be_asked()
    {
        Assert.Null(SpaceCheck.FreeSpaceOf(""));
        Assert.NotNull(SpaceCheck.FreeSpaceOf(Here));
    }

    [Fact]
    public void The_constructors_of_the_exception_keep_the_message_and_the_cause()
    {
        IOException cause = new("The disk is full.");

        Assert.NotEmpty(new NotEnoughSpaceException().Message);
        Assert.Equal("Too little.", new NotEnoughSpaceException("Too little.").Message);
        Assert.Same(cause, new NotEnoughSpaceException("Too little.", cause).InnerException);
        Assert.Null(new NotEnoughSpaceException("Too little.").Directory);
    }
}
