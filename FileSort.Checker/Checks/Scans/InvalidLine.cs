using FileSort.Checker.Lines;

namespace FileSort.Checker.Checks.Scans;

/// <summary>
/// The line that ended a scan because it is not valid: its number from 1, the offset of its first byte from the
/// start of the file from 0, the reason, and the start of the line.
/// </summary>
internal sealed record InvalidLine(long LineNumber, long Offset, LineFault Fault, LineSample Line);
