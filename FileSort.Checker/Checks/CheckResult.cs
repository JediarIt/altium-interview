using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Lines;

namespace FileSort.Checker.Checks;

/// <summary>The result of checking a pair of files: the line ending they were read with and what each scan
/// found.</summary>
internal sealed record CheckResult(LineEnding LineEnding, bool LineEndingDetected, ScanResult Input, ScanResult Output)
{
    /// <summary>
    /// The verdict, in order of precedence: a line that is not valid in either file; then a scan that could not
    /// finish; then the two checks of a valid pair.
    /// </summary>
    public CheckOutcome Outcome
    {
        get
        {
            if (Input.End == ScanEnd.InvalidLine || Output.End == ScanEnd.InvalidLine)
            {
                return CheckOutcome.InvalidFile;
            }

            if (Input.End != ScanEnd.Completed || Output.End != ScanEnd.Completed)
            {
                return CheckOutcome.NotChecked;
            }

            return InOrder && SameLines ? CheckOutcome.Correct : CheckOutcome.NotCorrect;
        }
    }

    /// <summary>No line of the output comes before its predecessor.</summary>
    public bool InOrder => Output.Order.Violations == 0;

    /// <summary>The output has the lines of the input, each as many times, as far as the digests can tell.</summary>
    public bool SameLines => Input.Digest == Output.Digest;

    /// <summary>
    /// The output looks like a file written with <c>\r\n</c> for an input written with <c>\n</c>: the files were
    /// read with <c>\n</c>, the output has lines followed by a terminator, every one of them ends with a carriage
    /// return, and that is not so for the input. Such an output consists of valid lines whose Strings end with a
    /// carriage return, so only the comparison of the lines fails, and this explains why.
    /// </summary>
    public bool LineEndingsSeemToDiffer =>
        LineEnding == LineEnding.Lf && Output.EveryTerminatedLineEndsWithCarriageReturn
        && !Input.EveryTerminatedLineEndsWithCarriageReturn;
}
