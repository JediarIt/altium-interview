using System.Globalization;

namespace FileSort.Cli.Reports;

/// <summary>A size as it is printed: the exact number of bytes, and the same in a unit a person can read.</summary>
public static class ByteSize
{
    /// <summary>For example <c>1073741824 bytes (1.00 GiB)</c>. The unit is binary, as everywhere in these
    /// programs.</summary>
    public static string Describe(long bytes)
    {
        (double value, string unit) = bytes switch
        {
            >= 1L << 30 => (bytes / (double)(1L << 30), "GiB"),
            >= 1L << 20 => (bytes / (double)(1L << 20), "MiB"),
            _ => (bytes / 1024.0, "KiB"),
        };
        return string.Create(CultureInfo.InvariantCulture, $"{bytes} bytes ({value:F2} {unit})");
    }
}
