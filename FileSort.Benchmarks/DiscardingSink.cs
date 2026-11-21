using FileSort.Generator.Pipeline;

namespace FileSort.Benchmarks;

/// <summary>Drops what it receives: measures how fast content is generated when nothing is written.</summary>
internal sealed class DiscardingSink : IBlockSink
{
    public void Write(ReadOnlySpan<byte> data)
    {
    }
}
