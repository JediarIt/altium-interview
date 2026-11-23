namespace FileSort.Checker.Checks.Order;

/// <summary>Which part of the key puts a line before its predecessor.</summary>
internal enum OrderViolationKind
{
    /// <summary>The String of the line comes before the String of its predecessor.</summary>
    StringsOutOfOrder,

    /// <summary>The Strings are equal, and the Number of the line is smaller than the Number of its predecessor.</summary>
    NumbersOutOfOrder,
}
