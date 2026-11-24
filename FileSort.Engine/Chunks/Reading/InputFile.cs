using Microsoft.Win32.SafeHandles;

namespace FileSort.Engine.Chunks.Reading;

/// <summary>
/// The input file, opened for positional reads with sequential access as a hint to the system. Only a regular file
/// will do: the sorter needs its length, and reads the bytes after the end of a chunk again for the next one, so a
/// device or a pipe, which can be read once from where it is, is refused. Other programs may read the file at the
/// same time.
/// </summary>
internal sealed class InputFile : IInputSource, IDisposable
{
    private readonly SafeFileHandle _handle;

    private InputFile(SafeFileHandle handle, long length)
    {
        _handle = handle;
        Length = length;
    }

    public long Length { get; }

    /// <exception cref="IOException">The file cannot be opened or is not a regular file; the message names the
    /// cause.</exception>
    public static InputFile Open(string path)
    {
        SafeFileHandle handle = File.OpenHandle(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
        try
        {
            // A pipe or a device has no length: asking for it throws.
            return new InputFile(handle, RandomAccess.GetLength(handle));
        }
        catch (NotSupportedException e)
        {
            handle.Dispose();
            throw new IOException("It is a device or a pipe, not a regular file.", e);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public int Read(long position, Span<byte> destination)
    {
        // One read may return fewer bytes than asked for; the loop asks again until the destination is full or the
        // file ends.
        int total = 0;
        while (total < destination.Length)
        {
            int read = RandomAccess.Read(_handle, destination[total..], position + total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    public void Dispose()
    {
        _handle.Dispose();
    }
}
