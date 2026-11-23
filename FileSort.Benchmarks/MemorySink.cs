using FileSort.Generator.Pipeline;

namespace FileSort.Benchmarks;

/// <summary>Keeps what it receives in memory: content for a benchmark that then reads it without a disk.</summary>
internal sealed class MemorySink(long capacity) : IBlockSink, IDisposable
{
    private readonly MemoryStream _content = new((int)capacity);

    public void Write(ReadOnlySpan<byte> data)
    {
        _content.Write(data);
    }

    public byte[] ToArray()
    {
        return _content.ToArray();
    }

    public void Dispose()
    {
        _content.Dispose();
    }
}
