using System.Runtime.InteropServices;

namespace FileSort.Generator.Output;

/// <summary>
/// A block of native memory whose address is a multiple of an alignment. Unbuffered writes need their memory, their
/// length and their file offset aligned to the sector size of the disk; managed arrays give no such promise. This is
/// the only unsafe code of the generator.
/// </summary>
internal sealed unsafe class AlignedBuffer : IDisposable
{
    private void* _pointer;

    public AlignedBuffer(int length, int alignment)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alignment);
        _pointer = NativeMemory.AlignedAlloc((nuint)length, (nuint)alignment);
        Length = length;
    }

    ~AlignedBuffer()
    {
        Free();
    }

    public int Length { get; }

    public Span<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_pointer is null, this);
            return new Span<byte>(_pointer, Length);
        }
    }

    public void Dispose()
    {
        Free();
        GC.SuppressFinalize(this);
    }

    private void Free()
    {
        if (_pointer is not null)
        {
            NativeMemory.AlignedFree(_pointer);
            _pointer = null;
        }
    }
}
