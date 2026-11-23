using System.Runtime.ExceptionServices;

using FileSort.Checker.Checks.Chunks;
using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Lines;

namespace FileSort.Checker.Checks;

/// <summary>
/// The check of a pair of files, the input and the output of a sort. Each file is read once, by a scan of its own,
/// and the two scans run at the same time. The input gives the digest of its lines; the output gives the same
/// plus the order check. The scans take streams, so tests run them on memory.
/// </summary>
internal static class PairCheck
{
    /// <summary>
    /// Checks the pair. Both streams are read with one line ending: the given one, or, when none is given, the one
    /// that the first line feed of the input shows. The input must be seekable for that.
    /// </summary>
    /// <exception cref="OperationCanceledException">The user cancelled the check.</exception>
    public static CheckResult Run(
        Stream input,
        Stream output,
        LineEnding? lineEnding,
        ScanSettings settings,
        ScanFeed feed,
        CancellationToken cancellationToken)
    {
        if (!TryChooseLineEnding(input, lineEnding, settings, out LineEnding chosen, out string? failure))
        {
            // The input cannot be read even at its start: its scan has failed, and the output is not read at all.
            return new CheckResult(
                chosen,
                LineEndingDetected: true,
                new ScanResult { End = ScanEnd.Failed, Failure = failure },
                new ScanResult { End = ScanEnd.Stopped });
        }

        // What the scans share besides the cancellation token and the feed: the threads that check their chunks,
        // and the signal that stops the other scan.
        using ChunkWorkers workers = new(settings.Threads);
        ScanStop stop = new();
        FileScan inputScan = Scan(input, chosen, settings, checkOrder: false, workers, feed, stop);
        FileScan outputScan = Scan(output, chosen, settings, checkOrder: true, workers, feed, stop);

        (ScanResult inputResult, ScanResult outputResult) = RunBoth(inputScan, outputScan, cancellationToken);
        return new CheckResult(chosen, LineEndingDetected: lineEnding is null, inputResult, outputResult);
    }

    private static FileScan Scan(
        Stream stream,
        LineEnding lineEnding,
        ScanSettings settings,
        bool checkOrder,
        ChunkWorkers workers,
        ScanFeed feed,
        ScanStop stop)
    {
        ChunkReader reader = new(stream, lineEnding, settings.ChunkSize, settings.MaxLineLength, keepPreviousLine: checkOrder);
        return new FileScan(reader, workers, lineEnding, settings, checkOrder, feed, stop);
    }

    private static bool TryChooseLineEnding(
        Stream input, LineEnding? given, ScanSettings settings, out LineEnding chosen, out string? failure)
    {
        chosen = given ?? LineEnding.Lf;
        failure = null;
        if (given is not null)
        {
            return true;
        }

        try
        {
            // Past the longest line with its terminator and a byte order mark, the first line is too long anyway.
            chosen = LineEndingDetector.Detect(input, settings.MaxLineLength + 2L + ChunkReader.ByteOrderMark.Length);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            failure = e.Message;
            return false;
        }
    }

    private static (ScanResult Input, ScanResult Output) RunBoth(
        FileScan input, FileScan output, CancellationToken cancellationToken)
    {
        // A thread of its own for each file: it reads, waits for a free worker and waits for results, and the
        // threads of the pool stay free for the checks of the chunks. The tasks themselves are not cancelled: a
        // scan looks at the token between chunks and ends by itself.
        Task<ScanResult> inputTask = StartOnItsOwnThread(() => input.Run(cancellationToken));
        Task<ScanResult> outputTask = StartOnItsOwnThread(() => output.Run(cancellationToken));
        try
        {
            Task.WaitAll(inputTask, outputTask);
        }
        catch (AggregateException e)
        {
            // Both scans have ended by now. A cancellation, or a failure that no scan expects, leaves as it was
            // thrown, not wrapped.
            ExceptionDispatchInfo.Capture(e.InnerExceptions[0]).Throw();
        }

        return (inputTask.Result, outputTask.Result);
    }

    private static Task<ScanResult> StartOnItsOwnThread(Func<ScanResult> scan)
    {
        return Task.Factory.StartNew(scan, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
}
