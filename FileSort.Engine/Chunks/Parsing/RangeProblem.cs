using FileSort.Engine.Lines.Parsing;

namespace FileSort.Engine.Chunks.Parsing;

/// <summary>
/// The first line of a range that stops the sort: a line that is not valid, or one that is too long.
/// </summary>
/// <param name="Offset">Where the line starts in the chunk buffer.</param>
/// <param name="LineIndex">How many lines of the range come before it.</param>
/// <param name="Fault">Why the line is not valid; <see cref="LineFault.None"/> for a line that is too long.</param>
internal readonly record struct RangeProblem(int Offset, int LineIndex, LineFault Fault)
{
    public bool TooLong => Fault == LineFault.None;
}
