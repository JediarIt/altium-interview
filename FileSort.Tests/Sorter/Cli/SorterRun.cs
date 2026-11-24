using System.Text;

using FileSort.Engine;
using FileSort.Sorter.Cli;
using FileSort.Tests.Cli;

namespace FileSort.Tests.Sorter.Cli;

/// <summary>
/// The sorter run in-process on small files of a directory of the test: the input is written, the program is run with
/// the console of the tests, and the output and what was printed are read back. The console counts its error stream
/// as redirected unless a test gives another one, so no progress is drawn.
/// </summary>
internal sealed class SorterRun : IDisposable
{
    private readonly TempDirectory _directory = new();

    public string DirectoryPath => _directory.Path;

    public string InputPath => _directory.File("input.txt");

    public string OutputPath => _directory.File("sorted.txt");

    public FakeConsole Console { get; set; } = new() { IsErrorRedirected = true };

    /// <summary>Changes the settings that the program gives the engine, right before the sort.</summary>
    public Func<SortSettings, SortSettings> Tune { get; set; } = settings => settings;

    /// <summary>The settings that the engine got in the last sort, after <see cref="Tune"/>.</summary>
    public SortSettings? EngineSettings { get; private set; }

    public string Out => Console.OutText;

    public string Error => Console.ErrorText;

    /// <summary>The files of the directory, in folders too, by their paths relative to it.</summary>
    public string[] FileNames => _directory.FileNames();

    public string[] Folders => [.. Directory.GetDirectories(DirectoryPath).Select(Path.GetFileName).OfType<string>()];

    public byte[] Output => File.ReadAllBytes(OutputPath);

    public void Dispose()
    {
        _directory.Dispose();
    }

    /// <summary>Writes the input and sorts it into the output, with these options added.</summary>
    public int Sort(string input, params string[] options)
    {
        return Sort(Encoding.UTF8.GetBytes(input), options);
    }

    public int Sort(byte[] input, params string[] options)
    {
        File.WriteAllBytes(InputPath, input);
        return Run(["--input", InputPath, "--output", OutputPath, .. options]);
    }

    /// <summary>Writes the input and sorts it with a token that can cancel the sort.</summary>
    public int SortCancellable(string input, CancellationToken cancellationToken, params string[] options)
    {
        return SortCancellable(Encoding.UTF8.GetBytes(input), cancellationToken, options);
    }

    public int SortCancellable(byte[] input, CancellationToken cancellationToken, params string[] options)
    {
        File.WriteAllBytes(InputPath, input);
        string[] args = ["--input", InputPath, "--output", OutputPath, .. options];
        return SorterApp.Run(args, Console, Capture, cancellationToken);
    }

    /// <summary>Runs the sorter with exactly these arguments.</summary>
    public int Run(params string[] args)
    {
        return SorterApp.Run(args, Console, Capture, CancellationToken.None);
    }

    /// <summary>The value of a field of the settings printed before the sort, such as <c>Threads</c>.</summary>
    public string SettingsField(string name)
    {
        return Value(Error, name);
    }

    /// <summary>The value of a field of the summary, such as <c>Lines</c>.</summary>
    public string SummaryField(string name)
    {
        return Value(Out, name);
    }

    /// <summary>The names of the fields of the summary, in their order.</summary>
    public string[] SummaryNames()
    {
        return [.. Lines(Out).Select(line => line.Split(':')[0])];
    }

    private SortSettings Capture(SortSettings settings)
    {
        EngineSettings = Tune(settings);
        return EngineSettings;
    }

    private static string Value(string text, string name)
    {
        string line = Lines(text).Single(l => l.StartsWith(name + ":", StringComparison.Ordinal));
        return line[(name.Length + 1)..].Trim();
    }

    private static string[] Lines(string text)
    {
        return [.. text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.Length > 0)];
    }
}
