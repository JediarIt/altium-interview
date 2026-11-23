namespace FileSort.Checker.Checks.Scans;

/// <summary>How the scan of one file ended.</summary>
internal enum ScanEnd
{
    /// <summary>The file was read to its end, and every line is valid.</summary>
    Completed,

    /// <summary>A line that is not valid ended the scan.</summary>
    InvalidLine,

    /// <summary>The scan could not go on: the file could not be read, or a line is longer than supported.</summary>
    Failed,

    /// <summary>The scan of the other file ended early and stopped this one before the end of its file.</summary>
    Stopped,
}
