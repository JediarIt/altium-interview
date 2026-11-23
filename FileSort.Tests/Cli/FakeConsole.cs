using System.Text;

using FileSort.Cli.Terminal;

namespace FileSort.Tests.Cli;

/// <summary>A console for tests: collects what is printed and answers a prompt from a list of lines.</summary>
internal sealed class FakeConsole : IAppConsole
{
    private readonly Queue<string> _input;
    private readonly CallbackWriter _out;
    private readonly CallbackWriter _error;

    public FakeConsole(params string[] input)
    {
        _input = new Queue<string>(input);
        _out = new CallbackWriter(() => OnOutWrite);
        _error = new CallbackWriter(() => OnErrorWrite);
    }

    public TextWriter Out => _out;

    public TextWriter Error => _error;

    public bool IsInputRedirected { get; init; }

    public bool IsErrorRedirected { get; init; }

    /// <summary>Called before every write to the error stream; lets a test act in the middle of a run.</summary>
    public Action<string>? OnErrorWrite { get; set; }

    /// <summary>Called before every write to standard output; lets a test break the stream of the summary.</summary>
    public Action<string>? OnOutWrite { get; set; }

    public string OutText => _out.Text;

    public string ErrorText => _error.Text;

    public int PromptsAnswered { get; private set; }

    public string? ReadLine()
    {
        PromptsAnswered++;
        return _input.Count > 0 ? _input.Dequeue() : null;
    }

    /// <summary>The value of a field of the summary, such as <c>Seed</c>.</summary>
    public string SummaryField(string name)
    {
        return Field(OutText, name);
    }

    /// <summary>The value of a field of the settings printed before the run, such as <c>Seed</c>.</summary>
    public string SettingsField(string name)
    {
        return Field(ErrorText, name);
    }

    private static string Field(string text, string name)
    {
        string line = text.Split('\n').Single(l => l.StartsWith(name + ":", StringComparison.Ordinal));
        return line[(name.Length + 1)..].Trim();
    }

    // Collects what is written; the callback, read at every write, can throw like a broken stream.
    private sealed class CallbackWriter(Func<Action<string>?> callback) : TextWriter
    {
        private readonly StringBuilder _text = new();

        public override Encoding Encoding => Encoding.UTF8;

        public string Text => _text.ToString();

        public override void Write(char value)
        {
            Write(value.ToString());
        }

        public override void Write(string? value)
        {
            callback()?.Invoke(value ?? "");
            _text.Append(value);
        }
    }
}
