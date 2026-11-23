namespace FileSort.Checker.Checks.Chunks;

/// <summary>How the check of one chunk ended.</summary>
internal enum ChunkEnd
{
    /// <summary>Every line of the chunk was read and is valid.</summary>
    Completed,

    /// <summary>A line that is not valid ended the check of the chunk.</summary>
    InvalidLine,

    /// <summary>A line longer than the longest line that is read ended the check of the chunk.</summary>
    LineTooLong,
}
