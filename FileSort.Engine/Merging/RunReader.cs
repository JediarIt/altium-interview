using FileSort.Common;
using FileSort.Engine.Lines;
using FileSort.Engine.Lines.Parsing;
using FileSort.Engine.Runs;

namespace FileSort.Engine.Merging;

/// <summary>
/// Reads a run line by line through a buffer of its own, a slice of the chunk buffer. The current
/// line is parsed into a head of the merge, whose entry points into the chunk buffer, so lines of different runs
/// compare with one comparer. When the next line is not whole in the buffer, the rest of the buffer moves to its
/// start and the next bytes of the run are read after it.
/// </summary>
internal sealed class RunReader
{
    private readonly IRunStorage _storage;
    private readonly Run _run;
    private readonly byte[] _buffer;
    private readonly int _start;
    private readonly int _capacity;
    private readonly bool _carriageReturn;
    private readonly bool _headsWithKey;
    private long _runPosition;
    private int _filled;
    private int _lineStart;
    private int _lineEnd;

    /// <param name="storage">The storage of the run.</param>
    /// <param name="run">The run.</param>
    /// <param name="buffer">The chunk buffer.</param>
    /// <param name="start">Where the slice of this reader starts.</param>
    /// <param name="capacity">The bytes of the slice: at least a longest line with its terminator.</param>
    /// <param name="lineEnding">The line ending of the lines of the run.</param>
    /// <param name="headsWithKey">Whether the heads carry more of the key than their entries: see
    /// <see cref="MergeHead.WithoutKey"/>.</param>
    public RunReader(
        IRunStorage storage,
        Run run,
        byte[] buffer,
        int start,
        int capacity,
        LineEnding lineEnding,
        bool headsWithKey)
    {
        _storage = storage;
        _run = run;
        _buffer = buffer;
        _start = start;
        _capacity = capacity;
        _carriageReturn = lineEnding == LineEnding.CrLf;
        _headsWithKey = headsWithKey;
        _filled = start;
        _lineStart = start;
        _lineEnd = start;
    }

    /// <summary>The current line with its terminator, as it will be written.</summary>
    public ReadOnlySpan<byte> Line => _buffer.AsSpan(_lineStart, _lineEnd - _lineStart);

    /// <summary>Moves to the next line and gives its head for the merge; false at the end of the run.</summary>
    public bool Next(CancellationToken cancellationToken, out MergeHead head)
    {
        _lineStart = _lineEnd;
        int lineFeed = _buffer.AsSpan(_lineStart, _filled - _lineStart).IndexOf(LineFormat.LineFeed);
        if (lineFeed < 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Refill();
            lineFeed = _buffer.AsSpan(_lineStart, _filled - _lineStart).IndexOf(LineFormat.LineFeed);
        }

        if (lineFeed < 0)
        {
            head = default;
            return EndOfRun();
        }

        _lineEnd = _lineStart + lineFeed + 1;
        int contentLength = _carriageReturn ? lineFeed - 1 : lineFeed;
        ReadOnlySpan<byte> line = _buffer.AsSpan(_lineStart, contentLength);
        IndexEntry entry = LineParser.ParseValid(line, (uint)_lineStart);
        head = _headsWithKey ? new MergeHead(in entry, line) : MergeHead.WithoutKey(in entry);
        return true;
    }

    // The bytes not yet used move to the start of the slice, and the run fills the rest of it.
    private void Refill()
    {
        int left = _filled - _lineStart;
        _buffer.AsSpan(_lineStart, left).CopyTo(_buffer.AsSpan(_start));
        int read = _storage.Read(_run.Id, _runPosition, _buffer.AsSpan(_start + left, _capacity - left));
        _runPosition += read;
        _filled = _start + left + read;
        _lineStart = _start;
        _lineEnd = _start;
    }

    // Every line of a run ends with its terminator, so bytes without a line feed at the end of the run mean that the
    // run is broken.
    private bool EndOfRun()
    {
        if (_filled != _lineStart)
        {
            throw new InvalidDataException($"Run {_run.Id} ends inside a line.");
        }

        return false;
    }
}
