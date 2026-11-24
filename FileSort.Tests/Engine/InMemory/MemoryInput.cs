using FileSort.Engine.Chunks.Reading;

namespace FileSort.Tests.Engine.InMemory;

/// <summary>An input in memory, read at any position, which counts its reads.</summary>
internal sealed class MemoryInput(byte[] bytes) : IInputSource
{
    private readonly byte[] _bytes = bytes;

    public long Length => _bytes.Length;

    /// <summary>The bytes read so far, the same bytes as often as they were read.</summary>
    public long BytesRead { get; private set; }

    public int Read(long position, Span<byte> destination)
    {
        int count = (int)Math.Min(destination.Length, _bytes.Length - position);
        _bytes.AsSpan((int)position, count).CopyTo(destination);
        BytesRead += count;
        return count;
    }
}
