namespace FileSort.Tests.Checker;

/// <summary>A stream of fixed content that cannot seek and cannot tell its length, like a pipe.</summary>
internal sealed class NonSeekableStream(byte[] content) : MemoryStream(content, writable: false)
{
    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
}
