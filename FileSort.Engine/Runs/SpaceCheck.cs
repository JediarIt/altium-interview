namespace FileSort.Engine.Runs;

/// <summary>
/// Checks that the volumes of the temporary folder and of the output have the free space that the sort needs: before
/// the input is read, room for runs of about the input's size and for the output of the same size, the sum of the two
/// on one volume; and before every merge of a group of runs, room for the run it writes, which the first check could
/// not foresee, since the number of passes depends on the length of the lines. A volume whose free space cannot be
/// asked is not checked.
/// </summary>
/// <param name="tempDirectory">The directory in which the temporary folder is created.</param>
/// <param name="outputPath">The full path of the output.</param>
/// <param name="freeSpace">The free space of the volume of a directory, or null when it cannot be asked.</param>
internal sealed class SpaceCheck(string tempDirectory, string outputPath, Func<string, long?> freeSpace)
{
    private readonly string _tempDirectory = tempDirectory;
    private readonly string _outputDirectory = Path.GetDirectoryName(outputPath) ?? outputPath;
    private readonly Func<string, long?> _freeSpace = freeSpace;
    private readonly bool _oneVolume = SameVolume(tempDirectory, Path.GetDirectoryName(outputPath) ?? outputPath);

    /// <summary>
    /// The free space of the volume of a directory as the operating system reports it, or null when it cannot be
    /// asked (some network paths). On Windows a volume is asked by its root; elsewhere the root says nothing about the
    /// file system mounted at the directory, so the directory itself is asked.
    /// </summary>
    public static long? FreeSpaceOf(string directory)
    {
        try
        {
            string? volume = OperatingSystem.IsWindows() ? Path.GetPathRoot(Path.GetFullPath(directory)) : directory;
            return string.IsNullOrEmpty(volume) ? null : new DriveInfo(volume).AvailableFreeSpace;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Before the input is read. The files that the sort replaces count as free only once they are gone: a stale
    /// temporary file of the output from the start, since it is emptied before any run is written, and an old output
    /// only for the output, since it is deleted right before the output is written, when the runs already exist. On
    /// one volume the runs must therefore fit next to the old output, and the runs and the output together where the
    /// old output was.
    /// </summary>
    /// <exception cref="NotEnoughSpaceException">A volume has less free space than the sort needs.</exception>
    public void BeforeReading(long inputBytes, long oldOutputBytes, long staleBytes)
    {
        if (_oneVolume)
        {
            Require(_outputDirectory, inputBytes, staleBytes);
            Require(_outputDirectory, 2 * inputBytes, oldOutputBytes + staleBytes);
            return;
        }

        Require(_tempDirectory, inputBytes, 0);
        Require(_outputDirectory, inputBytes, oldOutputBytes + staleBytes);
    }

    /// <summary>
    /// Before the merge of a group of runs: room for the run that the group writes. The runs of the group are deleted
    /// once it is merged, which gives the room back before the output is written.
    /// </summary>
    /// <exception cref="NotEnoughSpaceException">The volume of the runs has less free space than the run
    /// needs.</exception>
    public void BeforeGroup(long runBytes)
    {
        Require(_tempDirectory, runBytes, 0);
    }

    private void Require(string directory, long required, long replaced)
    {
        if (_freeSpace(directory) is long free && required > free + replaced)
        {
            throw new NotEnoughSpaceException(directory, required, free + replaced);
        }
    }

    // On Windows two directories share a volume when they share a root, which is the case that matters on the machine
    // the sorter is measured on; elsewhere they are taken to be on different volumes, so each one is checked alone.
    private static bool SameVolume(string first, string second)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        string? firstRoot = Path.GetPathRoot(Path.GetFullPath(first));
        string? secondRoot = Path.GetPathRoot(Path.GetFullPath(second));
        return string.Equals(firstRoot, secondRoot, StringComparison.OrdinalIgnoreCase);
    }
}
