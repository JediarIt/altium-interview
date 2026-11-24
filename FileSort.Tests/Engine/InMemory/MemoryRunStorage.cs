using FileSort.Engine.Runs;

namespace FileSort.Tests.Engine.InMemory;

/// <summary>
/// Runs and the output in memory: thousands of runs and several merge passes without a disk. It records what the
/// sort did with it, for the tests to compare with what the design says.
/// </summary>
internal sealed class MemoryRunStorage : IRunStorage
{
    private readonly Dictionary<int, MemoryStream> _runs = [];
    private MemoryStream? _output;
    private int _nextId;

    /// <summary>The runs that exist now, by number.</summary>
    public IReadOnlyCollection<int> Runs => _runs.Keys;

    /// <summary>The runs created so far, deleted or not.</summary>
    public int RunsCreated => _nextId;

    /// <summary>For every run created, how many runs existed right before it.</summary>
    public List<int> RunsBeforeEachRun { get; } = [];

    /// <summary>How many runs existed when the output was created.</summary>
    public int RunsBeforeOutput { get; private set; } = -1;

    /// <summary>Called when the output is created: lets a test act at the start of the final merge.</summary>
    public Action? OnCreateOutput { get; init; }

    /// <summary>The bytes written to the output, once it was created.</summary>
    public byte[]? Output => _output?.ToArray();

    /// <summary>The bytes of a run.</summary>
    public byte[] Run(int id) => _runs[id].ToArray();

    public Stream CreateRun(out int id)
    {
        RunsBeforeEachRun.Add(_runs.Count);
        id = _nextId++;
        MemoryStream run = new();
        _runs[id] = run;
        return new KeptOpen(run);
    }

    public int Read(int id, long position, Span<byte> destination)
    {
        MemoryStream run = _runs[id];
        int count = (int)Math.Max(0, Math.Min(destination.Length, run.Length - position));
        run.GetBuffer().AsSpan((int)position, count).CopyTo(destination);
        return count;
    }

    public void Delete(int id)
    {
        Assert.True(_runs.Remove(id), $"Run {id} was deleted twice, or never created.");
    }

    public Stream CreateOutput()
    {
        Assert.Null(_output);
        OnCreateOutput?.Invoke();
        RunsBeforeOutput = _runs.Count;
        _output = new MemoryStream();
        return new KeptOpen(_output);
    }

    public void CommitOutput()
    {
        Assert.NotNull(_output);
    }

    // The sort disposes the streams it writes; the bytes must stay readable afterwards.
    private sealed class KeptOpen(MemoryStream inner) : Stream
    {
        private readonly MemoryStream _inner = inner;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
    }
}
