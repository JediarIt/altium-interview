using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

using FileSort.Generator.Cli.Options;

namespace FileSort.Benchmarks.Experiments;

/// <summary>
/// The options that the experiments share, built on System.CommandLine like the generator's own command line. Sizes
/// are parsed by the generator's <see cref="SizeParser"/>, so <c>--size</c> means the same in both programs.
/// </summary>
internal static class ExperimentOptions
{
    public const string Unbuffered = "unbuffered";
    public const string Buffered = "buffered";

    private delegate bool ItemParser<T>(string text, out T value);

    public static Option<string> File(bool required = true)
    {
        return new Option<string>("--file")
        {
            Description = "The file to write. It is deleted after every run.",
            HelpName = "path",
            Required = required,
        };
    }

    public static Option<long> Size()
    {
        return new Option<long>("--size")
        {
            Description = "The bytes written by one run, in the units of the generator: 64MB, 1GiB. A series writes "
                + "this size times its combinations times its runs: mind the total, it wears the disk.",
            HelpName = "size",
            DefaultValueFactory = _ => 1L << 30,
            CustomParser = result =>
            {
                if (SizeParser.TryParse(result.Tokens[0].Value, out long bytes, out string? error))
                {
                    return bytes;
                }

                result.AddError(error);
                return 0;
            },
        };
    }

    public static Option<int> Runs(int fallback)
    {
        return new Option<int>("--runs")
        {
            Description = "Runs per combination. A row shows the median, the slowest and the fastest run.",
            HelpName = "count",
            DefaultValueFactory = _ => fallback,
        };
    }

    public static Option<int> Pause()
    {
        return new Option<int>("--pause")
        {
            Description = "Seconds to wait after every run that wrote a file: an SSD needs idle time to empty its "
                + "fast cache, or the next run measures the cache.",
            HelpName = "seconds",
            DefaultValueFactory = _ => 0,
        };
    }

    /// <summary>Whole numbers above zero, separated by commas: <c>1,4,8,16</c>.</summary>
    public static Option<int[]> Numbers(string name, string description, params int[] fallback)
    {
        return new Option<int[]>(name)
        {
            Description = description,
            HelpName = "list",
            DefaultValueFactory = _ => fallback,
            CustomParser = result => ParseList(
                result,
                (string text, out int number) =>
                    int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0,
                "whole numbers above zero separated by commas, such as 1,4,8"),
        };
    }

    /// <summary>The write modes to measure, separated by commas.</summary>
    public static Option<string[]> Modes()
    {
        return new Option<string[]>("--modes")
        {
            Description = $"The write modes, separated by commas: {Unbuffered} (without the file cache of the "
                + $"operating system), {Buffered} (through it).",
            HelpName = "list",
            DefaultValueFactory = _ => [Unbuffered, Buffered],
            CustomParser = result => ParseList(
                result,
                (string text, out string mode) =>
                {
                    mode = text;
                    return text is Unbuffered or Buffered;
                },
                $"{Unbuffered}, {Buffered} or both separated by a comma"),
        };
    }

    private static T[] ParseList<T>(ArgumentResult result, ItemParser<T> parse, string expected)
    {
        string text = result.Tokens[0].Value;
        List<T> values = [];
        foreach (string item in text.Split(','))
        {
            if (!parse(item, out T value))
            {
                result.AddError($"'{text}' is not accepted. Expected {expected}.");
                return [];
            }

            values.Add(value);
        }

        return [.. values];
    }
}
