using System.Text;

using FileSort.Checker.Checks;
using FileSort.Checker.Cli;
using FileSort.Tests.Cli;

namespace FileSort.Tests.Checker.Cli;

/// <summary>
/// The checker run in-process on two small files of a directory of the test: the files are written, the
/// application is run with the console of the tests, and what it printed is read back.
/// </summary>
internal sealed class CheckerRun : IDisposable
{
    private readonly TempDirectory _directory = new();

    public string InputPath => _directory.File("input.txt");

    public string SortedPath => _directory.File("sorted.txt");

    public FakeConsole Console { get; set; } = new();

    public string Out => Console.OutText;

    public string Error => Console.ErrorText;

    /// <summary>The lines printed to standard output, without their line breaks.</summary>
    public string[] OutLines => [.. Out.Split('\n').Select(line => line.TrimEnd('\r'))];

    public string[] FileNames => _directory.FileNames();

    public void Dispose()
    {
        _directory.Dispose();
    }

    public int Check(string input, string sorted, params string[] options)
    {
        return Check(Encoding.UTF8.GetBytes(input), Encoding.UTF8.GetBytes(sorted), options);
    }

    public int Check(byte[] input, byte[] sorted, params string[] options)
    {
        return Check(input, sorted, ScanSettings.Default, CancellationToken.None, options);
    }

    /// <summary>Runs the check with other settings of the scans than the defaults, and with a token to cancel it.</summary>
    public int Check(string input, string sorted, ScanSettings scan, CancellationToken cancellationToken)
    {
        return Check(Encoding.UTF8.GetBytes(input), Encoding.UTF8.GetBytes(sorted), scan, cancellationToken);
    }

    public int Check(byte[] input, byte[] sorted, ScanSettings scan, CancellationToken cancellationToken, params string[] options)
    {
        File.WriteAllBytes(InputPath, input);
        File.WriteAllBytes(SortedPath, sorted);
        return CheckerApp.Run(["--input", InputPath, "--sorted", SortedPath, .. options], Console, scan, cancellationToken);
    }

    /// <summary>Runs the checker with exactly these arguments.</summary>
    public int Run(params string[] args)
    {
        return CheckerApp.Run(args, Console, CancellationToken.None);
    }

    /// <summary>The value of a field of the summary that stands at the start of a line, such as <c>Result</c>.</summary>
    public string Field(string name)
    {
        return Value(OutLines.Single(line => line.StartsWith(name + ":", StringComparison.Ordinal)), name);
    }

    /// <summary>The value of a field of one of the two files in the summary: <c>Field("Output", "Lines")</c>.</summary>
    public string Field(string file, string name)
    {
        string[] lines = OutLines;
        int start = Array.FindIndex(lines, line => line.StartsWith(file + ":", StringComparison.Ordinal));
        Assert.True(start >= 0, $"The summary has no '{file}:'.");

        // The fields of a file are the indented lines right after it.
        string field = lines.Skip(start + 1)
            .TakeWhile(line => line.StartsWith("  ", StringComparison.Ordinal))
            .Single(line => line.TrimStart().StartsWith(name + ":", StringComparison.Ordinal));
        return Value(field.TrimStart(), name);
    }

    /// <summary>The value of a field of the settings printed before the check, such as <c>Line ending</c>.</summary>
    public string SettingsField(string name)
    {
        string line = Error.Split('\n').Single(l => l.StartsWith(name + ":", StringComparison.Ordinal));
        return Value(line.TrimEnd('\r'), name);
    }

    private static string Value(string line, string name)
    {
        return line[(name.Length + 1)..].Trim();
    }
}
