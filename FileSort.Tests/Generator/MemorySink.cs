using System.Buffers;

using FileSort.Generator.Pipeline;

namespace FileSort.Tests.Generator;

/// <summary>A sink that keeps everything in memory, for tests that look at the bytes.</summary>
internal sealed class MemorySink : IBlockSink
{
    private readonly ArrayBufferWriter<byte> _buffer = new();

    public void Write(ReadOnlySpan<byte> data)
    {
        data.CopyTo(_buffer.GetSpan(data.Length));
        _buffer.Advance(data.Length);
    }

    public byte[] ToArray()
    {
        return _buffer.WrittenSpan.ToArray();
    }
}
