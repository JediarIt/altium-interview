namespace FileSort.Checker.Cli;

/// <summary>One of the two files cannot be opened for reading. The message names the path and the cause.</summary>
internal sealed class UnreadableFileException(string path, Exception inner)
    : IOException($"Cannot read '{path}': {inner.Message}", inner);
