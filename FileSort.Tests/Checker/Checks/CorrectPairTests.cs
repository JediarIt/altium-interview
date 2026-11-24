using FileSort.Checker.Checks;
using FileSort.Checker.Checks.Scans;
using FileSort.Checker.Lines;
using FileSort.Generator.Content;

using CommonLineEnding = FileSort.Common.LineEnding;

namespace FileSort.Tests.Checker.Checks;

/// <summary>
/// Correct pairs: a file made by the generator, with hand-written lines that the generator writes rarely or not at
/// all, and its copy sorted by the naive sort of the tests. The checker must find every one of them correct.
/// </summary>
public class CorrectPairTests
{
    [Theory]
    [InlineData(false, CommonLineEnding.Lf)]
    [InlineData(false, CommonLineEnding.CrLf)]
    [InlineData(true, CommonLineEnding.Lf)]
    [InlineData(true, CommonLineEnding.CrLf)]
    public void A_generated_file_and_its_naively_sorted_copy_are_correct(bool edgeCases, CommonLineEnding lineEnding)
    {
        SortedPair pair = SortedPair.Generated(edgeCases ? EdgeCases.All : EdgeCases.None, lineEnding);

        CheckResult result = TestCheck.Run(pair.Input, pair.Output);

        Assert.Equal(CheckOutcome.Correct, result.Outcome);
        Assert.Equal(lineEnding == CommonLineEnding.CrLf ? LineEnding.CrLf : LineEnding.Lf, result.LineEnding);
        Assert.Equal(pair.Lines.Count, result.Input.Digest.Lines);
        Assert.Equal(pair.Lines.Count, result.Output.Digest.Lines);
        Assert.True(pair.Lines.Count > 30_000);
        Assert.Equal(0, result.Output.Order.Violations);

        // The hand-written lines 007. Apple and 7. Apple stand side by side in the sorted file.
        Assert.True(result.Output.Order.EqualKeyPairs >= 1);
    }

    [Theory]
    [InlineData(64, 1)]
    [InlineData(1000, 2)]
    [InlineData(4096, 3)]
    [InlineData(65536, 16)]
    [InlineData(4 * 1024 * 1024, 1)]
    public void The_result_does_not_depend_on_the_size_of_a_chunk_or_on_the_number_of_threads(
        int chunkSize, int threads)
    {
        SortedPair pair = SortedPair.Generated(EdgeCases.All, CommonLineEnding.CrLf, seed: 12);
        CheckResult expected = TestCheck.Run(pair.Input, pair.Output);

        // With the small sizes the files take hundreds or thousands of chunks, checked on several threads.
        CheckResult actual = TestCheck.Run(
            pair.Input, pair.Output, settings: new ScanSettings { ChunkSize = chunkSize, Threads = threads });

        Assert.Equal(CheckOutcome.Correct, actual.Outcome);
        Assert.Equal(expected.Input.Digest, actual.Input.Digest);
        Assert.Equal(expected.Output.Digest, actual.Output.Digest);
        Assert.Equal(expected.Output.Order, actual.Output.Order);
        Assert.Equal(expected.Output.TerminatedLines, actual.Output.TerminatedLines);
    }

    [Fact]
    public void The_input_itself_is_not_a_correct_output()
    {
        SortedPair pair = SortedPair.Generated(EdgeCases.None, CommonLineEnding.Lf);

        CheckResult result = TestCheck.Run(pair.Input, pair.Input);

        // The same lines, in the order of the input, which is not sorted.
        Assert.Equal(CheckOutcome.NotCorrect, result.Outcome);
        Assert.True(result.SameLines);
        Assert.True(result.Output.Order.Violations > 10_000);
        Assert.Equal(ScanEnd.Completed, result.Output.End);
    }
}
