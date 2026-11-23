using System.Globalization;

using FileSort.Checker.Checks.Order;

namespace FileSort.Checker.Cli.Reports;

/// <summary>An order violation as it is printed: its kind, the number of the line, that line and the line before it.</summary>
internal static class ViolationReport
{
    public static void Write(TextWriter writer, OrderViolation violation)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        string kind = violation.Kind == OrderViolationKind.NumbersOutOfOrder
            ? "equal Strings, Numbers out of order"
            : "Strings out of order";

        writer.WriteLine(string.Create(invariant, $"Order violation at output line {violation.LineNumber}: {kind}"));
        writer.WriteLine(string.Create(invariant, $"  {violation.LineNumber - 1}: {LineExcerpt.Format(violation.Previous)}"));
        writer.WriteLine(string.Create(invariant, $"  {violation.LineNumber}: {LineExcerpt.Format(violation.Line)}"));
    }
}
