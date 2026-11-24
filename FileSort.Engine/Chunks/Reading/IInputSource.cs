namespace FileSort.Engine.Chunks.Reading;

/// <summary>
/// The bytes of the input, read at any position. The sorter reads a file; tests and measurements of the processor
/// alone read bytes in memory.
/// </summary>
internal interface IInputSource
{
    /// <summary>The length of the input in bytes.</summary>
    long Length { get; }

    /// <summary>
    /// Reads bytes from a position until the destination is full or the input ends, and returns how many it read: as
    /// many as the destination holds unless the input ends first.
    /// </summary>
    int Read(long position, Span<byte> destination);
}
