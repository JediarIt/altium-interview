using System.Runtime.ExceptionServices;

using FileSort.Checker.Checks.Chunks;
using FileSort.Checker.Checks.Order;
using FileSort.Checker.Lines;

namespace FileSort.Checker.Checks.Scans;

/// <summary>
/// The scan of one file. One thread reads the file as chunks of whole lines, hands every chunk to the shared
/// workers and takes the results in the order of the chunks. That order is what gives every line its number, puts the
/// violations in the order of their lines, and makes the first line that is not valid the first one in the file.
/// The digest is a sum, so the chunks need no order for it.
/// </summary>
internal sealed class FileScan(
    ChunkReader reader, ChunkWorkers workers, LineEnding lineEnding, ScanSettings settings, bool checkOrder, ScanFeed feed, ScanStop stop)
{
    private readonly ChunkReader _reader = reader;
    private readonly ChunkWorkers _workers = workers;
    private readonly LineEnding _lineEnding = lineEnding;
    private readonly ScanSettings _settings = settings;
    private readonly bool _checkOrder = checkOrder;
    private readonly ScanFeed _feed = feed;
    private readonly ScanStop _stop = stop;

    // The chunks that were handed out and whose results are not merged yet, the oldest first.
    private readonly Queue<(Task<ChunkResult> Check, long Offset)> _pending = new();

    // What the chunks merged so far add up to.
    private long _mergedChunks;
    private LineDigest _digest;
    private long _terminatedLines;
    private long _carriageReturnEndings;
    private long _violations;
    private long _firstViolationLine;
    private long _equalKeyPairs;
    private int _reported;

    /// <summary>
    /// Reads the file to its end, or until something ends the scan early. A scan that ends early on its own, with
    /// a line that is not valid or with a failure, stops the scan of the other file.
    /// </summary>
    /// <exception cref="OperationCanceledException">The user cancelled the check.</exception>
    public ScanResult Run(CancellationToken cancellationToken)
    {
        try
        {
            return Read(cancellationToken) ?? Result(ScanEnd.Completed);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _stop.Request();
            return Result(ScanEnd.Failed) with { Failure = e.Message };
        }
        finally
        {
            // However the scan ends, no chunk is still being checked when it has ended.
            WaitForPending();
        }
    }

    // One chunk at a time. The cancellation by the user is looked at before every chunk. The stop signal of the
    // other scan is looked at only after a chunk was merged, so never before the first one, and never after the
    // last one: a file that fits in one chunk is always read to its end, and what is found in it does not depend
    // on which scan is faster. Returns how the scan ended early, or null at the end of the file.
    private ScanResult? Read(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Chunk? chunk = _reader.Read();
            _feed.AddBytes(_reader.ReadBytes);
            if (chunk is null)
            {
                return MergePending(leave: 0);
            }

            HandOut(chunk, cancellationToken);

            // The first chunk is merged before anything else is looked at. After that the results are taken as
            // they come, and the reading waits only when too many chunks are still on their way.
            ScanResult? ended = MergePending(leave: _mergedChunks == 0 ? 0 : _settings.Threads);
            if (ended is not null)
            {
                return ended;
            }

            if (!chunk.EndsFile && _stop.IsRequested)
            {
                return Result(ScanEnd.Stopped);
            }
        }
    }

    private void HandOut(Chunk chunk, CancellationToken cancellationToken)
    {
        if (chunk.IsLineWithoutEnd)
        {
            // Nothing to check: the reader found no end of the line within the limit. Its place in the order of
            // the chunks gives the line its number.
            ChunkResult tooLong = new()
            {
                End = ChunkEnd.LineTooLong,
                FaultyLine = new InvalidLine(1, 0, LineFault.None, LineSample.Of([])),
            };
            _pending.Enqueue((Task.FromResult(tooLong), chunk.Offset));
            return;
        }

        try
        {
            _pending.Enqueue((_workers.Start(() => Check(chunk), cancellationToken), chunk.Offset));
        }
        catch (OperationCanceledException)
        {
            ChunkReader.Release(chunk);
            throw;
        }
    }

    // Runs on a worker thread. The buffer of the chunk goes back to the pool as soon as the chunk is checked: the
    // result holds counts and the first violations, nothing that is as long as a line.
    private ChunkResult Check(Chunk chunk)
    {
        try
        {
            return new ChunkScan(chunk, _lineEnding, _settings.MaxLineLength, _checkOrder).Run();
        }
        finally
        {
            ChunkReader.Release(chunk);
        }
    }

    // Merges results in the order of their chunks. A result that is ready is always merged; a result that is not
    // ready is waited for only while more than `leave` chunks are pending. Returns how the scan ended, if a
    // merged chunk ended it.
    private ScanResult? MergePending(int leave)
    {
        while (_pending.Count > 0 && (_pending.Count > leave || _pending.Peek().Check.IsCompleted))
        {
            (Task<ChunkResult> check, long offset) = _pending.Dequeue();
            ScanResult? ended = Merge(ResultOf(check), offset);
            if (ended is not null)
            {
                _stop.Request();
                return ended;
            }
        }

        return null;
    }

    private ScanResult? Merge(ChunkResult chunk, long offset)
    {
        // The lines of the chunks before this one give the lines of this chunk their numbers.
        long linesBefore = _digest.Lines;
        if (chunk.Order is not null)
        {
            MergeOrder(chunk.Order, linesBefore);
        }

        _mergedChunks++;
        _digest = _digest.Add(chunk.Digest);
        _terminatedLines += chunk.TerminatedLines;
        _carriageReturnEndings += chunk.CarriageReturnEndings;
        if (chunk.End == ChunkEnd.Completed)
        {
            return null;
        }

        // Within the chunk the line has a number and an offset of its own: here they become those of the file.
        InvalidLine line = chunk.FaultyLine! with
        {
            LineNumber = linesBefore + chunk.FaultyLine.LineNumber,
            Offset = offset + chunk.FaultyLine.Offset,
        };
        return chunk.End == ChunkEnd.InvalidLine
            ? Result(ScanEnd.InvalidLine) with { InvalidLine = line }
            : Result(ScanEnd.Failed) with
            {
                Failure = LineLimit.Describe(line.LineNumber, line.Offset, _settings.MaxLineLength),
            };
    }

    // A chunk compares its first line with the last line of the chunk before it, which the reader puts in front of
    // it: every pair of neighbours lies in one chunk, and the chunks need no comparison here.
    private void MergeOrder(ChunkOrder order, long linesBefore)
    {
        foreach (OrderViolation violation in order.Violations)
        {
            Report(violation with { LineNumber = linesBefore + violation.LineNumber });
        }

        if (_firstViolationLine == 0 && order.Result.Violations > 0)
        {
            _firstViolationLine = linesBefore + order.Result.FirstViolationLine;
        }

        // The violations of the chunk were reported above, the first of them only; all of them are counted here.
        _violations += order.Result.Violations;
        _equalKeyPairs += order.Result.EqualKeyPairs;
    }

    // The first violations of the file go to the feed to be printed; they come here in the order of their lines.
    private void Report(OrderViolation violation)
    {
        if (_reported < OrderCheck.ReportedViolations)
        {
            _reported++;
            _feed.Report(violation);
        }
    }

    // A failure of a worker is a defect, not a state of the file: it leaves as it was thrown.
    private static ChunkResult ResultOf(Task<ChunkResult> check)
    {
        try
        {
            return check.Result;
        }
        catch (AggregateException e)
        {
            ExceptionDispatchInfo.Capture(e.InnerExceptions[0]).Throw();
            throw;
        }
    }

    private void WaitForPending()
    {
        try
        {
            Task.WaitAll(_pending.Select(pending => pending.Check).ToArray());
        }
        catch (AggregateException)
        {
            // How a check ended is reported where its result is merged; here the checks only have to be over.
        }
    }

    private ScanResult Result(ScanEnd end)
    {
        return new ScanResult
        {
            End = end,
            Digest = _digest,
            HasByteOrderMark = _reader.HasByteOrderMark,
            TerminatedLines = _terminatedLines,
            CarriageReturnEndings = _carriageReturnEndings,
            Order = new OrderResult(_violations, _firstViolationLine, _equalKeyPairs),
        };
    }
}
