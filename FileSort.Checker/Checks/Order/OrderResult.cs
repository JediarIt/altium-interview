namespace FileSort.Checker.Checks.Order;

/// <summary>
/// The order check of a file: how many lines come before their predecessor, the number of the first such line
/// (0 when there is none), and how many pairs of neighbouring lines have equal keys and different bytes, such as
/// <c>007. Apple</c> and <c>7. Apple</c>. Such a pair is in order whichever of the two comes first.
/// </summary>
internal readonly record struct OrderResult(long Violations, long FirstViolationLine, long EqualKeyPairs);
