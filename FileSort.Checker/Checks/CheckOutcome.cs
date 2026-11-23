namespace FileSort.Checker.Checks;

/// <summary>How the check of a pair of files ended.</summary>
internal enum CheckOutcome
{
    /// <summary>Both files are valid, the output is in order and has the lines of the input.</summary>
    Correct,

    /// <summary>Both files are valid; the order is violated, or the lines differ, or both.</summary>
    NotCorrect,

    /// <summary>A line of one of the files is not valid, so there is no verdict on the order or the lines.</summary>
    InvalidFile,

    /// <summary>The check could not be done: a file could not be read, or a line is longer than supported.</summary>
    NotChecked,
}
