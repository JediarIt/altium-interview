using System.Globalization;

using FileSort.Cli.Terminal;
using FileSort.Engine;

using Spectre.Console;

namespace FileSort.Sorter.Cli.Terminal;

/// <summary>
/// The progress of a sort drawn with Spectre.Console on the error stream of a console (D15 of the design): a bar for
/// the chunks over the bytes of the input read, with the runs written so far, and a bar for each merge pass over the
/// bytes it writes, each with its throughput and the time left. The counters of the engine are read, and the bars
/// drawn, every 100 ms on the thread that follows the sort, so a console that cannot be written to fails there.
/// </summary>
/// <param name="console">The console of Spectre.Console that the bars are drawn on.</param>
internal sealed class BarsProgressView(IAnsiConsole console) : IProgressView
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    private readonly IAnsiConsole _console = console;

    public void Follow(SortProgress progress, Task sort)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(sort);

        Progress display = _console.Progress()
            .AutoRefresh(false)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn { Base = FileSizeBase.Binary, Culture = CultureInfo.InvariantCulture },
                new RemainingTimeColumn(),
                new SpinnerColumn());
        try
        {
            display.Start(context => Draw(new Bars(context), progress, sort));
        }
        catch (IOException e)
        {
            throw new ConsoleWriteException(e);
        }
    }

    private static void Draw(Bars bars, SortProgress progress, Task sort)
    {
        while (!((IAsyncResult)sort).AsyncWaitHandle.WaitOne(Interval))
        {
            bars.Show(progress);
        }

        bars.Show(progress);
        bars.Finish(sort.IsCompletedSuccessfully);
    }

    /// <summary>The bars on the screen: the chunks from the start of the sort, and each merge pass from its
    /// start.</summary>
    private sealed class Bars(ProgressContext context)
    {
        private readonly ProgressContext _context = context;
        private ProgressTask? _chunks;
        private ProgressTask? _pass;
        private int _passNumber;

        // The phase is read once: read twice, it could pass from Starting to Merge in between, and a pass would be
        // shown before the bar of the chunks exists.
        public void Show(SortProgress progress)
        {
            SortPhase phase = progress.Phase;
            if (phase != SortPhase.Starting)
            {
                ShowChunks(progress);
            }

            if (phase == SortPhase.Merge)
            {
                ShowPass(progress);
            }

            _context.Refresh();
        }

        // A sort that succeeded has read and written everything; one that failed keeps its bars where they stopped.
        public void Finish(bool succeeded)
        {
            foreach (ProgressTask? task in new[] { _chunks, _pass })
            {
                if (task is not null && succeeded)
                {
                    MoveTo(task, task.MaxValue);
                }

                task?.StopTask();
            }

            _context.Refresh();
        }

        private void ShowChunks(SortProgress progress)
        {
            _chunks ??= _context.AddTask("Chunks", maxValue: Math.Max(1, progress.InputBytes));
            int runs = progress.RunsWritten;
            _chunks.Description = runs switch
            {
                0 => "Chunks",
                1 => "Chunks, 1 run",
                _ => string.Create(CultureInfo.InvariantCulture, $"Chunks, {runs} runs"),
            };
            MoveTo(_chunks, progress.InputBytesRead);
        }

        // A pass that started and ended between two looks at the counters gets no bar of its own.
        private void ShowPass(SortProgress progress)
        {
            int pass = progress.Pass;
            if (pass != _passNumber)
            {
                MoveTo(_chunks!, _chunks!.MaxValue);
                _chunks.StopTask();
                FinishPass();
                _passNumber = pass;
                int passes = progress.Passes;
                string description = passes > 1
                    ? string.Create(CultureInfo.InvariantCulture, $"Merge, pass {pass} of {passes}")
                    : "Merge";
                _pass = _context.AddTask(description, maxValue: Math.Max(1, progress.PassBytes));
            }

            MoveTo(_pass!, progress.PassBytesWritten);
        }

        private void FinishPass()
        {
            if (_pass is not null)
            {
                MoveTo(_pass, _pass.MaxValue);
                _pass.StopTask();
            }
        }

        // Forward only, by increments, from which Spectre.Console computes the throughput and the time left.
        private static void MoveTo(ProgressTask task, double value)
        {
            double step = Math.Min(value, task.MaxValue) - task.Value;
            if (step > 0)
            {
                task.Increment(step);
            }
        }
    }
}
