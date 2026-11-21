using System.Diagnostics;
using System.Globalization;

namespace FileSort.Benchmarks.Experiments;

/// <summary>Shared helpers of the experiments: timing and the rows of the printed tables.</summary>
internal static class Measure
{
    public const long Mebibyte = 1024 * 1024;

    /// <summary>Runs an action and returns its throughput in MiB/s for the given number of bytes.</summary>
    public static double Throughput(long bytes, Action action)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        action();
        return ToThroughput(bytes, stopwatch.Elapsed);
    }

    public static double ToThroughput(long bytes, TimeSpan elapsed)
    {
        return bytes / (double)Mebibyte / elapsed.TotalSeconds;
    }

    /// <summary>Prints one row of a Markdown table.</summary>
    public static void Row(params object[] cells)
    {
        Console.WriteLine("| " + string.Join(" | ", cells.Select(Format)) + " |");
    }

    /// <summary>Prints the header of a Markdown table.</summary>
    public static void Header(params string[] columns)
    {
        Console.WriteLine("| " + string.Join(" | ", columns) + " |");
        Console.WriteLine("|" + string.Concat(columns.Select(_ => "---|")));
    }

    public static string Size(long bytes)
    {
        return bytes < 1L << 30
            ? string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)Mebibyte:0.#} MiB")
            : string.Create(CultureInfo.InvariantCulture, $"{bytes / (double)(1L << 30):0.#} GiB");
    }

    private static string Format(object cell)
    {
        return cell switch
        {
            double number => number.ToString("F0", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => cell.ToString() ?? "",
        };
    }
}
