namespace FileSort.Checker.Checks.Scans;

/// <summary>
/// The signal with which a scan that ended early stops the scan of the other file, so that a file that is wrong at
/// its start does not cost the full reading of the other. It is separate from the cancellation by the user, so a
/// cancellation is never taken for it.
/// </summary>
internal sealed class ScanStop
{
    private volatile bool _requested;

    public bool IsRequested => _requested;

    public void Request()
    {
        _requested = true;
    }
}
