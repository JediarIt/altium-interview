using System.Text;

using FileSort.Checker.Checks.Order;
using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Lines;
using FileSort.Tests.Format;

namespace FileSort.Tests.Checker.Checks.Order;

public class OrderCheckTests
{
    private static (OrderResult Result, List<OrderViolation> Reported) Check(params string[] lines)
    {
        ChunkOrder order = Order(lines);
        return (order.Result, [.. order.Violations]);
    }

    // The digits of the Number of a valid line, or -1 for a line that is not valid.
    private static int NumberLengthOrMinusOne(byte[] line)
    {
        return LineParser.Parse(line, out int length) == LineFault.None ? length : -1;
    }

    // The lines as one chunk: their numbers count from 1.
    private static ChunkOrder Order(params string[] lines)
    {
        return OrderAfter(previous: null, lines);
    }

    // The lines as one chunk, with the last line of the chunk before in front of them when it is given. All of them
    // lie one after another in one buffer, as the lines of a chunk do.
    private static ChunkOrder OrderAfter(string? previous, params string[] lines)
    {
        byte[][] all = [.. (previous is null ? lines : [previous, .. lines]).Select(Encoding.UTF8.GetBytes)];
        byte[] buffer = [.. all.SelectMany(line => line)];
        OrderCheck check = new(buffer);
        int offset = 0;
        for (int i = 0; i < all.Length; i++)
        {
            Assert.Equal(LineFault.None, LineParser.Parse(all[i], out int numberLength));
            if (i == 0 && previous is not null)
            {
                check.Precede(offset, all[i].Length, numberLength);
            }
            else
            {
                check.Add(offset, all[i].Length, numberLength);
            }

            offset += all[i].Length;
        }

        return check.Result;
    }

    private static string Text(LineSample sample) => Encoding.UTF8.GetString(sample.Start);

    [Fact]
    public void Lines_in_order_have_no_violations()
    {
        (OrderResult result, List<OrderViolation> reported) = Check(
            "1. Apple",
            "415. Apple",
            "2. Banana is yellow",
            "32. Cherry is the best",
            "30432. Something something something");

        Assert.Equal(new OrderResult(0, 0, 0), result);
        Assert.Empty(reported);
    }

    [Fact]
    public void No_lines_and_one_line_are_in_order()
    {
        Assert.Equal(default, Check().Result);
        Assert.Equal(default, Check("1. Apple").Result);
    }

    [Fact]
    public void Strings_out_of_order()
    {
        (OrderResult result, List<OrderViolation> reported) = Check("2. Banana", "1. Apple");

        Assert.Equal(new OrderResult(1, 2, 0), result);
        OrderViolation violation = Assert.Single(reported);
        Assert.Equal(OrderViolationKind.StringsOutOfOrder, violation.Kind);
        Assert.Equal(2, violation.LineNumber);
        Assert.Equal("2. Banana", Text(violation.Previous));
        Assert.Equal("1. Apple", Text(violation.Line));
    }

    [Fact]
    public void Numbers_out_of_order()
    {
        (OrderResult result, List<OrderViolation> reported) = Check("10. Apple", "9. Apple");

        Assert.Equal(new OrderResult(1, 2, 0), result);
        Assert.Equal(OrderViolationKind.NumbersOutOfOrder, Assert.Single(reported).Kind);
    }

    [Fact]
    public void A_smaller_Number_after_a_greater_String_is_a_violation_of_the_Strings()
    {
        // The Numbers take part only when the Strings are equal.
        OrderViolation violation = Assert.Single(Check("1. Banana", "999. Apple").Reported);
        Assert.Equal(OrderViolationKind.StringsOutOfOrder, violation.Kind);
        Assert.Empty(Check("999. Apple", "1. Banana").Reported);
    }

    [Fact]
    public void Identical_lines_are_in_order_and_are_not_counted_as_a_pair_with_different_bytes()
    {
        Assert.Equal(new OrderResult(0, 0, 0), Check("7. Apple", "7. Apple", "7. Apple").Result);
    }

    [Theory]
    [InlineData("7. Apple", "007. Apple")]
    [InlineData("007. Apple", "7. Apple")]
    [InlineData("0. Apple", "000. Apple")]
    public void Equal_keys_with_different_bytes_are_in_order_and_are_counted(string first, string second)
    {
        (OrderResult result, List<OrderViolation> reported) = Check(first, second);

        Assert.Equal(new OrderResult(0, 0, 1), result);
        Assert.Empty(reported);
    }

    [Fact]
    public void Every_neighbouring_pair_with_equal_keys_and_different_bytes_is_counted()
    {
        Assert.Equal(
            3,
            Check("7. Apple", "07. Apple", "007. Apple", "7. Apple", "7. Apple", "8. Apple").Result.EqualKeyPairs);
    }

    [Fact]
    public void Every_line_is_compared_with_the_line_before_it()
    {
        // 3, 1, 2: only the second line comes before its predecessor. 3, 2, 1: the second and the third do.
        Assert.Equal(new OrderResult(1, 2, 0), Check("3. Apple", "1. Apple", "2. Apple").Result);
        Assert.Equal(new OrderResult(2, 2, 0), Check("3. Apple", "2. Apple", "1. Apple").Result);
        Assert.Equal(new OrderResult(1, 4, 0), Check("1. Apple", "2. Apple", "3. Apple", "1. Apple").Result);
    }

    [Fact]
    public void All_violations_are_counted_and_the_first_ten_are_reported()
    {
        // Numbers going down: every line but the first comes before its predecessor.
        string[] lines = [.. Enumerable.Range(0, 1001).Select(i => $"{2000 - i}. Apple")];

        (OrderResult result, List<OrderViolation> reported) = Check(lines);

        Assert.Equal(1000, result.Violations);
        Assert.Equal(2, result.FirstViolationLine);
        Assert.Equal(OrderCheck.ReportedViolations, reported.Count);
        Assert.Equal(Enumerable.Range(2, 10).Select(i => (long)i), reported.Select(v => v.LineNumber));
        Assert.Equal("1999. Apple", Text(reported[0].Line));
        Assert.Equal("2000. Apple", Text(reported[0].Previous));
    }

    [Fact]
    public void A_line_longer_than_any_before_is_still_remembered_whole()
    {
        string longText = new('x', 5000);

        // The long line is the predecessor of the last one, and differs from it only in its last byte.
        (OrderResult result, List<OrderViolation> reported) = Check("1. Apple", $"1. {longText}z", $"1. {longText}a");

        Assert.Equal(new OrderResult(1, 3, 0), result);
        OrderViolation violation = Assert.Single(reported);
        Assert.Equal(5004, violation.Previous.Length);
        Assert.Equal(LineSample.MaxBytes, violation.Previous.Start.Length);
        Assert.True(violation.Previous.IsCut);
    }

    [Fact]
    public void Numbers_above_64_bits_are_ordered_by_value()
    {
        Assert.Equal(
            default,
            Check("18446744073709551615. Apple", "18446744073709551616. Apple", "100000000000000000000000000000. Apple")
                .Result);
        Assert.Equal(
            1, Check("100000000000000000000000000000. Apple", "18446744073709551616. Apple").Result.Violations);
    }

    // The last line of the chunk before, in front of the lines of this one

    [Fact]
    public void The_first_line_is_compared_with_the_line_in_front_of_it()
    {
        ChunkOrder order = OrderAfter("9. Banana", "1. Apple", "2. Apple");

        // The line in front is not counted: the violation is at line 1 of the chunk, and has that line as its
        // predecessor.
        Assert.Equal(new OrderResult(1, 1, 0), order.Result);
        OrderViolation violation = Assert.Single(order.Violations);
        Assert.Equal(1, violation.LineNumber);
        Assert.Equal("9. Banana", Text(violation.Previous));
        Assert.Equal("1. Apple", Text(violation.Line));
    }

    [Fact]
    public void A_line_in_front_that_is_in_order_adds_nothing()
    {
        Assert.Equal(default, OrderAfter("1. Apple", "2. Apple", "1. Banana").Result);
    }

    [Fact]
    public void Equal_keys_with_the_line_in_front_are_counted()
    {
        Assert.Equal(new OrderResult(0, 0, 1), OrderAfter("007. Apple", "7. Apple").Result);
    }

    [Fact]
    public void A_line_in_front_and_no_lines_of_its_own_is_no_check()
    {
        ChunkOrder order = OrderAfter("9. Banana");

        Assert.Equal(default, order.Result);
        Assert.Empty(order.Violations);
    }

    [Fact]
    public void A_chunk_without_lines_has_no_violations()
    {
        ChunkOrder order = Order();

        Assert.Equal(default, order.Result);
        Assert.Empty(order.Violations);
    }

    [Theory]
    [InlineData("1. Apple", "2. Apple", nameof(OrderVerdict.InOrder))]
    [InlineData("7. Apple", "7. Apple", nameof(OrderVerdict.InOrder))]
    [InlineData("9. Apple", "1. Banana", nameof(OrderVerdict.InOrder))]
    [InlineData("7. Apple", "007. Apple", nameof(OrderVerdict.EqualKeysDifferentBytes))]
    [InlineData("1. Banana", "9. Apple", nameof(OrderVerdict.StringsOutOfOrder))]
    [InlineData("10. Apple", "9. Apple", nameof(OrderVerdict.NumbersOutOfOrder))]
    public void How_a_line_stands_to_the_line_before_it(string previous, string line, string expected)
    {
        byte[] first = Encoding.UTF8.GetBytes(previous);
        byte[] second = Encoding.UTF8.GetBytes(line);
        Assert.Equal(LineFault.None, LineParser.Parse(first, out int firstNumberLength));
        Assert.Equal(LineFault.None, LineParser.Parse(second, out int secondNumberLength));

        OrderVerdict verdict = OrderCheck.Judge(first, firstNumberLength, second, secondNumberLength);

        Assert.Equal(Enum.Parse<OrderVerdict>(expected), verdict);
        Assert.Equal(
            verdict is OrderVerdict.StringsOutOfOrder or OrderVerdict.NumbersOutOfOrder,
            OrderCheck.ViolationOf(verdict, 2, first, second) is not null);
    }

    // The scenarios of sort-order, judged the way the scan of the output judges every pair of neighbours

    [Theory]
    [MemberData(nameof(SortOrderScenarios.Orders), MemberType = typeof(SortOrderScenarios))]
    public void Every_pair_of_a_scenario_is_judged_in_order_one_way_and_out_of_order_the_other(
        string scenario, string[] lines, string[] expected)
    {
        Assert.NotEmpty(scenario);
        Assert.Equal(lines.Length, expected.Length);
        byte[][] sorted = [.. expected.Select(Encoding.UTF8.GetBytes)];
        int[] numberLengths = [.. sorted.Select(NumberLengthOrMinusOne)];
        Assert.DoesNotContain(-1, numberLengths);

        for (int i = 0; i < sorted.Length; i++)
        {
            for (int j = i + 1; j < sorted.Length; j++)
            {
                OrderVerdict verdict = OrderCheck.Judge(sorted[i], numberLengths[i], sorted[j], numberLengths[j]);
                Assert.Equal(OrderVerdict.InOrder, verdict);
                OrderVerdict reversed = OrderCheck.Judge(sorted[j], numberLengths[j], sorted[i], numberLengths[i]);
                Assert.True(
                    reversed is OrderVerdict.StringsOutOfOrder or OrderVerdict.NumbersOutOfOrder,
                    $"{expected[j]} | {expected[i]}: {reversed}");
            }
        }

        Assert.Equal(default, Order(expected).Result);
    }
}
