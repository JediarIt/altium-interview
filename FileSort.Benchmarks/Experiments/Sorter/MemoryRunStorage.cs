using FileSort.Engine.Runs;

namespace FileSort.Benchmarks.Experiments.Sorter;

/// <summary>
/// Runs in memory and an output that keeps nothing, for the experiments that measure the processor alone: the sort
/// reads its input and its runs from memory and writes nothing to the disk.
/// </summary>
internal sealed class MemoryRunStorage : IRunStorage
{
    private readonly Dictionary<int, MemoryStream> _runs = [];
    private int _nextId;

    /// <summary>The bytes written to the output.</summary>
    public long OutputBytes { get; private set; }

    public Stream CreateRun(out int id)
    {
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
        _runs.Remove(id);
    }

    public Stream CreateOutput()
    {
        return new Counting(this);
    }

    public void CommitOutput()
    {
    }

    // The sort disposes a run when it is written; its bytes must stay readable.
    private sealed class KeptOpen(MemoryStream inner) : WriteOnlyStream
    {
        private readonly MemoryStream _inner = inner;

        public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
    }

    // The output is only counted.
    private sealed class Counting(MemoryRunStorage storage) : WriteOnlyStream
    {
        private readonly MemoryRunStorage _storage = storage;

        public override void Write(ReadOnlySpan<byte> buffer) => _storage.OutputBytes += buffer.Length;
    }
}
