using System.Diagnostics;
using System.Text;

using FileSort.Checker.Checks;
using FileSort.Checker.Checks.Order;
using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Lines;

namespace FileSort.Tests.Checker.Checks;

/// <summary>
/// The check of a pair on streams in memory: the scenarios of "Valid files", "Order", "Same lines", "Line ending"
/// and "Byte order mark" of the <c>output-checker</c> spec, as far as the result of the check goes; then how a scan
/// that ends early stops the other, and cancellation.
/// </summary>
public class PairCheckTests
{
    private const string Example =
        "415. Apple\n30432. Something something something\n1. Apple\n32. Cherry is the best\n2. Banana is yellow\n";

    private const string SortedExample =
        "1. Apple\n415. Apple\n2. Banana is yellow\n32. Cherry is the best\n30432. Something something something\n";

    private static readonly byte[] Mark = [0xEF, 0xBB, 0xBF];

    // Small buffers for the tests that run a check many times; every file of these tests still fits in one.
    private static readonly ScanSettings Small = new() { ChunkSize = 4096, MaxLineLength = 4096 };

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static void AssertCorrect(CheckResult result)
    {
        Assert.Equal(CheckOutcome.Correct, result.Outcome);
        Assert.True(result.InOrder);
        Assert.True(result.SameLines);
        Assert.Equal(ScanEnd.Completed, result.Input.End);
        Assert.Equal(ScanEnd.Completed, result.Output.End);
    }

    private static InvalidLine AssertInvalidOutput(CheckResult result)
    {
        Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
        Assert.Equal(ScanEnd.InvalidLine, result.Output.End);
        Assert.NotNull(result.Output.InvalidLine);
        return result.Output.InvalidLine;
    }

    // Requirement: Valid files

    [Fact]
    public void Output_cut_inside_a_Number()
    {
        CheckResult result = TestCheck.Run("12. Banana\n1. Apple\n", "1. Apple\n12");

        InvalidLine invalid = AssertInvalidOutput(result);
        Assert.Equal(2, invalid.LineNumber);
        Assert.Equal(9, invalid.Offset);
        Assert.Equal(LineFault.NoSeparator, invalid.Fault);
        Assert.Equal("12"u8.ToArray(), invalid.Line.Start);
    }

    [Fact]
    public void Invalid_line_in_the_input()
    {
        CheckResult result = TestCheck.Run("1. Apple\nApple\n", "1. Apple\n");

        Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
        Assert.Equal(ScanEnd.InvalidLine, result.Input.End);
        Assert.Equal(2, result.Input.InvalidLine!.LineNumber);
        Assert.Equal(9, result.Input.InvalidLine.Offset);
        Assert.Equal(LineFault.NoNumber, result.Input.InvalidLine.Fault);

        // The output fits in one buffer, so its scan was not cut short: it is complete and has no fault of its own.
        Assert.Equal(ScanEnd.Completed, result.Output.End);
    }

    [Fact]
    public void Empty_line()
    {
        CheckResult result = TestCheck.Run("2. Banana\n1. Apple\n", "1. Apple\n\n2. Banana\n", LineEnding.Lf);

        InvalidLine invalid = AssertInvalidOutput(result);
        Assert.Equal(2, invalid.LineNumber);
        Assert.Equal(LineFault.Empty, invalid.Fault);
    }

    [Fact]
    public void Byte_order_mark_inside_the_output()
    {
        // A sorter took the mark of its input for a part of the first line, and sorting moved that line down.
        byte[] input = [.. Mark, .. "2. Banana\n1. Apple\n"u8];
        byte[] output = [.. "1. Apple\n"u8, .. Mark, .. "2. Banana\n"u8];

        InvalidLine invalid = AssertInvalidOutput(TestCheck.Run(input, output));

        Assert.Equal(2, invalid.LineNumber);
        Assert.Equal(LineFault.NoNumber, invalid.Fault);
    }

    [Fact]
    public void Ill_formed_UTF8()
    {
        byte[] output = [.. "1. Apple\n2. Ban"u8, 0xC3, 0x28, .. "na\n"u8];

        InvalidLine invalid = AssertInvalidOutput(TestCheck.Run(Utf8("2. Banana\n1. Apple\n"), output));

        Assert.Equal(2, invalid.LineNumber);
        Assert.Equal(LineFault.IllFormedString, invalid.Fault);
    }

    [Fact]
    public void An_invalid_line_ends_the_scan_at_once()
    {
        CheckResult result = TestCheck.Run("1. Apple\n2. Banana\n3. Cherry\n", "1. Apple\nBanana\n3. Cherry\n");

        // Only the line before the invalid one was counted: there is no reading on.
        AssertInvalidOutput(result);
        Assert.Equal(1, result.Output.Digest.Lines);
    }

    // Requirement: Order

    [Fact]
    public void Sorted_output()
    {
        CheckResult result = TestCheck.Run(Example, SortedExample);

        AssertCorrect(result);
        Assert.Equal(5, result.Input.Digest.Lines);
        Assert.Equal(5, result.Output.Digest.Lines);
        Assert.Equal(default, result.Output.Order);
    }

    [Fact]
    public void Strings_out_of_order()
    {
        ScanFeed feed = new();

        CheckResult result = TestCheck.Run(Utf8("2. Banana\n1. Apple\n"), Utf8("2. Banana\n1. Apple\n"), feed: feed);

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.False(result.InOrder);
        Assert.True(result.SameLines);
        Assert.Equal(new OrderResult(1, 2, 0), result.Output.Order);
        Assert.True(feed.TryTakeViolation(out OrderViolation? violation));
        Assert.Equal(OrderViolationKind.StringsOutOfOrder, violation.Kind);
        Assert.Equal(2, violation.LineNumber);
    }

    [Fact]
    public void Numbers_out_of_order()
    {
        ScanFeed feed = new();

        CheckResult result = TestCheck.Run(Utf8("10. Apple\n9. Apple\n"), Utf8("10. Apple\n9. Apple\n"), feed: feed);

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.Equal(new OrderResult(1, 2, 0), result.Output.Order);
        Assert.True(feed.TryTakeViolation(out OrderViolation? violation));
        Assert.Equal(OrderViolationKind.NumbersOutOfOrder, violation.Kind);
    }

    [Theory]
    [InlineData("7. Apple\n007. Apple\n")]
    [InlineData("007. Apple\n7. Apple\n")]
    public void Equal_keys_in_any_order(string output)
    {
        CheckResult result = TestCheck.Run("007. Apple\n7. Apple\n", output);

        AssertCorrect(result);
        Assert.Equal(new OrderResult(0, 0, 1), result.Output.Order);
    }

    [Fact]
    public void The_order_of_the_input_does_not_matter()
    {
        // Only the output is checked for order: the input of a sort is not sorted.
        CheckResult result = TestCheck.Run("9. Zebra\n1. Apple\n", "1. Apple\n9. Zebra\n");

        AssertCorrect(result);
        Assert.Equal(default, result.Input.Order);
    }

    // Requirement: Same lines

    [Fact]
    public void A_line_is_missing()
    {
        string withoutALine = SortedExample.Replace("2. Banana is yellow\n", "", StringComparison.Ordinal);
        CheckResult result = TestCheck.Run(Example, withoutALine);

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.True(result.InOrder);
        Assert.False(result.SameLines);
        Assert.Equal(5, result.Input.Digest.Lines);
        Assert.Equal(4, result.Output.Digest.Lines);
    }

    [Fact]
    public void A_line_replaced_by_a_copy_of_another()
    {
        CheckResult result = TestCheck.Run(
            Example, SortedExample.Replace("2. Banana is yellow\n", "415. Apple\n", StringComparison.Ordinal));

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.True(result.InOrder);
        Assert.False(result.SameLines);
        Assert.Equal(result.Input.Digest.Lines, result.Output.Digest.Lines);
    }

    [Fact]
    public void A_pair_of_identical_lines_replaced_by_another_pair()
    {
        CheckResult result = TestCheck.Run("1. Apple\n7. Kiwi\n1. Apple\n", "7. Kiwi\n7. Kiwi\n7. Kiwi\n");

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.True(result.InOrder);
        Assert.False(result.SameLines);
        Assert.Equal(result.Input.Digest.Lines, result.Output.Digest.Lines);
    }

    [Fact]
    public void One_byte_changed()
    {
        string changed = SortedExample.Replace("Cherry", "Cherri", StringComparison.Ordinal);
        CheckResult result = TestCheck.Run(Example, changed);

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.True(result.InOrder);
        Assert.False(result.SameLines);
        Assert.Equal(result.Input.Digest.Lines, result.Output.Digest.Lines);
        Assert.Equal(result.Input.Digest.Bytes, result.Output.Digest.Bytes);
    }

    [Fact]
    public void A_leading_zero_removed()
    {
        CheckResult result = TestCheck.Run("2. Banana\n007. Apple\n", "7. Apple\n2. Banana\n");

        // The two lines have equal keys, so the order is right; the lines are not the same.
        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.True(result.InOrder);
        Assert.False(result.SameLines);
    }

    [Theory]
    [InlineData("2. Banana\n1. Apple", "1. Apple\n2. Banana\n")]
    [InlineData("2. Banana\n1. Apple\n", "1. Apple\n2. Banana")]
    [InlineData("2. Banana\r\n1. Apple", "1. Apple\r\n2. Banana\r\n")]
    [InlineData("2. Banana\r\n1. Apple\r\n", "1. Apple\r\n2. Banana")]
    public void Terminator_of_the_last_line(string input, string output)
    {
        AssertCorrect(TestCheck.Run(input, output));
    }

    [Fact]
    public void Empty_files()
    {
        CheckResult result = TestCheck.Run("", "");

        AssertCorrect(result);
        Assert.Equal(0, result.Input.Digest.Lines);
        Assert.Equal(0, result.Output.Digest.Lines);
    }

    [Fact]
    public void An_empty_output_for_an_input_with_lines_is_in_order_and_not_correct()
    {
        // The order alone proves little: this is why the input is needed.
        CheckResult result = TestCheck.Run(Example, "");

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.True(result.InOrder);
        Assert.False(result.SameLines);
    }

    // Requirement: Line ending

    [Fact]
    public void Detected_line_feed()
    {
        CheckResult result = TestCheck.Run("2. Banana\n1. Apple\n", "1. Apple\n2. Banana\n");

        AssertCorrect(result);
        Assert.Equal(LineEnding.Lf, result.LineEnding);
        Assert.True(result.LineEndingDetected);
    }

    [Fact]
    public void Detected_carriage_return_and_line_feed()
    {
        CheckResult result = TestCheck.Run("2. Banana\r\n1. Apple\r\n", "1. Apple\r\n2. Banana\r\n");

        AssertCorrect(result);
        Assert.Equal(LineEnding.CrLf, result.LineEnding);
        Assert.True(result.LineEndingDetected);

        // No String has a carriage return: the lines are the eight and nine bytes of their text.
        Assert.Equal(0, result.Input.CarriageReturnEndings);
        Assert.Equal(0, result.Output.CarriageReturnEndings);
        Assert.Equal(17, result.Output.Digest.Bytes);
    }

    [Fact]
    public void Carriage_return_at_the_end_of_a_String()
    {
        CheckResult result = TestCheck.Run("2. Banana\n1. Apple\r\n", "1. Apple\r\n2. Banana\n");

        // Both files are read with \n, as the first line feed of the input shows; the String of one line is
        // "Apple" followed by a carriage return. The output alone would look like a \r\n file.
        AssertCorrect(result);
        Assert.Equal(LineEnding.Lf, result.LineEnding);
        Assert.Equal(1, result.Output.CarriageReturnEndings);
        Assert.Equal(18, result.Output.Digest.Bytes);
    }

    [Fact]
    public void Line_ending_given_explicitly()
    {
        const string File = "1. Apple\r\n2. Banana\n";

        CheckResult given = TestCheck.Run(File, File, LineEnding.Lf);
        CheckResult detected = TestCheck.Run(File, File);

        AssertCorrect(given);
        Assert.False(given.LineEndingDetected);

        Assert.Equal(CheckOutcome.InvalidFile, detected.Outcome);
        Assert.Equal(LineEnding.CrLf, detected.LineEnding);
        Assert.True(detected.LineEndingDetected);
        Assert.Equal(2, detected.Input.InvalidLine!.LineNumber);
        Assert.Equal(LineFault.BareLineFeed, detected.Input.InvalidLine.Fault);
    }

    [Fact]
    public void Input_without_a_line_feed()
    {
        CheckResult result = TestCheck.Run("1. Apple", "1. Apple\n");

        AssertCorrect(result);
        Assert.Equal(LineEnding.Lf, result.LineEnding);
    }

    [Fact]
    public void Output_with_line_feeds_for_an_input_with_carriage_returns()
    {
        CheckResult result = TestCheck.Run("2. Banana\r\n1. Apple\r\n", "1. Apple\n2. Banana\n");

        // The output fails at its first line, without being read as one long line.
        InvalidLine invalid = AssertInvalidOutput(result);
        Assert.Equal(1, invalid.LineNumber);
        Assert.Equal(0, invalid.Offset);
        Assert.Equal(LineFault.BareLineFeed, invalid.Fault);
        Assert.Equal(LineEnding.CrLf, result.LineEnding);
        Assert.True(result.LineEndingDetected);
    }

    [Fact]
    public void Output_with_carriage_returns_for_an_input_with_line_feeds()
    {
        CheckResult result = TestCheck.Run("2. Banana\n1. Apple\n", "1. Apple\r\n2. Banana\r\n");

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.True(result.InOrder);
        Assert.False(result.SameLines);
        Assert.True(result.LineEndingsSeemToDiffer);
    }

    [Fact]
    public void The_remark_about_line_endings_needs_every_line_of_the_output_to_end_with_a_carriage_return()
    {
        Assert.False(TestCheck.Run("2. Banana\n1. Apple\n", "1. Apple\r\n2. Banana\n").LineEndingsSeemToDiffer);
        Assert.False(TestCheck.Run("2. Banana\n1. Apple\n", "1. Apple\n2. Banana\n").LineEndingsSeemToDiffer);
        Assert.False(TestCheck.Run("2. Banana\n1. Apple\n", "").LineEndingsSeemToDiffer);

        // Every line of both files ends with a carriage return: with \n given, that is what the Strings are.
        CheckResult withLf = TestCheck.Run("2. Banana\r\n1. Apple\r\n", "1. Apple\r\n2. Banana\r\n", LineEnding.Lf);
        Assert.False(withLf.LineEndingsSeemToDiffer);

        // Read with \r\n, a carriage return at the end of every String is not a sign of anything.
        Assert.False(TestCheck.Run("1. Apple\r\n", "1. Apple\r\r\n", LineEnding.CrLf).LineEndingsSeemToDiffer);
    }

    // Requirement: Byte order mark

    [Fact]
    public void Mark_in_the_input_only()
    {
        CheckResult result = TestCheck.Run([.. Mark, .. Utf8(Example)], Utf8(SortedExample));

        AssertCorrect(result);
        Assert.True(result.Input.HasByteOrderMark);
        Assert.False(result.Output.HasByteOrderMark);
    }

    [Fact]
    public void Mark_in_the_output_only()
    {
        CheckResult result = TestCheck.Run(Utf8(Example), [.. Mark, .. Utf8(SortedExample)]);

        AssertCorrect(result);
        Assert.False(result.Input.HasByteOrderMark);
        Assert.True(result.Output.HasByteOrderMark);
    }

    [Fact]
    public void Two_files_of_a_mark_and_nothing_else_are_a_correct_pair_of_no_lines()
    {
        CheckResult result = TestCheck.Run(Mark, Mark);

        AssertCorrect(result);
        Assert.Equal(0, result.Output.Digest.Lines);
    }

    // A scan that ends early, and the other scan (D7)

    [Fact]
    public void An_invalid_line_in_each_file_is_recorded_for_both_on_every_run()
    {
        byte[] input = Utf8("1. Apple\nBanana\n3. Cherry\n");
        byte[] output = Utf8("1. Apple\n2. Banana\n\n3. Cherry\n");

        for (int run = 0; run < 100; run++)
        {
            CheckResult result = TestCheck.Run(input, output, settings: Small);

            // Neither file is cut short by the other scan: both fit in one buffer.
            Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
            Assert.Equal(ScanEnd.InvalidLine, result.Input.End);
            Assert.Equal(ScanEnd.InvalidLine, result.Output.End);
            Assert.Equal(2, result.Input.InvalidLine!.LineNumber);
            Assert.Equal(LineFault.NoNumber, result.Input.InvalidLine.Fault);
            Assert.Equal(3, result.Output.InvalidLine!.LineNumber);
            Assert.Equal(LineFault.Empty, result.Output.InvalidLine.Fault);
        }
    }

    [Fact]
    public void A_file_of_exactly_one_chunk_is_read_to_its_end_too()
    {
        // The output fills its chunk to the last byte, and its last line, without a terminator, is not valid. Read as
        // a chunk that does not reach the end of the file, that line would be left for a second chunk, which the
        // fault of the input could stop.
        byte[] input = Utf8("Apple\n");
        byte[] output = Utf8("1. Apple\n2. Banana\nCherry");
        ScanSettings settings = new() { ChunkSize = output.Length, MaxLineLength = 4096 };

        for (int run = 0; run < 100; run++)
        {
            CheckResult result = TestCheck.Run(input, output, LineEnding.Lf, settings);

            Assert.Equal(ScanEnd.InvalidLine, result.Input.End);
            Assert.Equal(ScanEnd.InvalidLine, result.Output.End);
            Assert.Equal(3, result.Output.InvalidLine!.LineNumber);
            Assert.Equal(19, result.Output.InvalidLine.Offset);
        }
    }

    [Fact]
    public void A_line_above_the_limit_ends_its_scan_with_a_failure()
    {
        ScanSettings settings = new() { ChunkSize = 4096, MaxLineLength = 20 };
        string longLine = "2. " + new string('x', 30);

        CheckResult result = TestCheck.Run($"1. Apple\n{longLine}\n", $"1. Apple\n{longLine}\n", settings: settings);

        Assert.Equal(CheckOutcome.NotChecked, result.Outcome);
        Assert.Equal(ScanEnd.Failed, result.Input.End);
        Assert.Equal(ScanEnd.Failed, result.Output.End);
        Assert.Contains("line 2", result.Input.Failure);
        Assert.Contains("starts at byte 9", result.Input.Failure);
        Assert.Contains("20 bytes", result.Input.Failure);
        Assert.Contains("--line-ending", result.Input.Failure);
        Assert.Null(result.Input.InvalidLine);
    }

    [Fact]
    public void A_line_above_the_limit_in_one_file_and_an_invalid_line_in_the_other_are_both_recorded()
    {
        ScanSettings settings = new() { ChunkSize = 4096, MaxLineLength = 20 };
        string input = "1. Apple\n2. " + new string('x', 30) + "\n";

        for (int run = 0; run < 100; run++)
        {
            CheckResult result = TestCheck.Run(input, "1. Apple\nBanana\n", settings: settings);

            // An invalid line in either file decides the outcome, and the failure of the other scan is kept.
            Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
            Assert.Equal(ScanEnd.Failed, result.Input.End);
            Assert.NotNull(result.Input.Failure);
            Assert.Equal(ScanEnd.InvalidLine, result.Output.End);
            Assert.Equal(2, result.Output.InvalidLine!.LineNumber);
        }
    }

    [Fact]
    public void A_file_that_cannot_be_read_ends_its_scan_with_a_failure()
    {
        using FailingStream input = new(Utf8(Example), failAt: 20);
        using MemoryStream output = new(Utf8(SortedExample));

        CheckResult result = PairCheck.Run(input, output, LineEnding.Lf, Small, new ScanFeed(), CancellationToken.None);

        Assert.Equal(CheckOutcome.NotChecked, result.Outcome);
        Assert.Equal(ScanEnd.Failed, result.Input.End);
        Assert.Equal(FailingStream.Message, result.Input.Failure);
        Assert.Equal(ScanEnd.Completed, result.Output.End);
    }

    [Fact]
    public void An_input_that_cannot_be_read_at_its_start_fails_before_the_output_is_read()
    {
        using FailingStream input = new(Utf8(Example), failAt: 0);
        using MemoryStream output = new(Utf8(SortedExample));

        // No line ending is given, so the check starts by looking for one in the input.
        CheckResult result = PairCheck.Run(
            input, output, lineEnding: null, Small, new ScanFeed(), CancellationToken.None);

        Assert.Equal(CheckOutcome.NotChecked, result.Outcome);
        Assert.Equal(ScanEnd.Failed, result.Input.End);
        Assert.Equal(FailingStream.Message, result.Input.Failure);
        Assert.Equal(ScanEnd.Stopped, result.Output.End);
        Assert.Equal(0, output.Position);
    }

    [Fact]
    public void A_scan_that_ends_early_stops_the_scan_of_the_other_file()
    {
        using MemoryStream input = new(Utf8("Apple\n"));
        using EndlessStream output = new("1. Apple\n");
        ScanSettings settings = new() { ChunkSize = 64 * 1024, MaxLineLength = 4096 };

        // Without the stop this check would never end: the output has no end.
        CheckResult result = PairCheck.Run(
            input, output, LineEnding.Lf, settings, new ScanFeed(), CancellationToken.None);

        Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
        Assert.Equal(ScanEnd.InvalidLine, result.Input.End);
        Assert.Equal(ScanEnd.Stopped, result.Output.End);
        Assert.Null(result.Output.InvalidLine);
        Assert.Null(result.Output.Failure);
    }

    [Fact]
    public void The_scans_report_the_bytes_they_read()
    {
        ScanFeed feed = new();

        TestCheck.Run(Utf8(Example), [.. Mark, .. Utf8(SortedExample)], feed: feed);

        // Every byte of both files, the byte order mark included.
        Assert.Equal(Example.Length + Mark.Length + SortedExample.Length, feed.BytesRead);
    }

    // Requirement: Cancellation

    [Fact]
    public void A_check_of_a_stream_that_never_ends_stops_when_it_is_cancelled()
    {
        using EndlessStream input = new("1. Apple\n");
        using EndlessStream output = new("1. Apple\n");
        using CancellationTokenSource cancellation = new();
        ScanFeed feed = new();

        Task<CheckResult> check = Task.Run(
            () => PairCheck.Run(input, output, LineEnding.Lf, ScanSettings.Default, feed, cancellation.Token));

        // The check is well under way before it is cancelled.
        SpinWait.SpinUntil(() => feed.BytesRead > 64 * 1024 * 1024, TimeSpan.FromSeconds(30));
        Assert.False(check.IsCompleted);
        Stopwatch stopwatch = Stopwatch.StartNew();
        cancellation.Cancel();

        AggregateException e = Assert.Throws<AggregateException>(
            () => check.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.IsAssignableFrom<OperationCanceledException>(e.InnerException);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"The check stopped after {stopwatch.Elapsed}.");
    }

    [Fact]
    public void A_check_that_is_cancelled_before_it_starts_reads_nothing()
    {
        using MemoryStream input = new(Utf8(Example));
        using MemoryStream output = new(Utf8(SortedExample));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        ScanFeed feed = new();

        Assert.ThrowsAny<OperationCanceledException>(
            () => PairCheck.Run(input, output, LineEnding.Lf, Small, feed, cancellation.Token));

        Assert.Equal(0, feed.BytesRead);
        Assert.Equal(0, output.Position);
    }
}
