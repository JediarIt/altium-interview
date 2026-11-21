using System.Runtime.ExceptionServices;

using Microsoft.Win32.SafeHandles;

using FileSort.Generator.Pipeline;

namespace FileSort.Generator.Output;

/// <summary>
/// Writes the output to a temporary file next to the output path and moves it there only when everything is written:
/// the output path never holds a partial file. Large aligned chunks go to aligned offsets, one at a time and in
/// order, without the file cache of the operating system where that is supported (Windows), or through it in the
/// buffered mode. Disposing a sink that was not committed deletes the temporary file.
/// </summary>
/// <remarks>
/// The sink has two chunk buffers and a writer thread of its own. While one buffer is being written, the caller
/// fills the other, so the copy into the buffer overlaps the write instead of adding to it. The second buffer is
/// measured (see the README of this project, Performance, "The file sink"). The thread is dedicated by reasoning, not
/// by measurement: the thread pool is busy generating content, and a write queued there would wait behind the
/// blocks in flight.
/// </remarks>
internal sealed class FileSink : IBlockSink, IDisposable
{
    /// <summary>Lengths and offsets of unbuffered writes are multiples of this: the sector size of most disks.</summary>
    public const int Alignment = 4096;

    /// <summary>The chunk of the unbuffered mode. Chosen by the write-path experiment (see the README of this project, Performance).</summary>
    public const int UnbufferedChunkSize = 16 * 1024 * 1024;

    /// <summary>The chunk of the buffered mode. Chosen by the write-path experiment (see the README of this project, Performance).</summary>
    public const int BufferedChunkSize = 1024 * 1024;

    // FILE_FLAG_NO_BUFFERING. FileOptions has no name for it, but the runtime passes the value through to CreateFile.
    private const FileOptions NoBuffering = (FileOptions)0x20000000;

    private readonly string _outputPath;
    private readonly string _temporaryPath;
    private readonly AlignedBuffer[] _chunks;
    private readonly SafeFileHandle _handle;

    // The hand-over to the writer thread. The caller takes _writerIdle before it describes the next write and
    // releases _writeReady; the writer releases _writerIdle when that write has ended. So at most one write is in
    // flight, and the buffer that the caller fills is never the one being written.
    private readonly Thread _writer;
    private readonly SemaphoreSlim _writeReady = new(0);
    private readonly SemaphoreSlim _writerIdle = new(1);
    private AlignedBuffer? _writeChunk;
    private int _writeLength;
    private long _writeOffset;
    private ExceptionDispatchInfo? _writeError;
    private volatile bool _stopWriter;

    // The buffer that the caller fills; the other one is free or being written.
    private int _filling;
    private int _chunkFill;
    private long _fileOffset;
    private bool _committed;
    private bool _disposed;

    /// <summary>
    /// Creates the temporary file, replacing a stale one, and reserves <paramref name="preallocationSize"/> bytes for
    /// it, so a volume that is too small fails here and not in the middle of the content.
    /// </summary>
    public FileSink(string outputPath, long preallocationSize, bool unbuffered, int? chunkSize = null)
    {
        _outputPath = Path.GetFullPath(outputPath);
        _temporaryPath = TemporaryPath(_outputPath);
        Unbuffered = unbuffered && SupportsUnbuffered;

        int size = chunkSize ?? (Unbuffered ? UnbufferedChunkSize : BufferedChunkSize);
        ArgumentOutOfRangeException.ThrowIfLessThan(size, Alignment);
        if (size % Alignment != 0)
        {
            throw new ArgumentException("The chunk size must be a multiple of the alignment.", nameof(chunkSize));
        }

        _chunks = [new AlignedBuffer(size, Alignment), new AlignedBuffer(size, Alignment)];
        try
        {
            _handle = OpenTemporaryFile(_outputPath, Unbuffered, preallocationSize);
        }
        catch
        {
            DisposeChunks();
            throw;
        }

        _writer = new Thread(WriteChunks) { IsBackground = true, Name = "FileSort.Generator writer" };
        _writer.Start();
    }

    /// <summary>Whether this system can write without the file cache. Other systems always write through it.</summary>
    public static bool SupportsUnbuffered => OperatingSystem.IsWindows();

    /// <summary>Whether this sink writes without the file cache.</summary>
    public bool Unbuffered { get; }

    /// <summary>A length rounded up to a whole number of sectors.</summary>
    public static long AlignUp(long length)
    {
        return (length + Alignment - 1) / Alignment * Alignment;
    }

    /// <summary>The path of the temporary file for an output path: the same directory, so the move stays on one volume.</summary>
    public static string TemporaryPath(string outputPath)
    {
        return outputPath + ".tmp";
    }

    /// <summary>
    /// Creates the empty temporary file for an output path, the way a sink would, and deletes it again. It fails the
    /// way a sink would: a path that cannot be written, a drive that refuses to open a file for unbuffered writes. A
    /// caller that is about to delete an existing output file checks this first, so a run that cannot even start
    /// does not cost the old file. A drive that opens such a file but rejects the writes themselves is not caught
    /// here; it fails on the first write.
    /// </summary>
    public static void EnsureWritable(string outputPath, bool unbuffered)
    {
        string fullOutputPath = Path.GetFullPath(outputPath);
        OpenTemporaryFile(fullOutputPath, unbuffered && SupportsUnbuffered, preallocationSize: 0).Dispose();

        // The check leaves nothing behind, also when the run stops before the sink creates the file for real.
        File.Delete(TemporaryPath(fullOutputPath));
    }

    private static SafeFileHandle OpenTemporaryFile(string fullOutputPath, bool unbuffered, long preallocationSize)
    {
        string? directory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return File.OpenHandle(
            TemporaryPath(fullOutputPath),
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            unbuffered ? NoBuffering : FileOptions.None,
            preallocationSize);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        while (!data.IsEmpty)
        {
            Span<byte> chunk = _chunks[_filling].Span;
            int count = Math.Min(data.Length, chunk.Length - _chunkFill);
            data[..count].CopyTo(chunk[_chunkFill..]);
            _chunkFill += count;
            data = data[count..];

            if (_chunkFill == chunk.Length)
            {
                StartWrite(chunk.Length);
            }
        }
    }

    /// <summary>Writes what is left, gives the file its exact length and moves it to the output path.</summary>
    public void Commit()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long length = _fileOffset + _chunkFill;
        if (_chunkFill > 0)
        {
            // An unbuffered write takes whole sectors only: the last chunk is padded, and the file is cut afterwards.
            int padded = Unbuffered ? (int)AlignUp(_chunkFill) : _chunkFill;
            _chunks[_filling].Span[_chunkFill..padded].Clear();
            StartWrite(padded);
        }

        WaitForWriter();
        _writerIdle.Release();

        RandomAccess.SetLength(_handle, length);
        RandomAccess.FlushToDisk(_handle);
        _handle.Dispose();

        File.Move(_temporaryPath, _outputPath);
        _committed = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // The writer ends the write in flight, if any, and stops: only then are the handle and the buffers released.
        _stopWriter = true;
        _writeReady.Release();
        _writer.Join();

        _handle.Dispose();
        DisposeChunks();
        _writeReady.Dispose();
        _writerIdle.Dispose();

        if (!_committed)
        {
            File.Delete(_temporaryPath);
        }
    }

    // Hands the filled buffer to the writer and switches to the other buffer, which is free once the previous
    // write has ended.
    private void StartWrite(int length)
    {
        WaitForWriter();

        _writeChunk = _chunks[_filling];
        _writeLength = length;
        _writeOffset = _fileOffset;
        _writeReady.Release();

        _fileOffset += length;

        // The other of the two buffers: 0 becomes 1 and 1 becomes 0.
        _filling = 1 - _filling;
        _chunkFill = 0;
    }

    // Returns when no write is in flight, holding _writerIdle. An error of the writer surfaces here, on the
    // caller's thread.
    private void WaitForWriter()
    {
        _writerIdle.Wait();
        if (_writeError is { } error)
        {
            _writerIdle.Release();
            error.Throw();
        }
    }

    private void WriteChunks()
    {
        while (true)
        {
            _writeReady.Wait();
            if (_stopWriter)
            {
                return;
            }

            try
            {
                RandomAccess.Write(_handle, _writeChunk!.Span[.._writeLength], _writeOffset);
            }
            catch (Exception e)
            {
                // Kept for the caller: nothing is written after a failed write, and the next call reports it.
                _writeError = ExceptionDispatchInfo.Capture(e);
            }

            _writerIdle.Release();
        }
    }

    private void DisposeChunks()
    {
        foreach (AlignedBuffer chunk in _chunks)
        {
            chunk.Dispose();
        }
    }
}
