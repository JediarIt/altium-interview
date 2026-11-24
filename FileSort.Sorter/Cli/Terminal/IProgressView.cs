using FileSort.Engine;

namespace FileSort.Sorter.Cli.Terminal;

/// <summary>Shows the progress of a sort that runs on another thread.</summary>
internal interface IProgressView
{
    /// <summary>
    /// Reads the counters of the sort every 100 ms and shows them, until the sort has ended, however it ended. A
    /// failure to write to the console leaves as a <see cref="FileSort.Cli.Terminal.ConsoleWriteException"/>.
    /// </summary>
    void Follow(SortProgress progress, Task sort);
}
