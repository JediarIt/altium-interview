namespace FileSort.Checker.Checks.Order;

/// <summary>How a line stands to the line before it.</summary>
internal enum OrderVerdict
{
    /// <summary>The line does not come before its predecessor, and the two have different keys or the same bytes.</summary>
    InOrder,

    /// <summary>
    /// The two lines have equal keys and different bytes: their Numbers differ in leading zeros only. They are in
    /// order whichever comes first.
    /// </summary>
    EqualKeysDifferentBytes,

    /// <summary>The String of the line comes before the String of its predecessor.</summary>
    StringsOutOfOrder,

    /// <summary>The Strings are equal, and the Number of the line is smaller than the Number of its predecessor.</summary>
    NumbersOutOfOrder,
}
