namespace FileSort.Tests.Checker;

/// <summary>
/// A stream whose reads fail once a position is reached, like a file on a disk that has gone: the content before
/// that position is read as usual.
/// </summary>
internal sealed class FailingStream(byte[] content, long failAt) : MemoryStream(content, writable: false)
{
    public const string Message = "The device is not ready.";

    private readonly long _failAt = failAt;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Position >= _failAt)
        {
            throw new IOException(Message);
        }

        return base.Read(buffer, offset, (int)Math.Min(count, _failAt - Position));
    }
}
