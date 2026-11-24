using FileSort.Cli.Terminal;
using FileSort.Engine;
using FileSort.Sorter.Cli.Terminal;
using FileSort.Tests.Cli;

using Spectre.Console;

namespace FileSort.Tests.Sorter.Cli.Terminal;

/// <summary>
/// The bars of the progress, drawn on the error stream of the console of the tests. Spectre.Console is told that the
/// stream is an interactive terminal, so it draws as it does in a console, and what it wrote is read back as text.
/// Its own reading of the environment is turned off: on GitHub Actions it would otherwise decide, whatever the
/// settings say, that the terminal is not interactive, and print lines such as "Chunks: 40%" instead of the bars.
/// </summary>
public class BarsProgressViewTests
{
    private readonly FakeConsole _console = new();

    private string Drawn => _console.ErrorText;

    [Fact]
    public void A_redirected_error_stream_gets_no_view_that_draws()
    {
        Assert.IsType<NoProgressView>(ProgressViews.For(new FakeConsole { IsErrorRedirected = true }));
        Assert.IsType<BarsProgressView>(ProgressViews.For(new FakeConsole()));
    }

    [Fact]
    public void The_bars_end_with_the_state_of_the_sort()
    {
        SortProgress progress = new();
        progress.StartChunks(1000);
        progress.ReadUpTo(1000);
        progress.RunWritten();
        progress.RunWritten();
        progress.StartPass(1, 2, 600);
        progress.Wrote(600);
        progress.StartPass(2, 2, 1000);
        progress.Wrote(1000);

        View().Follow(progress, Task.CompletedTask);

        Assert.Contains("Chunks, 2 runs", Drawn, StringComparison.Ordinal);
        Assert.Contains("Merge, pass 2 of 2", Drawn, StringComparison.Ordinal);
        Assert.Contains("100%", Drawn, StringComparison.Ordinal);
        Assert.Empty(_console.OutText);
    }

    [Fact]
    public async Task The_bars_follow_a_sort_until_it_ends()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        SortProgress progress = new();
        TaskCompletionSource sort = new();
        Task following = Task.Run(() => View().Follow(progress, sort.Task), token);

        // The view draws while the phases move on; what it drew is read only after it has stopped, since the text is
        // not safe to read while another thread writes it, and how many times it drew depends on the machine.
        progress.StartChunks(1000);
        progress.ReadUpTo(400);
        await Task.Delay(300, token);

        progress.RunWritten();
        progress.StartPass(1, 1, 1000);
        progress.Wrote(300);
        await Task.Delay(300, token);
        Assert.False(following.IsCompleted);

        progress.Wrote(700);
        sort.SetResult();
        await following.WaitAsync(TimeSpan.FromSeconds(10), token);

        // One pass is the merge, without a number.
        Assert.Contains("Chunks, 1 run", Drawn, StringComparison.Ordinal);
        Assert.Contains("Merge", Drawn, StringComparison.Ordinal);
        Assert.DoesNotContain("pass 1 of 1", Drawn, StringComparison.Ordinal);
        Assert.Contains("100%", Drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void A_sort_that_fails_keeps_its_bars_where_they_stopped()
    {
        SortProgress progress = new();
        progress.StartChunks(1000);
        progress.ReadUpTo(500);

        View().Follow(progress, Task.FromException(new IOException("The disk is full.")));

        Assert.Contains("50%", Drawn, StringComparison.Ordinal);
        Assert.DoesNotContain("100%", Drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_is_drawn_before_the_sort_starts_reading()
    {
        View().Follow(new SortProgress(), Task.FromCanceled(new CancellationToken(canceled: true)));

        Assert.DoesNotContain("Chunks", Drawn, StringComparison.Ordinal);
    }

    [Fact]
    public void A_console_that_cannot_be_written_to_ends_the_view_with_a_console_write_exception()
    {
        _console.OnErrorWrite = _ => throw new IOException("The stream is broken.");
        SortProgress progress = new();
        progress.StartChunks(1000);

        Assert.Throws<ConsoleWriteException>(() => View().Follow(progress, Task.CompletedTask));
    }

    private BarsProgressView View()
    {
        return new BarsProgressView(AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(_console.Error),
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            ColorSystem = ColorSystemSupport.NoColors,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
        }));
    }
}
