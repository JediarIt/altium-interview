using FileSort.Engine.Chunks.Reading;

namespace FileSort.Benchmarks.Experiments.Sorter;

/// <summary>An input in memory, for the experiments that measure the processor alone.</summary>
internal sealed class MemoryInput(byte[] bytes) : IInputSource
{
    private readonly byte[] _bytes = bytes;

    public long Length => _bytes.Length;

    public int Read(long position, Span<byte> destination)
    {
        int count = (int)Math.Min(destination.Length, _bytes.Length - position);
        _bytes.AsSpan((int)position, count).CopyTo(destination);
        return count;
    }
}
