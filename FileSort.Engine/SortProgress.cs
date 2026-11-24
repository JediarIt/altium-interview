namespace FileSort.Engine;

/// <summary>
/// Counters of a sort that is running, for a progress display. The sort updates them once per piece of the input or
/// block of a run, never per line; another thread reads them whenever it likes.
/// </summary>
public sealed class SortProgress
{
    private int _phase;
    private long _inputBytes;
    private long _inputBytesRead;
    private int _runsWritten;
    private int _pass;
    private int _passes;
    private long _passBytes;
    private long _passBytesWritten;

    public SortPhase Phase => (SortPhase)Volatile.Read(ref _phase);

    /// <summary>The length of the input in bytes, known once the sort has started.</summary>
    public long InputBytes => Volatile.Read(ref _inputBytes);

    /// <summary>How far into the input the chunks have been read.</summary>
    public long InputBytesRead => Volatile.Read(ref _inputBytesRead);

    public int RunsWritten => Volatile.Read(ref _runsWritten);

    /// <summary>The merge pass that is running, from 1; 0 before the merge.</summary>
    public int Pass => Volatile.Read(ref _pass);

    /// <summary>How many merge passes the sort runs, known once the merge has started.</summary>
    public int Passes => Volatile.Read(ref _passes);

    /// <summary>The bytes that the running pass writes.</summary>
    public long PassBytes => Volatile.Read(ref _passBytes);

    /// <summary>The bytes that the running pass has written so far.</summary>
    public long PassBytesWritten => Volatile.Read(ref _passBytesWritten);

    internal void StartChunks(long inputBytes)
    {
        Volatile.Write(ref _inputBytes, inputBytes);
        Volatile.Write(ref _phase, (int)SortPhase.Chunks);
    }

    /// <summary>Moves the bytes read forward to a position of the input; a position behind it changes
    /// nothing.</summary>
    internal void ReadUpTo(long position)
    {
        if (position > Volatile.Read(ref _inputBytesRead))
        {
            Volatile.Write(ref _inputBytesRead, position);
        }
    }

    internal void RunWritten()
    {
        Interlocked.Increment(ref _runsWritten);
    }

    internal void StartPass(int pass, int passes, long bytes)
    {
        Volatile.Write(ref _passBytesWritten, 0);
        Volatile.Write(ref _passBytes, bytes);
        Volatile.Write(ref _passes, passes);
        Volatile.Write(ref _pass, pass);
        Volatile.Write(ref _phase, (int)SortPhase.Merge);
    }

    internal void Wrote(long bytes)
    {
        Interlocked.Add(ref _passBytesWritten, bytes);
    }
}
