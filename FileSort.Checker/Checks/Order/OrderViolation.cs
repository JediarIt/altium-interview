using FileSort.Checker.Checks.Scans;

namespace FileSort.Checker.Checks.Order;

/// <summary>A line of the output that comes before its predecessor: its number from 1, and the two lines.</summary>
internal sealed record OrderViolation(OrderViolationKind Kind, long LineNumber, LineSample Previous, LineSample Line);
