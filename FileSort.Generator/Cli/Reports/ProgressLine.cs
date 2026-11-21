using System.Diagnostics;
using System.Globalization;

using FileSort.Generator.Cli.Terminal;

namespace FileSort.Generator.Cli.Reports;

/// <summary>
/// One line on the error stream that is rewritten in place: the percentage, the throughput and the estimated time
/// left. Nothing is written when the error stream is redirected, so a log file gets no progress updates.
/// </summary>
internal sealed class ProgressLine(IAppConsole console, long totalBytes) : IDisposable
{
    private const double Mebibyte = 1024 * 1024;

    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);

    // Null when the error stream is redirected, and after a write to it failed.
    private TextWriter? _writer = console.IsErrorRedirected ? null : console.Error;
    private readonly long _totalBytes = totalBytes;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private int _lineLength;

    /// <summary>Shows the progress for the bytes written so far, at most once per interval.</summary>
    public void Report(long writtenBytes)
    {
        TimeSpan elapsed = _elapsed.Elapsed;
        if (_writer is null || elapsed < _nextUpdate)
        {
            return;
        }

        _nextUpdate = elapsed + Interval;

        double done = Math.Min(1.0, writtenBytes / (double)_totalBytes);
        double seconds = Math.Max(elapsed.TotalSeconds, 0.001);
        double bytesPerSecond = writtenBytes / seconds;
        TimeSpan left = TimeSpan.FromSeconds(Math.Max(0, _totalBytes - writtenBytes) / Math.Max(bytesPerSecond, 1));

        string text = string.Create(
            CultureInfo.InvariantCulture,
            $"{done,6:P1}  {bytesPerSecond / Mebibyte,6:F0} MiB/s  {left:hh\\:mm\\:ss} left");
        Write(text);
    }

    /// <summary>
    /// Removes the progress line, so what is printed next starts on a clean line. It never fails: removing the line
    /// is cosmetic, and a broken error stream must not cost the summary of a finished run or hide the error of a
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

    private void Write(string text)
    {
        // The line is padded to the length of the previous one, so a shorter text leaves no tail behind.
        Put('\r' + text.PadRight(_lineLength));
        _lineLength = text.Length;
    }

    // A failed write ends the progress line: it writes nothing more, and the failure is reported as the console's,
    // not the output file's. A failed progress update stops the run: the console that would report its result is
    // broken.
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
