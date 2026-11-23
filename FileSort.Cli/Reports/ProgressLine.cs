using System.Diagnostics;
using System.Globalization;

using FileSort.Cli.Terminal;

namespace FileSort.Cli.Reports;

/// <summary>
/// One line on the error stream that is rewritten in place: the percentage, the throughput and the estimated time
/// left. Nothing is written when the error stream is redirected, so a log file gets no progress updates.
/// </summary>
public sealed class ProgressLine(IAppConsole console, long totalBytes) : IDisposable
{
    private const double Mebibyte = 1024 * 1024;

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);

    // Null when the error stream is redirected, and after a write to it failed.
    private TextWriter? _writer = console.IsErrorRedirected ? null : console.Error;
    private readonly long _totalBytes = totalBytes;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private int _lineLength;

    /// <summary>Shows the progress for the bytes processed so far, at most once per interval.</summary>
    public void Report(long doneBytes)
    {
        TimeSpan elapsed = _elapsed.Elapsed;
        if (_writer is null || elapsed < _nextUpdate)
        {
            return;
        }

        _nextUpdate = elapsed + Interval;

        // A total of zero bytes (two empty files) is complete from the start.
        double done = _totalBytes > 0 ? Math.Min(1.0, doneBytes / (double)_totalBytes) : 1.0;
        double seconds = Math.Max(elapsed.TotalSeconds, 0.001);
        double bytesPerSecond = doneBytes / seconds;

        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"{done,6:P1}  {bytesPerSecond / Mebibyte,6:F0} MiB/s  {TimeLeft(doneBytes, bytesPerSecond)} left");
        Write(text);
    }

    /// <summary>
    /// Removes the progress line, so what is printed next starts on a clean line. It never fails: removing the line
    /// is cosmetic, and a broken error stream must not cost the report of a finished run or hide the error of a
    /// failed one.
    /// </summary>
    public void Clear()
    {
        if (_writer is null || _lineLength == 0)
        {
            return;
        }

        try
        {
            Write("");
            Put("\r");
        }
        catch (ConsoleWriteException)
        {
            // The line stays on the screen, which is all that is lost.
        }
    }

    /// <summary>
    /// Removes the line if it is still shown: a run that fails or is cancelled prints its message on a clean line.
    /// </summary>
    public void Dispose() => Clear();

    // Hours, minutes and seconds, the hours not wrapped at a day: 27:46:40 for 100000 seconds, and at most
    // 9999:59:59, which an estimate from the first moments of a run can exceed. Before the first byte there is no
    // rate to estimate from, and the time is shown as --:--:--.
    private string TimeLeft(long doneBytes, double bytesPerSecond)
    {
        const double LongestShown = (9999 * 3600) + 3599;

        long leftBytes = Math.Max(0, _totalBytes - doneBytes);
        if (leftBytes > 0 && doneBytes <= 0)
        {
            return "--:--:--";
        }

        long seconds = (long)Math.Min(Math.Ceiling(leftBytes / Math.Max(bytesPerSecond, 1)), LongestShown);
        return string.Create(
            CultureInfo.InvariantCulture, $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}");
    }

    private void Write(string text)
    {
        // The line is padded to the length of the previous one, so a shorter text leaves no tail behind.
        Put('\r' + text.PadRight(_lineLength));
        _lineLength = text.Length;
    }

    // A failed write ends the progress line: it writes nothing more, and the failure is reported as the console's,
    // not as a failure of the file the program works with. A failed progress update stops the run: the console
    // that would report its result is broken.
    private void Put(string text)
    {
        try
        {
            _writer!.Write(text);
            _writer.Flush();
        }
        catch (IOException e)
        {
            _writer = null;
            throw new ConsoleWriteException(e);
        }
    }
}
