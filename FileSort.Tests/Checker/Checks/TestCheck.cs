using System.Text;

using FileSort.Checker.Checks;
using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Lines;

namespace FileSort.Tests.Checker.Checks;

/// <summary>Runs the check of a pair on streams in memory.</summary>
internal static class TestCheck
{
    public static CheckResult Run(
        byte[] input,
        byte[] output,
        LineEnding? lineEnding = null,
        ScanSettings? settings = null,
        ScanFeed? feed = null)
    {
        using MemoryStream inputStream = new(input, writable: false);
        using MemoryStream outputStream = new(output, writable: false);
        settings ??= ScanSettings.Default;
        feed ??= new ScanFeed();
        return PairCheck.Run(inputStream, outputStream, lineEnding, settings, feed, CancellationToken.None);
    }

    public static CheckResult Run(
        string input, string output, LineEnding? lineEnding = null, ScanSettings? settings = null)
    {
        return Run(Encoding.UTF8.GetBytes(input), Encoding.UTF8.GetBytes(output), lineEnding, settings);
    }
}
