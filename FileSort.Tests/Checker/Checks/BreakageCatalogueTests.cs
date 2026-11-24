using System.Text;

using FileSort.Checker.Checks;
using FileSort.Checker.Checks.Order;
using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Lines;
using FileSort.Generator.Content;
using FileSort.Tests.Format;
using FileSort.Tests.Generator;

using CommonLineEnding = FileSort.Common.LineEnding;

namespace FileSort.Tests.Checker.Checks;

/// <summary>
/// The catalogue of breakages: a correct pair is broken in one way at a time, each a kind of wrong output that a
/// sorter can plausibly produce, and the checker must give the stated result for every one. The pairs are made
/// once and never changed: a test breaks its own copy of the sorted lines.
/// </summary>
public class BreakageCatalogueTests
{
    private static readonly byte[] Mark = [0xEF, 0xBB, 0xBF];

    private static readonly Lazy<SortedPair> PlainPair =
        new(() => SortedPair.Generated(EdgeCases.None, CommonLineEnding.Lf));
    private static readonly Lazy<SortedPair> EdgeCasePair =
        new(() => SortedPair.Generated(EdgeCases.All, CommonLineEnding.Lf));
    private static readonly Lazy<SortedPair> CrLfPair =
        new(() => SortedPair.Generated(EdgeCases.None, CommonLineEnding.CrLf));

    private static SortedPair Plain => PlainPair.Value;

    private static SortedPair EdgeCase => EdgeCasePair.Value;

    private static CheckResult Check(SortedPair pair, byte[] output) => TestCheck.Run(pair.Input, output);

    private static CheckResult Check(SortedPair pair, IEnumerable<NaiveLine> lines) => Check(pair, pair.Join(lines));

    private static bool SameBytes(NaiveLine x, NaiveLine y) => x.ToBytes().AsSpan().SequenceEqual(y.ToBytes());

    private static bool SameString(NaiveLine x, NaiveLine y) => x.Text.AsSpan().SequenceEqual(y.Text);

    // The first place from the middle of the file on where two neighbouring lines fit the description.
    private static int FindNeighbours(List<NaiveLine> lines, Func<NaiveLine, NaiveLine, bool> fits)
    {
        for (int i = lines.Count / 2; i < lines.Count - 1; i++)
        {
            if (fits(lines[i], lines[i + 1]))
            {
                return i;
            }
        }

        throw new InvalidOperationException("The pair has no such neighbouring lines.");
    }

    private static void AssertNotCorrectWithTheOrderRight(CheckResult result)
    {
        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.True(result.InOrder);
        Assert.False(result.SameLines);
    }

    private static void AssertNotCorrectWithTheSameLines(CheckResult result)
    {
        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.False(result.InOrder);
        Assert.True(result.SameLines);
    }

    [Fact]
    public void The_pairs_are_correct_before_they_are_broken()
    {
        Assert.Equal(CheckOutcome.Correct, Check(Plain, Plain.Output).Outcome);
        Assert.Equal(CheckOutcome.Correct, Check(EdgeCase, EdgeCase.Output).Outcome);
        Assert.Equal(CheckOutcome.Correct, Check(CrLfPair.Value, CrLfPair.Value.Output).Outcome);
    }

    // The lines differ

    [Fact]
    public void One_line_removed()
    {
        List<NaiveLine> lines = [.. Plain.Lines];
        lines.RemoveAt(lines.Count / 2);

        CheckResult result = Check(Plain, lines);

        AssertNotCorrectWithTheOrderRight(result);
        Assert.Equal(result.Input.Digest.Lines - 1, result.Output.Digest.Lines);
    }

    [Fact]
    public void One_line_written_twice()
    {
        List<NaiveLine> lines = [.. Plain.Lines];
        lines.Insert(lines.Count / 2, lines[lines.Count / 2]);

        CheckResult result = Check(Plain, lines);

        AssertNotCorrectWithTheOrderRight(result);
        Assert.Equal(result.Input.Digest.Lines + 1, result.Output.Digest.Lines);
    }

    [Fact]
    public void One_line_replaced_by_a_copy_of_another()
    {
        List<NaiveLine> lines = [.. Plain.Lines];
        int at = FindNeighbours(lines, (x, y) => !SameBytes(x, y));
        lines[at + 1] = lines[at];

        CheckResult result = Check(Plain, lines);

        AssertNotCorrectWithTheOrderRight(result);
        Assert.Equal(result.Input.Digest.Lines, result.Output.Digest.Lines);
    }

    [Fact]
    public void A_pair_of_identical_lines_replaced_by_a_pair_of_another_line()
    {
        List<NaiveLine> lines = [.. Plain.Lines];

        // Three lines in a row: one, then two identical ones that differ from it. The two become copies of the first.
        int at = Enumerable.Range(lines.Count / 2, lines.Count / 4)
            .First(i => !SameBytes(lines[i], lines[i + 1]) && SameBytes(lines[i + 1], lines[i + 2]));
        lines[at + 1] = lines[at];
        lines[at + 2] = lines[at];

        CheckResult result = Check(Plain, lines);

        // The count of lines is the same, and an exclusive or of line hashes would be the same too.
        AssertNotCorrectWithTheOrderRight(result);
        Assert.Equal(result.Input.Digest.Lines, result.Output.Digest.Lines);
    }

    [Fact]
    public void One_byte_of_a_String_changed_with_the_order_kept()
    {
        List<NaiveLine> lines = [.. Plain.Lines];
        IEnumerable<int> thirdQuarter = Enumerable.Range(lines.Count / 2, lines.Count / 4);
        int at = thirdQuarter.First(i => KeepsOrderWithItsLastLetterRaised(lines, i));
        lines[at] = WithItsLastLetterRaised(lines[at]);

        CheckResult result = Check(Plain, lines);

        AssertNotCorrectWithTheOrderRight(result);
        Assert.Equal(result.Input.Digest.Lines, result.Output.Digest.Lines);
        Assert.Equal(result.Input.Digest.Bytes, result.Output.Digest.Bytes);
    }

    private static NaiveLine WithItsLastLetterRaised(NaiveLine line)
    {
        byte[] text = [.. line.Text];
        text[^1]++;
        return line with { Text = text };
    }

    private static bool KeepsOrderWithItsLastLetterRaised(List<NaiveLine> lines, int at)
    {
        if (lines[at].Text.Length == 0 || lines[at].Text[^1] is < (byte)'a' or >= (byte)'z')
        {
            return false;
        }

        return NaiveSort.IsSorted([lines[at - 1], WithItsLastLetterRaised(lines[at]), lines[at + 1]]);
    }

    [Fact]
    public void A_leading_zero_of_a_Number_removed()
    {
        List<NaiveLine> lines = [.. EdgeCase.Lines];
        int at = lines.FindIndex(lines.Count / 2, line => line.NumberText.Length > 1 && line.NumberText[0] == '0');
        lines[at] = lines[at] with { NumberText = lines[at].NumberText[1..] };

        CheckResult result = Check(EdgeCase, lines);

        // The key of the line is what it was, so the order is right; its bytes are not.
        AssertNotCorrectWithTheOrderRight(result);
        Assert.Equal(result.Input.Digest.Bytes - 1, result.Output.Digest.Bytes);
    }

    [Fact]
    public void A_space_at_the_end_of_a_String_removed()
    {
        List<NaiveLine> lines = [.. Plain.Lines];
        int at = lines.FindIndex(line => line.Text.Length > 0 && line.Text[^1] == (byte)' ');
        lines[at] = lines[at] with { Text = lines[at].Text[..^1] };

        CheckResult result = Check(Plain, lines);

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.False(result.SameLines);
    }

    [Fact]
    public void Two_lines_joined()
    {
        List<byte[]> lines = Plain.RawLines();
        int at = lines.Count / 2;

        // The terminator between two lines is lost: one line, valid, that the input does not have.
        lines[at] = [.. lines[at], .. lines[at + 1]];
        lines.RemoveAt(at + 1);

        CheckResult result = Check(Plain, Plain.JoinRaw(lines));

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.False(result.SameLines);
        Assert.Equal(result.Input.Digest.Lines - 1, result.Output.Digest.Lines);
    }

    [Fact]
    public void The_file_cut_inside_a_String_between_two_characters()
    {
        List<byte[]> lines = Plain.RawLines();
        int at = lines.FindIndex(lines.Count / 2, line => line.Length > 16 && Ascii.IsValid(line));

        // Everything after the cut is gone, and the last line lost its last three letters; what is left of it is
        // a valid line: a plain Number has at most ten digits.
        byte[] output = [.. Plain.JoinRaw(lines.Take(at)), .. lines[at][..^3]];

        CheckResult result = Check(Plain, output);

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.False(result.SameLines);
        Assert.Equal(at + 1, result.Output.Digest.Lines);
    }

    // The order is wrong

    [Fact]
    public void Two_neighbouring_lines_with_different_Strings_swapped()
    {
        List<NaiveLine> lines = [.. Plain.Lines];
        int at = FindNeighbours(lines, (x, y) => !SameString(x, y));
        (lines[at], lines[at + 1]) = (lines[at + 1], lines[at]);
        ScanFeed feed = new();

        CheckResult result = TestCheck.Run(Plain.Input, Plain.Join(lines), feed: feed);

        AssertNotCorrectWithTheSameLines(result);
        Assert.Equal(1, result.Output.Order.Violations);
        Assert.Equal(at + 2, result.Output.Order.FirstViolationLine);
        Assert.True(feed.TryTakeViolation(out OrderViolation? violation));
        Assert.Equal(OrderViolationKind.StringsOutOfOrder, violation.Kind);
    }

    [Fact]
    public void Two_neighbouring_lines_with_equal_Strings_and_different_Numbers_swapped()
    {
        List<NaiveLine> lines = [.. Plain.Lines];
        int at = FindNeighbours(lines, (x, y) => SameString(x, y) && x.Number != y.Number);
        (lines[at], lines[at + 1]) = (lines[at + 1], lines[at]);
        ScanFeed feed = new();

        CheckResult result = TestCheck.Run(Plain.Input, Plain.Join(lines), feed: feed);

        AssertNotCorrectWithTheSameLines(result);
        Assert.Equal(1, result.Output.Order.Violations);
        Assert.True(feed.TryTakeViolation(out OrderViolation? violation));
        Assert.Equal(OrderViolationKind.NumbersOutOfOrder, violation.Kind);
    }

    // The lines of one String with their Numbers ordered as text, as a sorter that compares digits as text would.
    private static IEnumerable<NaiveLine> NumbersAsText(IEnumerable<NaiveLine> sameString)
    {
        return sameString.OrderBy(line => line.NumberText, StringComparer.Ordinal);
    }

    [Fact]
    public void Numbers_ordered_as_text_within_equal_Strings()
    {
        // The Strings stay in their order; within the lines of one String the Numbers go as text: 10 before 9.
        IEnumerable<NaiveLine> lines = Plain.Lines
            .GroupBy(line => line.TextAsString)
            .SelectMany(NumbersAsText);

        CheckResult result = Check(Plain, lines);

        AssertNotCorrectWithTheSameLines(result);
        Assert.True(result.Output.Order.Violations > 100);
    }

    [Fact]
    public void Strings_ordered_without_regard_to_letter_case()
    {
        // Default content has the same order with and without letter case, so this is the pair with edge cases.
        IEnumerable<NaiveLine> lines = EdgeCase.Lines
            .OrderBy(line => line.TextAsString, StringComparer.OrdinalIgnoreCase)
            .ThenBy(line => line.Number);

        CheckResult result = Check(EdgeCase, lines);

        AssertNotCorrectWithTheSameLines(result);
    }

    [Fact]
    public void Strings_ordered_by_UTF16_code_units()
    {
        // What a sorter does that decodes the lines to strings and compares them "ordinally".
        IEnumerable<NaiveLine> lines = EdgeCase.Lines
            .OrderBy(line => line.TextAsString, StringComparer.Ordinal)
            .ThenBy(line => line.Number);

        CheckResult result = Check(EdgeCase, lines);

        AssertNotCorrectWithTheSameLines(result);
    }

    [Fact]
    public void The_wrong_comparisons_give_the_right_order_on_default_content()
    {
        // Why the two tests above need the edge cases: without them, and without the hand-written lines, both
        // wrong orders are the right one, and nothing would be caught.
        SortedPair pair = new(
            Generator.TestContent.Generate(Generator.TestContent.Options(seed: 11), 256 * 1024), "\n");
        IEnumerable<NaiveLine> lines = pair.Lines
            .OrderBy(line => line.TextAsString, StringComparer.OrdinalIgnoreCase)
            .ThenBy(line => line.Number);

        Assert.Equal(CheckOutcome.Correct, Check(pair, lines).Outcome);
    }

    // A file that is not valid

    [Fact]
    public void The_file_cut_inside_a_character_of_several_bytes()
    {
        List<byte[]> lines = EdgeCase.RawLines();
        int at = lines.FindIndex(lines.Count / 2, line => !Ascii.IsValid(line));
        int firstByte = Array.FindIndex(lines[at], b => b >= 0x80);

        // The cut falls after the first byte of the character.
        byte[] output = [.. EdgeCase.JoinRaw(lines.Take(at)), .. lines[at][..(firstByte + 1)]];

        CheckResult result = Check(EdgeCase, output);

        Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
        Assert.Equal(at + 1, result.Output.InvalidLine!.LineNumber);
        Assert.Equal(LineFault.IllFormedString, result.Output.InvalidLine.Fault);
    }

    [Fact]
    public void The_file_cut_inside_a_Number()
    {
        List<byte[]> lines = Plain.RawLines();
        int at = lines.FindIndex(lines.Count / 2, line => line[1] is >= (byte)'0' and <= (byte)'9');
        byte[] output = [.. Plain.JoinRaw(lines.Take(at)), lines[at][0]];

        CheckResult result = Check(Plain, output);

        Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
        Assert.Equal(at + 1, result.Output.InvalidLine!.LineNumber);
        Assert.Equal(LineFault.NoSeparator, result.Output.InvalidLine.Fault);
    }

    [Fact]
    public void An_empty_line_inserted()
    {
        List<byte[]> lines = Plain.RawLines();
        int at = lines.Count / 2;
        lines.Insert(at, []);

        CheckResult result = Check(Plain, Plain.JoinRaw(lines));

        Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
        Assert.Equal(at + 1, result.Output.InvalidLine!.LineNumber);
        Assert.Equal(LineFault.Empty, result.Output.InvalidLine.Fault);
    }

    [Fact]
    public void The_byte_order_mark_of_the_input_kept_in_front_of_a_line_in_the_middle()
    {
        List<byte[]> lines = Plain.RawLines();
        int at = lines.Count / 2;
        lines[at] = [.. Mark, .. lines[at]];

        CheckResult result = TestCheck.Run([.. Mark, .. Plain.Input], Plain.JoinRaw(lines));

        Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
        Assert.Equal(at + 1, result.Output.InvalidLine!.LineNumber);
        Assert.Equal(LineFault.NoNumber, result.Output.InvalidLine.Fault);
    }

    // The other line ending

    [Fact]
    public void An_output_with_carriage_returns_for_an_input_with_line_feeds()
    {
        byte[] output = NaiveSort.Join(Plain.Lines, "\r\n");

        CheckResult result = Check(Plain, output);

        // Every line is valid, with a carriage return at the end of its String; only the lines differ.
        AssertNotCorrectWithTheOrderRight(result);
        Assert.True(result.LineEndingsSeemToDiffer);
        Assert.Equal(result.Input.Digest.Lines, result.Output.Digest.Lines);
    }

    [Fact]
    public void An_output_with_line_feeds_for_an_input_with_carriage_returns()
    {
        SortedPair pair = CrLfPair.Value;
        byte[] output = NaiveSort.Join(pair.Lines, "\n");

        CheckResult result = Check(pair, output);

        Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
        Assert.Equal(1, result.Output.InvalidLine!.LineNumber);
        Assert.Equal(LineFault.BareLineFeed, result.Output.InvalidLine.Fault);
    }

    // Differences that leave the output correct

    [Fact]
    public void Lines_with_equal_keys_and_different_bytes_swapped()
    {
        List<NaiveLine> lines = [.. Plain.Lines];
        int at = lines.FindIndex(line => Encoding.ASCII.GetString(line.ToBytes()) == "007. Apple");
        Assert.Equal("7. Apple", Encoding.ASCII.GetString(lines[at + 1].ToBytes()));
        (lines[at], lines[at + 1]) = (lines[at + 1], lines[at]);

        CheckResult result = Check(Plain, lines);

        Assert.Equal(CheckOutcome.Correct, result.Outcome);
        Assert.True(result.Output.Order.EqualKeyPairs >= 1);
    }

    [Fact]
    public void The_terminator_of_the_last_line_added_or_removed()
    {
        byte[] output = Plain.Output;

        Assert.Equal(CheckOutcome.Correct, Check(Plain, output[..^1]).Outcome);
        Assert.Equal(CheckOutcome.Correct, TestCheck.Run(Plain.Input[..^1], output).Outcome);
    }

    [Fact]
    public void A_byte_order_mark_added_or_removed_at_the_start()
    {
        byte[] output = Plain.Output;

        CheckResult added = Check(Plain, [.. Mark, .. output]);
        CheckResult removed = TestCheck.Run([.. Mark, .. Plain.Input], output);

        Assert.Equal(CheckOutcome.Correct, added.Outcome);
        Assert.True(added.Output.HasByteOrderMark);
        Assert.Equal(CheckOutcome.Correct, removed.Outcome);
        Assert.True(removed.Input.HasByteOrderMark);
    }
}
