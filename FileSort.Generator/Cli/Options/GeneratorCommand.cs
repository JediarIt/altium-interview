using System.Buffers.Binary;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using FileSort.Common;
using FileSort.Generator.Content;

namespace FileSort.Generator.Cli.Options;

/// <summary>
/// The command line of the generator: its options, their accepted values and defaults, and the conversion of a
/// parsed command line into <see cref="GeneratorSettings"/>. The descriptions are the text of <c>--help</c>.
/// </summary>
internal sealed class GeneratorCommand
{
    private delegate bool ValueParser<T>(string text, out T value, [NotNullWhen(false)] out string? error);

    public GeneratorCommand()
    {
        Output = new Option<string>("--output", "-o")
        {
            Description = "The file to create. Missing directories are created. The content goes to <path>.tmp "
                + "and is moved to <path> when it is complete.",
            HelpName = "path",
            Required = true,
        };

        // An empty path is a value the option does not accept, not a file that cannot be written.
        Output.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>()))
            {
                result.AddError("The path is empty.");
            }
        });

        Size = new Option<long>("--size", "-s")
        {
            Description = "The size of the file: a number with an optional unit B, KB, MB, GB or TB, such as 1GB "
                + "or 1.5GB. Units are binary (1KB is 1024 bytes); KiB, MiB, GiB and TiB mean the same. The file "
                + "ends with the first line that reaches the size.",
            HelpName = "size",
            Required = true,
            CustomParser = Parser<long>(SizeParser.TryParse),
        };

        Seed = new Option<ulong?>("--seed")
        {
            Description = "The seed of the content, a whole number from 0 to 18446744073709551615. The same seed "
                + "and content options give the same bytes. Random when omitted; the summary prints the seed used.",
            HelpName = "number",
            CustomParser = Parser<ulong?>((string text, out ulong? value, [NotNullWhen(false)] out string? error) =>
            {
                bool parsed = WholeNumberParser.TryParse(text, 0, ulong.MaxValue, out ulong number, out error);
                value = number;
                return parsed;
            }),
        };

        Duplicates = new Option<int>("--duplicates")
        {
            Description = "The probability, in percent from 0 to 100, that a line takes its String from a pool of "
                + $"{ContentFormat.PoolSize} repeated Strings. The other Strings are composed anew and still "
                + "repeat by chance, short ones most of all.",
            HelpName = "percent",
            DefaultValueFactory = _ => ContentOptions.DefaultDuplicatesPercent,
            CustomParser = Parser<int>((string text, out int value, [NotNullWhen(false)] out string? error) =>
            {
                bool parsed = WholeNumberParser.TryParse(text, 0, 100, out ulong number, out error);
                value = (int)number;
                return parsed;
            }),
        };

        Words = new Option<WordRange>("--words")
        {
            Description = "The number of words in a String: a number such as 4, or a range such as 1-5, from 1 "
                + $"to {WordRange.Limit}.",
            HelpName = "range",
            DefaultValueFactory = _ => WordRange.Default,
            CustomParser = Parser<WordRange>(WordRangeParser.TryParse),
        };

        EdgeCases = new Option<EdgeCases>("--edge-cases")
        {
            Description = "Adds valid but unusual lines. A list of categories separated by commas: "
                + $"{EdgeCaseNames.Accepted} (all of them). Without it every line is plain: a canonical Number "
                + "below 2^32 and ASCII words that every comparer orders the same way.",
            HelpName = "list",
            CustomParser = Parser<EdgeCases>(EdgeCaseNames.TryParse),
        };

        LineEnding = new Option<string>("--line-ending")
        {
            Description = @"The line ending of the file: lf (\n) or crlf (\r\n), the same on every operating system.",
            HelpName = "name",
            DefaultValueFactory = _ => LineEndingNames.Lf,
        };
        LineEnding.AcceptOnlyFromAmong(LineEndingNames.Lf, LineEndingNames.CrLf);

        Threads = new Option<int>("--threads")
        {
            Description = "The largest number of threads that generate content at the same time, from 1 to "
                + $"{GeneratorSettings.MaxThreads}. The default is the number of logical processors. It does "
                + "not change the content.",
            HelpName = "count",
            DefaultValueFactory = _ => GeneratorSettings.DefaultThreads,
            CustomParser = Parser<int>((string text, out int value, [NotNullWhen(false)] out string? error) =>
            {
                bool parsed = WholeNumberParser.TryParse(text, 1, GeneratorSettings.MaxThreads, out ulong number, out error);
                value = (int)number;
                return parsed;
            }),
        };

        Overwrite = new Option<bool>("--overwrite")
        {
            Description = "Replace an existing output file without asking. The old file is deleted before the "
                + "new one is written.",
        };

        BufferedIo = new Option<bool>("--buffered-io")
        {
            Description = "Write through the file cache of the operating system. By default the file is written "
                + "without it where that is supported (Windows), which is faster. Use this option on a drive "
                + "that rejects such writes.",
        };

        Root = new RootCommand(
            "Generates a test file for the sorter: lines of the form 'Number. String', with repeated Strings, "
            + "of the requested size. The same seed and options always give the same file.")
        {
            Output,
            Size,
            Seed,
            Duplicates,
            Words,
            EdgeCases,
            LineEnding,
            Threads,
            Overwrite,
            BufferedIo,
        };
    }

    public RootCommand Root { get; }

    public Option<string> Output { get; }

    public Option<long> Size { get; }

    public Option<ulong?> Seed { get; }

    public Option<int> Duplicates { get; }

    public Option<WordRange> Words { get; }

    public Option<EdgeCases> EdgeCases { get; }

    public Option<string> LineEnding { get; }

    public Option<int> Threads { get; }

    public Option<bool> Overwrite { get; }

    public Option<bool> BufferedIo { get; }

    /// <summary>Converts a command line that parsed without errors. A missing seed becomes a random one.</summary>
    public GeneratorSettings Bind(ParseResult result)
    {
        ulong? seed = result.GetValue(Seed);
        return new GeneratorSettings
        {
            OutputPath = result.GetValue(Output)!,
            Size = result.GetValue(Size),
            Content = new ContentOptions
            {
                Seed = seed ?? RandomSeed(),
                DuplicatesPercent = result.GetValue(Duplicates),
                Words = result.GetValue(Words),
                EdgeCases = result.GetValue(EdgeCases),
                LineEnding = LineEndingNames.Parse(result.GetValue(LineEnding)!),
            },
            SeedIsRandom = seed is null,
            Threads = result.GetValue(Threads),
            Overwrite = result.GetValue(Overwrite),
            BufferedIo = result.GetValue(BufferedIo),
        };
    }

    // The seed of a run that was given none. It only has to differ from run to run, so it comes from the operating
    // system's generator; the content itself is then drawn by Xoshiro256, which is what makes a seed reproducible.
    private static ulong RandomSeed()
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(RandomNumberGenerator.GetBytes(sizeof(ulong)));
    }

    // Wraps a value parser as the custom parser of an option. The option is named when the error is printed.
    private static Func<ArgumentResult, T?> Parser<T>(ValueParser<T> parser)
    {
        return result =>
        {
            if (parser(result.Tokens[0].Value, out T value, out string? error))
            {
                return value;
            }

            result.AddError(error);
            return default;
        };
    }
}
