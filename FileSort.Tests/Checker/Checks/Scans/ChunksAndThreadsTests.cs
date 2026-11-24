using FileSort.Checker.Checks;
using FileSort.Checker.Checks.Order;
using FileSort.Checker.Checks.Scans;
using FileSort.Generator.Content;
using FileSort.Tests.Format;

using CommonLineEnding = FileSort.Common.LineEnding;

namespace FileSort.Tests.Checker.Checks.Scans;

/// <summary>
/// A file is checked in chunks, on several threads. What the check finds must not depend on where the chunks are
/// cut or on how many threads check them: the counts, the line numbers, the first violations and the first line
/// that is not valid are those of the file. The expected values come from the naive comparison of the tests, not
/// from a run of the checker with other settings.
/// </summary>
public class ChunksAndThreadsTests
{
    private static readonly Lazy<SortedPair> Pair =
        new(() => SortedPair.Generated(EdgeCases.All, CommonLineEnding.Lf, seed: 13));

    // From chunks of a few lines, which put thousands of pairs of neighbours across two chunks, to one chunk for
    // the whole file; from one thread to more threads than the machine may have.
    public static TheoryData<int, int> Settings => new()
    {
        { 64, 1 }, { 64, 4 }, { 100, 2 }, { 1000, 3 }, { 4096, 1 }, { 4096, 8 }, { 65536, 32 }, { 4 * 1024 * 1024, 1 },
    };

    private static (CheckResult Result, List<OrderViolation> Reported) Check(
        byte[] input, byte[] output, int chunkSize, int threads)
    {
        ScanFeed feed = new();
        CheckResult result = TestCheck.Run(
            input, output, settings: new ScanSettings { ChunkSize = chunkSize, Threads = threads }, feed: feed);

        List<OrderViolation> reported = [];
        while (feed.TryTakeViolation(out OrderViolation? violation))
        {
            reported.Add(violation);
        }

        return (result, reported);
    }

    // As much of a line as a violation keeps of it.
    private static byte[] Start(NaiveLine line) => [.. line.ToBytes().Take(LineSample.MaxBytes)];

    // The lines that come before their predecessor, by the naive comparison: their numbers, from 1.
    private static List<long> NaiveViolations(List<NaiveLine> lines)
    {
        List<long> violations = [];
        for (int i = 1; i < lines.Count; i++)
        {
            if (NaiveLineComparer.Instance.Compare(lines[i - 1], lines[i]) > 0)
            {
                violations.Add(i + 1);
            }
        }

        return violations;
    }

    private static long NaiveEqualKeyPairs(List<NaiveLine> lines)
    {
        long pairs = 0;
        for (int i = 1; i < lines.Count; i++)
        {
            bool equalKeys = NaiveLineComparer.Instance.Compare(lines[i - 1], lines[i]) == 0;
            pairs += equalKeys && lines[i - 1].NumberText != lines[i].NumberText ? 1 : 0;
        }

        return pairs;
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void A_sorted_file_is_in_order_however_it_is_cut(int chunkSize, int threads)
    {
        SortedPair pair = Pair.Value;

        (CheckResult result, List<OrderViolation> reported) = Check(pair.Input, pair.Output, chunkSize, threads);

        Assert.Equal(CheckOutcome.Correct, result.Outcome);
        Assert.Empty(reported);
        Assert.Equal(pair.Lines.Count, result.Output.Digest.Lines);
        Assert.Equal(NaiveEqualKeyPairs(pair.Lines), result.Output.Order.EqualKeyPairs);

        // At least the hand-written pair, 007. Apple and 7. Apple.
        Assert.True(result.Output.Order.EqualKeyPairs >= 1);
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void The_violations_of_an_unsorted_file_are_counted_and_numbered_however_it_is_cut(
        int chunkSize, int threads)
    {
        // The input as its own output: the same lines, tens of thousands of them out of order.
        SortedPair pair = Pair.Value;
        List<NaiveLine> unsorted = NaiveLineParser.ParseValidFile(pair.Input, "\n");
        List<long> expected = NaiveViolations(unsorted);

        (CheckResult result, List<OrderViolation> reported) = Check(pair.Input, pair.Input, chunkSize, threads);

        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.True(result.SameLines);
        Assert.True(expected.Count > 10_000);
        Assert.Equal(expected.Count, result.Output.Order.Violations);
        Assert.Equal(expected[0], result.Output.Order.FirstViolationLine);
        Assert.Equal(NaiveEqualKeyPairs(unsorted), result.Output.Order.EqualKeyPairs);

        // The first ten violations of the file, in the order of their lines, each with its line and the one before.
        Assert.Equal(expected.Take(10), reported.Select(violation => violation.LineNumber));
        Assert.All(reported, violation =>
        {
            Assert.Equal(Start(unsorted[(int)violation.LineNumber - 1]), violation.Line.Start);
            Assert.Equal(Start(unsorted[(int)violation.LineNumber - 2]), violation.Previous.Start);
        });
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void A_few_violations_far_apart_are_all_reported_however_the_file_is_cut(int chunkSize, int threads)
    {
        // Three pairs of neighbours swapped, in three far places of a sorted file: fewer violations than are
        // reported, each in a chunk of its own or across two.
        SortedPair pair = Pair.Value;
        List<NaiveLine> lines = [.. pair.Lines];
        foreach (int at in new[] { 100, lines.Count / 2, lines.Count - 100 })
        {
            int swap = Enumerable.Range(at, 50)
                .First(i => NaiveLineComparer.Instance.Compare(lines[i], lines[i + 1]) < 0);
            (lines[swap], lines[swap + 1]) = (lines[swap + 1], lines[swap]);
        }

        List<long> expected = NaiveViolations(lines);

        (CheckResult result, List<OrderViolation> reported) = Check(pair.Input, pair.Join(lines), chunkSize, threads);

        Assert.Equal(3, expected.Count);
        Assert.Equal(3, result.Output.Order.Violations);
        Assert.Equal(expected, reported.Select(violation => violation.LineNumber));
        Assert.Equal(expected[0], result.Output.Order.FirstViolationLine);
        Assert.True(result.SameLines);
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void The_first_invalid_line_of_a_file_is_found_however_it_is_cut(int chunkSize, int threads)
    {
        // Two lines that are not valid, far apart: the first one in the file is the one reported, with the number
        // and the offset it has in the file, whichever chunk is checked first.
        SortedPair pair = Pair.Value;
        List<byte[]> lines = pair.RawLines();
        int first = lines.Count / 3;
        int second = lines.Count - 5;
        lines[first] = "Apple"u8.ToArray();
        lines[second] = [];
        long offset = lines.Take(first).Sum(line => line.Length + 1L);

        (CheckResult result, _) = Check(pair.Input, pair.JoinRaw(lines), chunkSize, threads);

        Assert.Equal(CheckOutcome.InvalidFile, result.Outcome);
        Assert.Equal(ScanEnd.InvalidLine, result.Output.End);
        Assert.Equal(first + 1, result.Output.InvalidLine!.LineNumber);
        Assert.Equal(offset, result.Output.InvalidLine.Offset);
        Assert.Equal("Apple"u8.ToArray(), result.Output.InvalidLine.Line.Start);

        // The valid lines before it were all counted, and none after it.
        Assert.Equal(first, result.Output.Digest.Lines);
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void A_line_above_the_limit_is_found_with_its_number_however_the_file_is_cut(int chunkSize, int threads)
    {
        SortedPair pair = Pair.Value;
        List<byte[]> lines = pair.RawLines();
        int longest = lines.Max(line => line.Length);
        int at = lines.Count / 2;
        lines[at] = [.. "1. "u8, .. Enumerable.Repeat((byte)'x', longest * 2)];
        long offset = lines.Take(at).Sum(line => line.Length + 1L);
        ScanSettings settings = new() { ChunkSize = chunkSize, Threads = threads, MaxLineLength = longest };

        CheckResult result = TestCheck.Run(pair.Input, pair.JoinRaw(lines), settings: settings);

        Assert.Equal(CheckOutcome.NotChecked, result.Outcome);
        Assert.Equal(ScanEnd.Failed, result.Output.End);
        string expected = $"line {at + 1}, which starts at byte {offset}, is longer than {longest} bytes";
        Assert.Contains(expected, result.Output.Failure);
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void The_digest_of_a_file_does_not_depend_on_how_it_is_cut(int chunkSize, int threads)
    {
        SortedPair pair = Pair.Value;

        (CheckResult result, _) = Check(pair.Input, pair.Output, chunkSize, threads);
        (CheckResult reference, _) = Check(pair.Input, pair.Output, 4 * 1024 * 1024, 1);

        Assert.Equal(reference.Input.Digest, result.Input.Digest);
        Assert.Equal(reference.Output.Digest, result.Output.Digest);
        Assert.Equal(result.Input.Digest, result.Output.Digest);
        Assert.Equal(reference.Output.TerminatedLines, result.Output.TerminatedLines);
        Assert.Equal(reference.Output.CarriageReturnEndings, result.Output.CarriageReturnEndings);
    }
}
