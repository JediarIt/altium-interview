namespace FileSort.Generator.Output;

/// <summary>Decides whether the output fits on its volume before anything is deleted or written.</summary>
internal static class OutputSpace
{
    /// <summary>
    /// The space that a run can need: the requested size, one longest line (the output ends on a line boundary at or
    /// after the size), and the padding of the last chunk to whole sectors. Capped at the largest file size instead
    /// of overflowing.
    /// </summary>
    public static long Required(long size, int maxLineLength)
    {
        long padded = long.MaxValue / FileSink.Alignment * FileSink.Alignment;
        if (size > padded - maxLineLength - FileSink.Alignment)
        {
            return long.MaxValue;
        }

        return FileSink.AlignUp(size + maxLineLength);
    }

    /// <summary>
    /// Whether the required space fits. The files that the run is about to replace, the old output and a stale
    /// temporary file, count as available.
    /// </summary>
    public static bool Fits(long required, long free, long replaced)
    {
        return required - replaced <= free;
    }

    /// <summary>
    /// The free space of the volume that holds a path, or null when it cannot be queried (a network path, for
    /// example). Then the preallocation of the temporary file is the only check.
    /// </summary>
    public static long? Available(string fullPath)
    {
        try
        {
            // On Windows a volume is asked by its root. Elsewhere the root is always "/", which says nothing about
            // the file system mounted at the output, so the nearest directory that exists is asked instead.
            string? volume = OperatingSystem.IsWindows() ? Path.GetPathRoot(fullPath) : NearestExistingDirectory(fullPath);
            return string.IsNullOrEmpty(volume) ? null : new DriveInfo(volume).AvailableFreeSpace;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The closest directory above a path that exists; null or empty when there is none.</summary>
    public static string? NearestExistingDirectory(string fullPath)
    {
        string? directory = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory;
    }

    /// <summary>The length of a file, or zero when it does not exist.</summary>
    public static long LengthOrZero(string path)
    {
        FileInfo file = new(path);
        return file.Exists ? file.Length : 0;
    }
}
