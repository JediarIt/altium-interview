using FileSort.Cli.Reports;
using FileSort.Cli.Terminal;

namespace FileSort.Tests.Cli.Reports;

public class ProgressLineTests
{
    private const long Total = 1000;

    [Fact]
    public void An_update_shows_the_share_the_throughput_and_the_time_left()
    {
        FakeConsole console = new();
        using ProgressLine progress = new(console, Total);

        progress.Report(250);

        Assert.StartsWith("\r", console.ErrorText);
        Assert.Contains("25.0", console.ErrorText);
        Assert.Contains("MiB/s", console.ErrorText);
        Assert.Contains(" left", console.ErrorText);
        Assert.Empty(console.OutText);
    }

    [Fact]
    public void Updates_come_at_most_once_per_interval()
    {
        FakeConsole console = new();
        using ProgressLine progress = new(console, Total);

        progress.Report(100);
        string afterFirst = console.ErrorText;
        progress.Report(200);

        // The second call follows the first within microseconds, far inside the interval.
        Assert.Equal(afterFirst, console.ErrorText);
    }

    [Fact]
    public void Nothing_is_written_to_a_redirected_error_stream()
    {
        FakeConsole console = new() { IsErrorRedirected = true };
        using ProgressLine progress = new(console, Total);

        progress.Report(250);
        progress.Clear();

        Assert.Empty(console.ErrorText);
    }

    [Fact]
    public void More_than_the_total_is_shown_as_complete()
    {
        FakeConsole console = new();
        using ProgressLine progress = new(console, Total);

        progress.Report(Total * 2);

        Assert.Contains("100.0", console.ErrorText);
        Assert.Contains("00:00:00 left", console.ErrorText);
    }

    [Fact]
    public void Before_the_first_byte_the_time_left_is_unknown()
    {
        FakeConsole console = new();
        using ProgressLine progress = new(console, 1L << 30);

        progress.Report(0);

        // No rate to estimate from: not 298 hours, as one byte per second would give.
        Assert.Contains("--:--:-- left", console.ErrorText);
    }

    [Fact]
    public void A_time_left_of_more_than_a_day_keeps_its_hours()
    {
        FakeConsole console = new();
        using ProgressLine progress = new(console, long.MaxValue / 2);

        progress.Report(1);

        // Far over a day at the rate of the first microseconds: the hours are not wrapped at 24, and the estimate stops
        // at the longest time shown instead of overflowing.
        Assert.Contains("9999:59:59 left", console.ErrorText);
    }

    [Fact]
    public void The_time_left_is_hours_minutes_and_seconds()
    {
        FakeConsole console = new();
        using ProgressLine progress = new(console, Total);

        // A tenth done at once: at least nine tenths of the elapsed time are left, so a few milliseconds or less.
        progress.Report(Total / 10);

        Assert.Matches(@"  \d{2}:\d{2}:\d{2} left$", console.ErrorText);
    }

    [Fact]
    public void A_total_of_zero_is_complete_from_the_start()
    {
        FakeConsole console = new();
        using ProgressLine progress = new(console, 0);

        progress.Report(0);

        Assert.Contains("100.0", console.ErrorText);
        Assert.DoesNotContain("NaN", console.ErrorText);
    }

    [Fact]
    public void Clear_blanks_the_line_and_returns_to_its_start()
    {
        FakeConsole console = new();
        ProgressLine progress = new(console, Total);
        progress.Report(250);
        int shown = console.ErrorText.Length - 1;

        progress.Clear();

        // The update, then a carriage return with as many spaces as the update had characters, then a carriage return.
        Assert.EndsWith("\r" + new string(' ', shown) + "\r", console.ErrorText);
    }

    [Fact]
    public void Clear_without_an_update_writes_nothing()
    {
        FakeConsole console = new();
        ProgressLine progress = new(console, Total);

        progress.Clear();
        progress.Dispose();

        Assert.Empty(console.ErrorText);
    }

    [Fact]
    public void A_failed_update_is_reported_as_a_failure_of_the_console()
    {
        FakeConsole console = new() { OnErrorWrite = _ => throw new IOException("The stream is broken.") };
        using ProgressLine progress = new(console, Total);

        ConsoleWriteException e = Assert.Throws<ConsoleWriteException>(() => progress.Report(250));

        Assert.Equal("The stream is broken.", e.Message);
        Assert.IsType<IOException>(e.InnerException);
    }

    [Fact]
    public void After_a_failed_update_the_line_writes_nothing_more()
    {
        int writes = 0;
        FakeConsole console = new()
        {
            OnErrorWrite = _ =>
            {
                writes++;
                throw new IOException("The stream is broken.");
            },
        };
        using ProgressLine progress = new(console, Total);
        Assert.Throws<ConsoleWriteException>(() => progress.Report(250));

        // Long past the interval, so only the broken stream can keep the line silent.
        Thread.Sleep(250);
        progress.Report(500);
        progress.Clear();

        Assert.Equal(1, writes);
    }

    [Fact]
    public void Clear_never_fails()
    {
        bool broken = false;
        FakeConsole console = new()
        {
            OnErrorWrite = _ =>
            {
                if (broken)
                {
                    throw new IOException("The terminal has gone.");
                }
            },
        };
        ProgressLine progress = new(console, Total);
        progress.Report(250);
        broken = true;

        progress.Clear();
        progress.Dispose();
    }
}
