namespace FileSort.Tests.Checker;

/// <summary>
/// A stream that never ends: the same line over and over, which is a sorted file of any size. It cannot seek, so
/// a check that reads it is given its line ending.
/// </summary>
internal sealed class EndlessStream(string line) : Stream
{
    private readonly byte[] _line = System.Text.Encoding.UTF8.GetBytes(line);
    private long _position;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            buffer[offset + i] = _line[(int)((_position + i) % _line.Length)];
        }

        _position += count;
        return count;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
