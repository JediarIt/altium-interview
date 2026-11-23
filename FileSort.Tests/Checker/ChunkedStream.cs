namespace FileSort.Tests.Checker;

/// <summary>
/// A stream that gives its content in small pieces, at most a few bytes per read, like a slow device: a reader
/// that expects a read to fill its buffer fails on it.
/// </summary>
internal sealed class ChunkedStream(byte[] content, int chunk) : MemoryStream(content, writable: false)
{
    private readonly int _chunk = chunk;

    public int Reads { get; private set; }

    // The reads into a span arrive here too: for a class derived from MemoryStream the framework routes them
    // through this overload.
    public override int Read(byte[] buffer, int offset, int count)
    {
        Reads++;
        return base.Read(buffer, offset, Math.Min(count, _chunk));
    }
}
