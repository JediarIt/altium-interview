using FileSort.Engine;

namespace FileSort.Sorter.Cli.Terminal;

/// <summary>
/// The view of a sort whose error stream is redirected: nothing about progress is written, so a log file gets the
/// settings and the messages only, as with the other programs.
/// </summary>
internal sealed class NoProgressView : IProgressView
{
    public void Follow(SortProgress progress, Task sort)
    {
        ArgumentNullException.ThrowIfNull(sort);
        ((IAsyncResult)sort).AsyncWaitHandle.WaitOne();
    }
}
