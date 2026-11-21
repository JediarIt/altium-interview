namespace FileSort.Generator.Pipeline;

/// <summary>
/// Receives the bytes of the output in order. The pipeline calls it from one thread, and the data is valid only
/// during the call.
/// </summary>
internal interface IBlockSink
{
    void Write(ReadOnlySpan<byte> data);
}
