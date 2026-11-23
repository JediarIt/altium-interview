using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Lines;

namespace FileSort.Checker.Checks.Order;

/// <summary>
/// Compares every line of a chunk with the line before it. A line that comes before its predecessor is an order
/// violation: all of them are counted, and the first ones are kept to be printed. The lines are given as places in
/// the buffer of the chunk, which holds them all for the whole check, so nothing is copied but the few lines of the
/// violations that are kept.
/// </summary>
internal sealed class OrderCheck(byte[] buffer)
{
    /// <summary>How many violations are reported in full; the rest are only counted.</summary>
    public const int ReportedViolations = 10;

    private readonly byte[] _buffer = buffer;
    private readonly List<OrderViolation> _reported = [];

    // The line before the next one, as a place in the buffer; a length of -1 while there is none.
    private int _previousOffset;
    private int _previousLength = -1;
    private int _previousNumberLength;

    private long _lines;
    private long _violations;
    private long _firstViolationLine;
    private long _equalKeyPairs;

    /// <summary>What the check found in the lines it was given.</summary>
    public ChunkOrder Result => new(new OrderResult(_violations, _firstViolationLine, _equalKeyPairs), _reported);

    /// <summary>
    /// How a valid line stands to the valid line before it. The order is the one of
    /// <see cref="LineComparer.Compare"/>, the only definition of the order in the checker; this method only tells
    /// apart the ways in which two lines can stand.
    /// </summary>
    public static OrderVerdict Judge(
        ReadOnlySpan<byte> previous, int previousNumberLength, ReadOnlySpan<byte> line, int numberLength)
    {
        int order = LineComparer.Compare(previous, previousNumberLength, line, numberLength);
        if (order < 0)
        {
            return OrderVerdict.InOrder;
        }

        if (order > 0)
        {
            // Rare: a violation. Which part of the key put the line first is worth a second look at the Strings.
            int strings = LineComparer.CompareStrings(
                LineParser.Text(previous, previousNumberLength), LineParser.Text(line, numberLength));
            return strings != 0 ? OrderVerdict.StringsOutOfOrder : OrderVerdict.NumbersOutOfOrder;
        }

        // Equal keys: the lines are identical, or their Numbers differ in leading zeros only.
        bool sameNumbers = LineParser.Number(previous, previousNumberLength)
            .SequenceEqual(LineParser.Number(line, numberLength));
        return sameNumbers ? OrderVerdict.InOrder : OrderVerdict.EqualKeysDifferentBytes;
    }

    /// <summary>The violation that a verdict stands for, or null when the verdict is not a violation.</summary>
    public static OrderViolation? ViolationOf(
        OrderVerdict verdict, long lineNumber, ReadOnlySpan<byte> previous, ReadOnlySpan<byte> line)
    {
        OrderViolationKind? kind = verdict switch
        {
            OrderVerdict.StringsOutOfOrder => OrderViolationKind.StringsOutOfOrder,
            OrderVerdict.NumbersOutOfOrder => OrderViolationKind.NumbersOutOfOrder,
            _ => null,
        };
        return kind is null
            ? null
            : new OrderViolation(kind.Value, lineNumber, LineSample.Of(previous), LineSample.Of(line));
    }

    /// <summary>
    /// Takes a valid line that comes before the first line given to <see cref="Add"/> without being one of the
    /// lines of the check: the last line of the chunk before. It is compared with, and not counted.
    /// </summary>
    public void Precede(int offset, int length, int numberLength)
    {
        Remember(offset, length, numberLength);
    }

    /// <summary>Takes the next line: a valid line at this place in the buffer, and the length of its Number.</summary>
    public void Add(int offset, int length, int numberLength)
    {
        _lines++;
        if (_previousLength >= 0)
        {
            Compare(offset, length, numberLength);
        }

        Remember(offset, length, numberLength);
    }

    private void Compare(int offset, int length, int numberLength)
    {
        ReadOnlySpan<byte> previous = _buffer.AsSpan(_previousOffset, _previousLength);
        ReadOnlySpan<byte> line = _buffer.AsSpan(offset, length);
        OrderVerdict verdict = Judge(previous, _previousNumberLength, line, numberLength);
        if (verdict == OrderVerdict.InOrder)
        {
            return;
        }

        if (verdict == OrderVerdict.EqualKeysDifferentBytes)
        {
            _equalKeyPairs++;
            return;
        }

        _violations++;
        if (_violations == 1)
        {
            _firstViolationLine = _lines;
        }

        if (_violations <= ReportedViolations)
        {
            _reported.Add(ViolationOf(verdict, _lines, previous, line)!);
        }
    }

    // The line stays where it is: the buffer is not reused while the chunk is checked.
    private void Remember(int offset, int length, int numberLength)
    {
        _previousOffset = offset;
        _previousLength = length;
        _previousNumberLength = numberLength;
    }
}
