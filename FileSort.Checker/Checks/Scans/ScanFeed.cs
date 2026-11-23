using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using FileSort.Checker.Checks.Order;

namespace FileSort.Checker.Checks.Scans;

/// <summary>
/// What the scans tell the thread that owns the console while they run: how many bytes they have read, and the
/// order violations that are to be printed. The scans never write to the console themselves.
/// </summary>
internal sealed class ScanFeed
{
    private readonly ConcurrentQueue<OrderViolation> _violations = new();
    private long _bytesRead;

    /// <summary>The bytes of both files read so far.</summary>
    public long BytesRead => Interlocked.Read(ref _bytesRead);

    public void AddBytes(long bytes)
    {
        Interlocked.Add(ref _bytesRead, bytes);
    }

    public void Report(OrderViolation violation)
    {
        _violations.Enqueue(violation);
    }

    public bool TryTakeViolation([NotNullWhen(true)] out OrderViolation? violation)
    {
        return _violations.TryDequeue(out violation);
    }
}
