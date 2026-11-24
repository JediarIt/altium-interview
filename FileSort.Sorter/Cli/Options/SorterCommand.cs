using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using FileSort.Cli.Options;

namespace FileSort.Sorter.Cli.Options;

/// <summary>
/// The command line of the sorter: its options, their accepted values and defaults, and the conversion of a parsed
/// command line into <see cref="SorterSettings"/>. The descriptions are the text of <c>--help</c>.
/// </summary>
internal sealed class SorterCommand
{
    public SorterCommand()
    {
        Input = PathOption("--input", "-i", "The file to sort. It is only read.");
        Output = PathOption(
            "--output",
            "-o",
            "The sorted file to write. The lines go to <path>.tmp, which is moved to <path> when it is complete.");

        Temp = new Option<string>("--temp")
        {
            Description = "The directory in which the sorter creates its temporary folder of runs, about as large as "
                + "the input. The default is the directory of the output. The folder is deleted however the sort ends.",
            HelpName = "path",
        };
        RejectEmptyPath(Temp);

        // The value stays the text that was typed and becomes bytes in Bind, so that the help shows the default as
        // 1GB and not as a number of bytes; the parser checks the text all the same.
        Memory = new Option<string>("--memory")
        {
            Description = "The largest peak working set of the process: a size such as 512MB or 1.5GB, at least 256MB. "
                + "Units are binary (1MB is 1048576 bytes).",
            HelpName = "size",
            DefaultValueFactory = _ => SorterSettings.DefaultMemory,
            CustomParser = CustomParsers.From<string>(CheckMemory),
        };

        Threads = new Option<int>("--threads")
        {
            Description = "The largest number of threads that parse, sort and merge lines at the same time, from 1 to "
                + $"{SorterSettings.MaxThreads}. The default is the number of logical processors, at most "
                + $"{SorterSettings.MaxThreads}. It does not change the output.",
            HelpName = "count",
            DefaultValueFactory = _ => SorterSettings.DefaultThreads,
            CustomParser = CustomParsers.WholeNumber(1, SorterSettings.MaxThreads),
        };

        LineEnding = new Option<string>("--line-ending")
        {
            Description = @"The line ending of the input and the output: lf (\n), crlf (\r\n), or auto, which takes "
                + @"it from the first line feed of the input: \r\n when a carriage return stands right before it, "
                + @"\n otherwise.",
            HelpName = "name",
            DefaultValueFactory = _ => LineEndingNames.Auto,
        };
        LineEnding.AcceptOnlyFromAmong(LineEndingNames.Auto, LineEndingNames.Lf, LineEndingNames.CrLf);

        Overwrite = new Option<bool>("--overwrite")
        {
            Description = "Replace an existing output file without asking. The old file is deleted once the whole "
                + "input has been read, right before the output is written.",
        };

        Root = new RootCommand(
            "Sorts a file of 'Number. String' lines by String and then by Number, within the given memory, whatever "
            + "its size: chunks of the input are sorted in memory and kept as runs in a temporary folder, then merged. "
            + "Exit codes: 0 sorted, 3 the input is not valid, 2 invalid arguments, 1 the sort could not be done, "
            + "130 cancelled.")
        {
            Input,
            Output,
            Temp,
            Memory,
            Threads,
            LineEnding,
            Overwrite,
        };
        Root.Validators.Add(RejectTheSameFileTwice);
    }

    public RootCommand Root { get; }

    public Option<string> Input { get; }

    public Option<string> Output { get; }

    public Option<string> Temp { get; }

    public Option<string> Memory { get; }

    public Option<int> Threads { get; }

    public Option<string> LineEnding { get; }

    public Option<bool> Overwrite { get; }

    /// <summary>Converts a command line that parsed without errors.</summary>
    public SorterSettings Bind(ParseResult result)
    {
        return new SorterSettings
        {
            InputPath = result.GetValue(Input)!,
            OutputPath = result.GetValue(Output)!,
            TempDirectory = result.GetValue(Temp),
            Memory = MemoryBytes(result.GetValue(Memory)!),
            Threads = result.GetValue(Threads),
            LineEndingName = result.GetValue(LineEnding)!,
            Overwrite = result.GetValue(Overwrite),
        };
    }

    private static bool CheckMemory(string text, out string value, [NotNullWhen(false)] out string? error)
    {
        value = text;
        return ParseMemory(text, out _, out error);
    }

    private static long MemoryBytes(string text)
    {
        if (!ParseMemory(text, out long bytes, out string? error))
        {
            throw new ArgumentException(error, nameof(text));
        }

        return bytes;
    }

    private static bool ParseMemory(string text, out long bytes, [NotNullWhen(false)] out string? error)
    {
        if (!SizeParser.TryParse(text, out bytes, out error))
        {
            return false;
        }

        if (bytes < SorterSettings.MinMemory)
        {
            error = string.Create(
                CultureInfo.InvariantCulture,
                $"'{text}' is too little: the sorter needs at least 256MB ({SorterSettings.MinMemory} bytes).");
            return false;
        }

        return true;
    }

    private static Option<string> PathOption(string name, string alias, string description)
    {
        Option<string> option = new(name, alias)
        {
            Description = description,
            HelpName = "path",
            Required = true,
        };
        RejectEmptyPath(option);
        return option;
    }

    // An empty path is a value the option does not accept, not a file that cannot be read or written.
    private static void RejectEmptyPath(Option<string> option)
    {
        option.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>()))
            {
                result.AddError("The path is empty.");
            }
        });
    }

    // A sort in place would lose its input: the old output, which is the input, is deleted before the new one is
    // written, so a failure or a Ctrl+C during the final merge would leave neither. The paths are read as they were
    // typed, because the value of an option that failed its own check cannot be asked for here.
    private void RejectTheSameFileTwice(CommandResult result)
    {
        if (result.GetResult(Input)?.Tokens is [{ Value: string input }]
            && result.GetResult(Output)?.Tokens is [{ Value: string output }]
            && IsSameFile(input, output))
        {
            result.AddError(
                $"Options '{Input.Name}' and '{Output.Name}' name the same file. The sorter does not sort a file in "
                + "place: write the output to another file.");
        }
    }

    // The two full paths, compared as the file system of the platform compares names. A second name for one file,
    // such as a link, is not looked for.
    private static bool IsSameFile(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        StringComparison names = OperatingSystem.IsLinux()
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), names);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException)
        {
            // Text that is not a path at all: opening it reports that.
            return false;
        }
    }
}
