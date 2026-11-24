using FileSort.Checker.Checks;
using FileSort.Cli;
using FileSort.Cli.Reports;
using FileSort.Tests.Cli;

namespace FileSort.Tests.Checker.Cli;

/// <summary>
/// What the checker prints, and with which exit code: the scenarios of "Report of order violations", "Summary and
/// result" and "Settings and progress" of the <c>output-checker</c> spec, and the printed part of "Valid files",
/// "Order", "Same lines", "Line ending" and "Byte order mark".
/// </summary>
public sealed class CheckerReportTests : IDisposable
{
    private const string Input =
        "415. Apple\n30432. Something something something\n1. Apple\n32. Cherry is the best\n2. Banana is yellow\n";

    private const string Sorted =
        "1. Apple\n415. Apple\n2. Banana is yellow\n32. Cherry is the best\n30432. Something something something\n";

    private static readonly byte[] Mark = [0xEF, 0xBB, 0xBF];

    private readonly CheckerRun _run = new();

    public void Dispose()
    {
        _run.Dispose();
    }

    private static byte[] Utf8(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    private void AssertInvalidFile(int exitCode)
    {
        Assert.Equal(ExitCode.InvalidFile, exitCode);
        Assert.Equal("INVALID FILE", _run.Field("Result"));
        Assert.Equal("Result:           INVALID FILE", _run.OutLines.Last(line => line.Length > 0));

        // No summary, and no verdict on the order or the lines.
        Assert.DoesNotContain("Order:", _run.Out);
        Assert.DoesNotContain("Same lines:", _run.Out);
    }

    // Requirement: Valid files

    [Fact]
    public void Output_cut_inside_a_Number()
    {
        int exitCode = _run.Check("12. Banana\n1. Apple\n", "1. Apple\n12");

        AssertInvalidFile(exitCode);
        Assert.Contains($"Invalid line in the output: {_run.SortedPath}", _run.Out);
        Assert.Contains("  Line:           2", _run.Out);
        Assert.Contains("  Offset:         9 bytes", _run.Out);
        Assert.Contains("not followed by a period and a space", _run.Out);
        Assert.Contains("  Content:        12", _run.Out);
    }

    [Fact]
    public void Invalid_line_in_the_input()
    {
        int exitCode = _run.Check("1. Apple\nApple\n", "1. Apple\n");

        AssertInvalidFile(exitCode);
        Assert.Contains($"Invalid line in the input: {_run.InputPath}", _run.Out);
        Assert.Contains("  Line:           2", _run.Out);
        Assert.Contains("  Offset:         9 bytes", _run.Out);
        Assert.Contains("does not start with a digit", _run.Out);
        Assert.DoesNotContain("Invalid line in the output", _run.Out);
    }

    [Fact]
    public void Empty_line()
    {
        int exitCode = _run.Check("2. Banana\n1. Apple\n", "1. Apple\n\n2. Banana\n", "--line-ending", "lf");

        AssertInvalidFile(exitCode);
        Assert.Contains("  Line:           2", _run.Out);
        Assert.Contains("the line is empty", _run.Out);
        Assert.Contains("lf (given)", _run.Out);
        Assert.DoesNotContain("--line-ending", _run.Out);
    }

    [Fact]
    public void Byte_order_mark_inside_the_output()
    {
        byte[] input = [.. Mark, .. "2. Banana\n1. Apple\n"u8];
        byte[] output = [.. "1. Apple\n"u8, .. Mark, .. "2. Banana\n"u8];

        int exitCode = _run.Check(input, output);

        // The mark shows nothing on a console, so the report spells out its bytes.
        AssertInvalidFile(exitCode);
        Assert.Contains("does not start with a digit", _run.Out);
        Assert.Contains(@"  Content:        \xEF\xBB\xBF2. Banana", _run.Out);
    }

    [Fact]
    public void Ill_formed_UTF8()
    {
        byte[] output = [.. "1. Apple\n2. Ban"u8, 0xC3, 0x28, .. "na\n"u8];

        int exitCode = _run.Check(Utf8("2. Banana\n1. Apple\n"), output);

        AssertInvalidFile(exitCode);
        Assert.Contains("not well-formed UTF-8", _run.Out);
        Assert.Contains(@"  Content:        2. Ban\xC3(na", _run.Out);
    }

    [Fact]
    public void Order_violations_found_before_an_invalid_line_are_printed()
    {
        int exitCode = _run.Check("1. Apple\n2. Banana\n3. Cherry\n", "2. Banana\n1. Apple\nCherry\n");

        AssertInvalidFile(exitCode);
        Assert.True(
            _run.Out.IndexOf("Order violation at output line 2", StringComparison.Ordinal)
                < _run.Out.IndexOf("Invalid line in the output", StringComparison.Ordinal));
        Assert.Contains("  Line:           3", _run.Out);
    }

    // Requirement: Order

    [Fact]
    public void Sorted_output()
    {
        int exitCode = _run.Check(Input, Sorted);

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal("OK", _run.Field("Order"));
        Assert.Equal("OK", _run.Field("Same lines"));
        Assert.Equal("CORRECT", _run.Field("Result"));
        Assert.DoesNotContain("Order violation", _run.Out);
    }

    [Fact]
    public void Strings_out_of_order()
    {
        int exitCode = _run.Check("2. Banana\n1. Apple\n", "2. Banana\n1. Apple\n");

        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.Contains("Order violation at output line 2: Strings out of order", _run.Out);
        Assert.Contains("  1: 2. Banana", _run.OutLines);
        Assert.Contains("  2: 1. Apple", _run.OutLines);
        Assert.Equal("FAILED, 1 violation, the first at line 2", _run.Field("Order"));
        Assert.Equal("OK", _run.Field("Same lines"));
    }

    [Fact]
    public void Numbers_out_of_order()
    {
        int exitCode = _run.Check("10. Apple\n9. Apple\n", "10. Apple\n9. Apple\n");

        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.Contains("Order violation at output line 2: equal Strings, Numbers out of order", _run.Out);
        Assert.Contains("  1: 10. Apple", _run.OutLines);
        Assert.Contains("  2: 9. Apple", _run.OutLines);
    }

    [Theory]
    [InlineData("7. Apple\n007. Apple\n")]
    [InlineData("007. Apple\n7. Apple\n")]
    public void Equal_keys_in_any_order(string sorted)
    {
        int exitCode = _run.Check("007. Apple\n7. Apple\n", sorted);

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal("1 pair", _run.Field("Equal keys, different bytes"));
        Assert.Equal("OK", _run.Field("Order"));
    }

    // Requirement: Same lines

    [Fact]
    public void A_line_is_missing()
    {
        int exitCode = _run.Check(Input, Sorted.Replace("2. Banana is yellow\n", "", StringComparison.Ordinal));

        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.Equal("5", _run.Field("Input", "Lines"));
        Assert.Equal("4", _run.Field("Output", "Lines"));
        Assert.Equal("FAILED, the output has 1 line fewer", _run.Field("Same lines"));
        Assert.Equal("OK", _run.Field("Order"));
    }

    [Fact]
    public void Lines_that_the_input_does_not_have()
    {
        int exitCode = _run.Check("1. Apple\n", "1. Apple\n1. Apple\n1. Apple\n");

        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.Equal("FAILED, the output has 2 lines more", _run.Field("Same lines"));
    }

    [Theory]
    [InlineData("1. Apple\n415. Apple\n415. Apple\n32. Cherry is the best\n30432. Something something something\n")]
    [InlineData("1. Apple\n415. Apple\n2. Banana is yellow\n32. Cherri is the best\n"
        + "30432. Something something something\n")]
    public void The_same_number_of_lines_and_different_lines(string sorted)
    {
        int exitCode = _run.Check(Input, sorted);

        // A line replaced by a copy of another, or one byte changed: only the hashes tell.
        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.Equal("FAILED, the same number of lines, different lines", _run.Field("Same lines"));
        Assert.Equal("OK", _run.Field("Order"));
    }

    [Fact]
    public void A_pair_of_identical_lines_replaced_by_another_pair()
    {
        int exitCode = _run.Check("1. Apple\n7. Kiwi\n1. Apple\n", "7. Kiwi\n7. Kiwi\n7. Kiwi\n");

        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.StartsWith("FAILED", _run.Field("Same lines"));
    }

    [Fact]
    public void A_leading_zero_removed()
    {
        int exitCode = _run.Check("2. Banana\n007. Apple\n", "7. Apple\n2. Banana\n");

        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.Equal("OK", _run.Field("Order"));
        Assert.StartsWith("FAILED", _run.Field("Same lines"));
    }

    [Theory]
    [InlineData("2. Banana\n1. Apple", "1. Apple\n2. Banana\n")]
    [InlineData("2. Banana\n1. Apple\n", "1. Apple\n2. Banana")]
    public void Terminator_of_the_last_line(string input, string sorted)
    {
        Assert.Equal(ExitCode.Success, _run.Check(input, sorted));
        Assert.Equal("2", _run.Field("Output", "Lines"));
    }

    [Fact]
    public void Empty_files()
    {
        int exitCode = _run.Check("", "");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal("0", _run.Field("Input", "Lines"));
        Assert.Equal("0", _run.Field("Output", "Lines"));
        Assert.Equal("0 bytes (0.00 KiB)", _run.Field("Output", "Size"));
        Assert.Equal("CORRECT", _run.Field("Result"));
    }

    // Requirement: Line ending

    [Fact]
    public void Detected_line_feed()
    {
        Assert.Equal(ExitCode.Success, _run.Check("2. Banana\n1. Apple\n", "1. Apple\n2. Banana\n"));
        Assert.Equal("lf (from the input)", _run.Field("Line ending"));
    }

    [Fact]
    public void Detected_carriage_return_and_line_feed()
    {
        Assert.Equal(ExitCode.Success, _run.Check("2. Banana\r\n1. Apple\r\n", "1. Apple\r\n2. Banana\r\n"));
        Assert.Equal("crlf (from the input)", _run.Field("Line ending"));
    }

    [Fact]
    public void Carriage_return_at_the_end_of_a_String()
    {
        Assert.Equal(ExitCode.Success, _run.Check("2. Banana\n1. Apple\r\n", "1. Apple\r\n2. Banana\n"));
        Assert.Equal("lf (from the input)", _run.Field("Line ending"));
    }

    [Fact]
    public void Line_ending_given_explicitly()
    {
        const string Both = "1. Apple\r\n2. Banana\n";

        Assert.Equal(ExitCode.Success, _run.Check(Both, Both, "--line-ending", "lf"));
        Assert.Equal("lf (given)", _run.Field("Line ending"));
    }

    [Fact]
    public void Line_ending_detected_where_it_should_have_been_given()
    {
        // The same two contents as above, in two files; without the option \r\n is taken from the first line feed
        // of the input, and the second line of the input, which ends with a bare line feed, is not valid.
        int exitCode = _run.Check("1. Apple\r\n2. Banana\n", "1. Apple\r\n2. Banana\n");

        AssertInvalidFile(exitCode);
        Assert.Contains("Invalid line in the input", _run.Out);
        Assert.Contains("  Line:           2", _run.Out);
        Assert.Contains("--line-ending", _run.Out);
    }

    [Fact]
    public void Input_without_a_line_feed()
    {
        Assert.Equal(ExitCode.Success, _run.Check("1. Apple", "1. Apple\n"));
        Assert.Equal("lf (from the input)", _run.Field("Line ending"));
    }

    [Fact]
    public void Output_with_line_feeds_for_an_input_with_carriage_returns()
    {
        int exitCode = _run.Check("2. Banana\r\n1. Apple\r\n", "1. Apple\n2. Banana\n");

        AssertInvalidFile(exitCode);
        Assert.Contains($"Invalid line in the output: {_run.SortedPath}", _run.Out);
        Assert.Contains("  Line:           1", _run.Out);
        Assert.Contains(@"line feed without a carriage return before it", _run.Out);
        Assert.Contains(@"the line ending \r\n", _run.Out);
        Assert.Contains(@"  Content:        1. Apple\n", _run.Out);

        // The line ending was taken from the input, and the option that overrides it is named.
        Assert.Contains("  Line ending:    crlf (from the input); give --line-ending if that is wrong", _run.Out);
    }

    [Fact]
    public void Output_with_carriage_returns_for_an_input_with_line_feeds()
    {
        int exitCode = _run.Check("2. Banana\n1. Apple\n", "1. Apple\r\n2. Banana\r\n");

        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.Equal("OK", _run.Field("Order"));
        Assert.Equal("FAILED, the same number of lines, different lines", _run.Field("Same lines"));
        Assert.Contains("every line of the output ends with a carriage return", _run.Field("Note"));
        Assert.Contains("not every line of the input does", _run.Field("Note"));
    }

    [Fact]
    public void No_note_about_line_endings_for_a_correct_pair()
    {
        _run.Check(Input, Sorted);

        Assert.DoesNotContain("Note:", _run.Out);
    }

    // Requirement: Byte order mark

    [Fact]
    public void Mark_in_the_input_only()
    {
        int exitCode = _run.Check([.. Mark, .. Utf8(Input)], Utf8(Sorted));

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal("yes", _run.Field("Input", "Byte order mark"));
        Assert.Equal("no", _run.Field("Output", "Byte order mark"));
    }

    [Fact]
    public void Mark_in_the_output_only()
    {
        int exitCode = _run.Check(Utf8(Input), [.. Mark, .. Utf8(Sorted)]);

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal("no", _run.Field("Input", "Byte order mark"));
        Assert.Equal("yes", _run.Field("Output", "Byte order mark"));
        Assert.Equal("5", _run.Field("Output", "Lines"));
    }

    // Requirement: Report of order violations

    [Fact]
    public void A_few_violations()
    {
        const string File = "5. Apple\n4. Apple\n9. Banana\n1. Banana\n3. Cherry\n2. Cherry\n";

        int exitCode = _run.Check(File, File);

        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.Equal(3, _run.OutLines.Count(line => line.StartsWith("Order violation", StringComparison.Ordinal)));
        Assert.All(
            [
                "Order violation at output line 2: equal Strings, Numbers out of order",
                "  1: 5. Apple",
                "  2: 4. Apple",
                "Order violation at output line 4: equal Strings, Numbers out of order",
                "  3: 9. Banana",
                "  4: 1. Banana",
                "Order violation at output line 6: equal Strings, Numbers out of order",
                "  5: 3. Cherry",
                "  6: 2. Cherry",
            ],
            line => Assert.Contains(line, _run.OutLines));
        Assert.Equal("FAILED, 3 violations, the first at line 2", _run.Field("Order"));
    }

    [Fact]
    public void More_violations_than_are_printed()
    {
        // Numbers going down: every line but the first comes before its predecessor.
        string file = string.Concat(Enumerable.Range(0, 1001).Select(i => $"{2000 - i}. Apple\n"));

        int exitCode = _run.Check(file, file);

        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.Equal(10, _run.OutLines.Count(line => line.StartsWith("Order violation", StringComparison.Ordinal)));
        Assert.Contains("Order violation at output line 11:", _run.Out);
        Assert.DoesNotContain("Order violation at output line 12:", _run.Out);
        Assert.Equal("FAILED, 1000 violations, the first at line 2", _run.Field("Order"));
    }

    [Fact]
    public void A_long_line_is_shortened_in_a_violation()
    {
        string longText = new('x', 1000);
        string file = $"2. {longText}\n1. {longText}\n";

        _run.Check(file, file);

        Assert.Contains($"  1: 2. {new string('x', 197)}... (1003 bytes)", _run.OutLines);
    }

    // Requirement: Summary and result

    [Fact]
    public void Correct_output()
    {
        int exitCode = _run.Check(Input, Sorted);

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal(_run.InputPath, _run.Field("Input"));
        Assert.Equal(_run.SortedPath, _run.Field("Output"));
        Assert.Equal(ByteSize.Describe(Input.Length), _run.Field("Input", "Size"));
        Assert.Equal(ByteSize.Describe(Sorted.Length), _run.Field("Output", "Size"));
        Assert.Equal("5", _run.Field("Input", "Lines"));
        Assert.Equal("5", _run.Field("Output", "Lines"));
        Assert.Equal("no", _run.Field("Input", "Byte order mark"));
        Assert.Equal("no", _run.Field("Output", "Byte order mark"));
        Assert.Equal("lf (from the input)", _run.Field("Line ending"));
        Assert.Equal("OK", _run.Field("Order"));
        Assert.Equal("OK", _run.Field("Same lines"));
        Assert.Equal("0 pairs", _run.Field("Equal keys, different bytes"));
        Assert.EndsWith(" s", _run.Field("Elapsed"));
        Assert.EndsWith(" MiB/s", _run.Field("Throughput"));
        Assert.EndsWith(" MiB", _run.Field("Peak working set"));

        // The result is the last line.
        Assert.Equal("Result:           CORRECT", _run.OutLines.Last(line => line.Length > 0));
    }

    [Fact]
    public void Both_checks_fail()
    {
        int exitCode = _run.Check(Input, "415. Apple\n1. Apple\n2. Banana is yellow\n32. Cherry is the best\n");

        // Each check on its own line: the order is wrong, and a line is missing.
        Assert.Equal(ExitCode.NotCorrect, exitCode);
        Assert.Equal("FAILED, 1 violation, the first at line 2", _run.Field("Order"));
        Assert.Equal("FAILED, the output has 1 line fewer", _run.Field("Same lines"));
        Assert.Equal("Result:           NOT CORRECT", _run.OutLines.Last(line => line.Length > 0));
    }

    // Requirement: Settings and progress

    [Fact]
    public void Settings_before_the_check()
    {
        _run.Check(Input, Sorted);

        Assert.Equal(_run.SortedPath, _run.SettingsField("Checking"));
        Assert.Equal(_run.InputPath, _run.SettingsField("Against input"));
        Assert.Equal("auto", _run.SettingsField("Line ending"));
        string threads = ScanSettings.DefaultThreads.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(threads, _run.SettingsField("Threads"));

        // The settings go to the error stream, before the progress.
        int settingsEnd = _run.Error.IndexOf("Threads:", StringComparison.Ordinal);
        int firstProgress = _run.Error.IndexOf(" left", StringComparison.Ordinal);
        Assert.True(settingsEnd < firstProgress);
        Assert.DoesNotContain("Checking:", _run.Out);
    }

    [Fact]
    public void Settings_show_the_given_line_ending_and_full_paths()
    {
        File.WriteAllText(_run.InputPath, "2. Banana\r\n1. Apple\r\n");
        File.WriteAllText(_run.SortedPath, "1. Apple\r\n2. Banana\r\n");
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, _run.InputPath);

        int exitCode = _run.Run(
            "--input", relative, "--sorted", _run.SortedPath, "--line-ending", "crlf", "--threads", "3");

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal("3", _run.SettingsField("Threads"));
        Assert.Equal(Path.GetFullPath(relative), _run.SettingsField("Against input"));
        Assert.Equal("crlf", _run.SettingsField("Line ending"));
        Assert.Equal("crlf (given)", _run.Field("Line ending"));
    }

    [Fact]
    public void Redirected_error_stream()
    {
        _run.Console = new FakeConsole { IsErrorRedirected = true };

        int exitCode = _run.Check(Input, Sorted);

        // A log file gets the settings, and no progress updates.
        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal("auto", _run.SettingsField("Line ending"));
        Assert.DoesNotContain(" left", _run.Error);
        Assert.DoesNotContain("MiB/s", _run.Error);
        Assert.DoesNotContain("\r", _run.Error.Replace(Environment.NewLine, "", StringComparison.Ordinal));
    }

    [Fact]
    public void Progress_is_shown_on_the_error_stream()
    {
        _run.Check(Input, Sorted);

        Assert.Contains(" left", _run.Error);
        Assert.Contains("MiB/s", _run.Error);
        Assert.DoesNotContain(" left", _run.Out);
    }

    [Fact]
    public void Findings_on_standard_output()
    {
        _run.Check("2. Banana\n1. Apple\n", "2. Banana\n1. Apple\n");

        // Standard output holds the violation and the summary, and nothing else; the error stream holds the
        // settings and the progress.
        Assert.StartsWith("Order violation at output line 2", _run.Out);
        Assert.Equal("NOT CORRECT", _run.Field("Result"));
        Assert.DoesNotContain("Checking:", _run.Out);
        Assert.DoesNotContain(" left", _run.Out);
        Assert.Equal("auto", _run.SettingsField("Line ending"));
        Assert.DoesNotContain("Order violation", _run.Error);
        Assert.DoesNotContain("Result:", _run.Error);
    }

    // Requirement: Parallelism

    [Fact]
    public void Result_does_not_depend_on_the_number_of_threads()
    {
        // A file of a few thousand lines, most of them out of order, with lines of equal keys among them.
        Random random = new(1);
        byte[] file = Utf8(string.Concat(Enumerable.Range(0, 5000).Select(
            _ => $"{new string('0', random.Next(3))}{random.Next(50)}. Line {random.Next(20)}{'\n'}")));

        string[] one = ReportWithThreads(file, "1");

        Assert.Equal(10, one.Count(line => line.StartsWith("Order violation", StringComparison.Ordinal)));
        Assert.Contains(one, line => line.StartsWith("Order:            FAILED", StringComparison.Ordinal));
        Assert.Equal(one, ReportWithThreads(file, "2"));
        Assert.Equal(one, ReportWithThreads(file, "8"));
        Assert.Equal(one, ReportWithThreads(file, "64"));
    }

    // Checks a file against itself in chunks of a few kilobytes, so that there are many chunks for the threads to
    // share, and returns what was printed without the measurements of the run.
    private string[] ReportWithThreads(byte[] file, string threads)
    {
        _run.Console = new FakeConsole();
        ScanSettings small = new() { ChunkSize = 4096 };
        int exitCode = _run.Check(file, file, small, TestContext.Current.CancellationToken, "--threads", threads);

        Assert.Equal(ExitCode.NotCorrect, exitCode);
        return [.. _run.OutLines.Where(line => !line.StartsWith("Elapsed", StringComparison.Ordinal)
            && !line.StartsWith("Throughput", StringComparison.Ordinal)
            && !line.StartsWith("Peak", StringComparison.Ordinal))];
    }

    // Both scans end early (D7)

    [Fact]
    public void An_invalid_line_in_each_file_is_reported_for_both_the_input_first()
    {
        int exitCode = _run.Check("1. Apple\nBanana\n", "1. Apple\n\n2. Banana\n");

        AssertInvalidFile(exitCode);
        int input = _run.Out.IndexOf("Invalid line in the input", StringComparison.Ordinal);
        int output = _run.Out.IndexOf("Invalid line in the output", StringComparison.Ordinal);
        Assert.True(input >= 0 && input < output);
        Assert.Single(_run.OutLines, line => line.StartsWith("Result:", StringComparison.Ordinal));
    }

    [Fact]
    public void An_invalid_line_in_one_file_and_a_line_above_the_limit_in_the_other()
    {
        string tooLong = "2. " + new string('x', 30) + "\n";

        ScanSettings scan = new() { MaxLineLength = 20 };
        int exitCode = _run.Check(
            "1. Apple\n" + tooLong, "1. Apple\nBanana\n", scan, TestContext.Current.CancellationToken);

        // The invalid line goes to standard output with its result, the line above the limit to the error stream,
        // and the exit code agrees with the printed result.
        AssertInvalidFile(exitCode);
        Assert.Contains("Invalid line in the output", _run.Out);
        Assert.Contains($"Cannot check '{_run.InputPath}'", _run.Error);
        Assert.Contains("longer than 20 bytes", _run.Error);
    }

    [Fact]
    public void A_line_above_the_limit_in_each_file()
    {
        string tooLong = "2. " + new string('x', 30) + "\n";

        ScanSettings scan = new() { MaxLineLength = 20 };
        int exitCode = _run.Check(
            "1. Apple\n" + tooLong, "1. Apple\n" + tooLong, scan, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains($"Cannot check '{_run.InputPath}'", _run.Error);
        Assert.Contains($"Cannot check '{_run.SortedPath}'", _run.Error);
        Assert.Empty(_run.Out);
    }

    // A console that cannot be written to (D8)

    [Fact]
    public void Settings_that_cannot_be_printed_stop_the_check()
    {
        _run.Console = new FakeConsole
        {
            OnErrorWrite = text =>
            {
                if (text.StartsWith("Checking:", StringComparison.Ordinal))
                {
                    throw new IOException("The stream is broken.");
                }
            },
        };

        int exitCode = _run.Check(Input, Sorted);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("Cannot write to the console, so the check was stopped: The stream is broken.", _run.Error);
        Assert.Empty(_run.Out);
    }

    [Fact]
    public void A_progress_update_that_cannot_be_printed_stops_the_check()
    {
        bool failed = false;
        _run.Console = new FakeConsole
        {
            OnErrorWrite = text =>
            {
                if (!failed && text.Contains(" left", StringComparison.Ordinal))
                {
                    failed = true;
                    throw new IOException("The stream is broken.");
                }
            },
        };

        int exitCode = _run.Check(Input, Sorted);

        Assert.True(failed);
        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Contains("Cannot write to the console, so the check was stopped: The stream is broken.", _run.Error);
        Assert.DoesNotContain("   at ", _run.Error);
        Assert.Empty(_run.Out);
    }

    [Theory]
    [InlineData(Sorted, ExitCode.Success)]
    [InlineData("415. Apple\n1. Apple\n", ExitCode.NotCorrect)]
    [InlineData("1. Apple\nApple\n", ExitCode.InvalidFile)]
    public void A_report_that_cannot_be_printed_keeps_the_exit_code(string sorted, int expected)
    {
        _run.Console = new FakeConsole
        {
            // Standard output fails like a stream redirected to a file on a full disk: at its first write, which
            // is a violation, the report of an invalid line or the summary.
            OnOutWrite = _ => throw new IOException("There is not enough space on the disk."),
        };

        int exitCode = _run.Check(Input, sorted);

        // The exit code still carries the verdict, and the error stream says, once, that the report was lost.
        Assert.Equal(expected, exitCode);
        Assert.Empty(_run.Out);
        const string Lost = "its report could not be printed: There is not enough space on the disk.";
        Assert.Single(_run.Error.Split('\n'), line => line.Contains(Lost, StringComparison.Ordinal));
    }

    [Fact]
    public void A_report_that_was_printed_is_not_reported_as_lost()
    {
        _run.Check(Input, Sorted);

        Assert.DoesNotContain("could not be printed", _run.Error);
    }

    [Fact]
    public void A_broken_error_stream_still_ends_with_an_exit_code()
    {
        // Every write to the error stream fails, the message about the failure included.
        _run.Console = new FakeConsole { OnErrorWrite = _ => throw new IOException("The stream is broken.") };

        int exitCode = _run.Check(Input, Sorted);

        Assert.Equal(ExitCode.Failure, exitCode);
        Assert.Empty(_run.Out);
    }

    [Fact]
    public void A_progress_line_that_cannot_be_removed_keeps_the_summary()
    {
        _run.Console = new FakeConsole
        {
            // Fails the write that blanks the progress line: a carriage return followed by spaces only.
            OnErrorWrite = text =>
            {
                if (text.Length > 1 && text[0] == '\r' && text[1..].All(c => c == ' '))
                {
                    throw new IOException("The terminal has gone.");
                }
            },
        };

        int exitCode = _run.Check(Input, Sorted);

        Assert.Equal(ExitCode.Success, exitCode);
        Assert.Equal("CORRECT", _run.Field("Result"));
    }
}
