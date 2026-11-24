namespace FileSort.Engine.Runs;

/// <summary>
/// A file opened for writing whose failures name the file: "There is not enough space on the disk" says nothing about
/// which of the files of a sort it was. The stream keeps no buffer of its own: runs and the output are written in
/// whole blocks.
/// </summary>
internal sealed class NamedFileStream(string path, FileMode mode)
    : FileStream(path, mode, FileAccess.Write, FileShare.Read, bufferSize: 0)
{
    public override void Write(byte[] buffer, int offset, int count)
    {
        try
        {
            base.Write(buffer, offset, count);
        }
        catch (IOException e)
        {
            throw Named(e);
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        try
        {
            base.Write(buffer);
        }
        catch (IOException e)
        {
            throw Named(e);
        }
    }

    private IOException Named(IOException e)
    {
        return new IOException($"Cannot write '{Name}': {e.Message}", e);
    }
}
