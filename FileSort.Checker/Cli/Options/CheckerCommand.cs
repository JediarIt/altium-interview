using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

using FileSort.Checker.Checks;

namespace FileSort.Checker.Cli.Options;

/// <summary>
/// The command line of the checker: its options, their accepted values and defaults, and the conversion of a parsed
/// command line into <see cref="CheckerSettings"/>. The descriptions are the text of <c>--help</c>.
/// </summary>
internal sealed class CheckerCommand
{
    public CheckerCommand()
    {
        Input = PathOption(
            "--input", "-i", "The input file of the sort: the file that the sorted file was made from. It is only read.");

        // Not --output: in the other programs --output is a file that the program writes, and the checker writes none.
        Sorted = PathOption(
            "--sorted", "-s", "The sorted file, the output of the sorter, to check against the input. It is only read.");

        LineEnding = new Option<string>("--line-ending")
        {
            Description = @"The line ending that both files are read with: lf (\n), crlf (\r\n), or auto, which takes "
                + @"it from the first line feed of the input: \r\n when a carriage return stands right before it, \n "
                + "otherwise. A sorted file with another line ending than its input does not pass the check.",
            HelpName = "name",
            DefaultValueFactory = _ => LineEndingNames.Auto,
        };
        LineEnding.AcceptOnlyFromAmong(LineEndingNames.Auto, LineEndingNames.Lf, LineEndingNames.CrLf);

        Threads = new Option<int>("--threads")
        {
            Description = "The largest number of threads that check lines at the same time, from 1 to "
                + $"{ScanSettings.MaxThreads}; reading the two files takes two more. The default is the number of "
                + "logical processors. It does not change the result.",
            HelpName = "count",
            DefaultValueFactory = _ => ScanSettings.DefaultThreads,
            CustomParser = ParseThreads,
        };

        Root = new RootCommand(
            "Checks that a sorted file is the correctly sorted version of an input file: every line of both files is a "
            + "valid 'Number. String' line, the lines of the sorted file are ordered by String and then by Number, "
            + "and the sorted file has exactly the lines of the input. Exit codes: 0 correct, 4 not correct, 3 a file "
            + "is not valid, 2 invalid arguments, 1 the check could not be done, 130 cancelled.")
        {
            Input,
            Sorted,
            LineEnding,
            Threads,
        };
        Root.Validators.Add(RejectTheSameFileTwice);
    }

    public RootCommand Root { get; }

    public Option<string> Input { get; }

    public Option<string> Sorted { get; }

    public Option<string> LineEnding { get; }

    public Option<int> Threads { get; }

    /// <summary>Converts a command line that parsed without errors.</summary>
    public CheckerSettings Bind(ParseResult result)
    {
        return new CheckerSettings
        {
            InputPath = result.GetValue(Input)!,
            SortedPath = result.GetValue(Sorted)!,
            LineEndingName = result.GetValue(LineEnding)!,
            Threads = result.GetValue(Threads),
        };
    }

    private static Option<string> PathOption(string name, string alias, string description)
    {
        Option<string> option = new(name, alias)
        {
            Description = description,
            HelpName = "path",
            Required = true,
        };

        // An empty path is a value the option does not accept, not a file that cannot be read.
        option.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>()))
            {
                result.AddError("The path is empty.");
            }
        });
        return option;
    }

    // A whole number within the range, written with digits only. The option is named when the error is printed.
    private static int ParseThreads(ArgumentResult result)
    {
        string text = result.Tokens[0].Value;
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int threads)
            && threads >= 1 && threads <= ScanSettings.MaxThreads)
        {
            return threads;
        }

        result.AddError(string.Create(
            CultureInfo.InvariantCulture,
            $"'{text}' is not accepted. Expected a whole number from 1 to {ScanSettings.MaxThreads}."));
        return 0;
    }

    // A file compared with itself always has "the same lines": a slip of the hand would get CORRECT for a check
    // that compared nothing. The paths are read as they were typed, because the value of an option that failed its
    // own check cannot be asked for here.
    private void RejectTheSameFileTwice(CommandResult result)
    {
        if (result.GetResult(Input)?.Tokens is [{ Value: string input }]
            && result.GetResult(Sorted)?.Tokens is [{ Value: string sorted }]
            && IsSameFile(input, sorted))
        {
            result.AddError(
                $"Options '{Input.Name}' and '{Sorted.Name}' name the same file. The checker compares a sorted file "
                + "with the input it was made from.");
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

        StringComparison names = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
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
