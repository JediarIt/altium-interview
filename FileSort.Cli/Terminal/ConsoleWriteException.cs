namespace FileSort.Cli.Terminal;

/// <summary>
/// A write to the console failed, for example to a stream redirected to a file on a full disk. (A pipe whose reader
/// has gone is not such a failure: the runtime drops writes to it without an error.) A failure of a file that the
/// program works with is an <see cref="IOException"/> too; this type keeps the two apart, so the message names the
/// console and not that file.
/// </summary>
public sealed class ConsoleWriteException(IOException inner) : IOException(inner.Message, inner);
