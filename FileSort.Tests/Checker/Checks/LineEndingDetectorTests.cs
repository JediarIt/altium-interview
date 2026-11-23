using System.Text;

using FileSort.Checker.Checks;
using FileSort.Checker.Lines;

namespace FileSort.Tests.Checker.Checks;

public class LineEndingDetectorTests
{
    private const long NoLimit = long.MaxValue - 1;

    private static LineEnding Detect(byte[] file, long searchLimit = NoLimit)
    {
        using MemoryStream stream = new(file);
        return LineEndingDetector.Detect(stream, searchLimit);
    }

    private static LineEnding Detect(string file) => Detect(Encoding.UTF8.GetBytes(file));

    [Theory]
    [InlineData("2. Banana\n1. Apple\n")]
    [InlineData("2. Banana\n1. Apple\r\n")]
    [InlineData("1. Apple")]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("1. A\rB\n2. C\r\n")]
    [InlineData("\uFEFF1. Apple\n")]
    public void A_line_feed_without_a_carriage_return_before_it_or_no_line_feed_at_all_means_lf(string file)
    {
        Assert.Equal(LineEnding.Lf, Detect(file));
    }

    [Theory]
    [InlineData("2. Banana\r\n1. Apple\r\n")]
    [InlineData("1. Apple\r\n2. Banana\n")]
    [InlineData("\r\n")]
    [InlineData("1. Apple\r\r\n")]
    [InlineData("\uFEFF1. Apple\r\n")]
    public void A_carriage_return_before_the_first_line_feed_means_crlf(string file)
    {
        Assert.Equal(LineEnding.CrLf, Detect(file));
    }

    [Theory]
    [InlineData(65535)]
    [InlineData(65536)]
    [InlineData(65537)]
    [InlineData(200000)]
    public void A_first_line_longer_than_one_read_is_searched_to_its_end(int lineFeedAt)
    {
        // The search reads 64 KiB at a time: the carriage return and the line feed fall on both sides of that edge.
        byte[] crlf = Encoding.ASCII.GetBytes("1. " + new string('x', lineFeedAt - 4) + "\r\n2. Banana\n");
        byte[] lf = Encoding.ASCII.GetBytes("1. " + new string('x', lineFeedAt - 3) + "\n2. Banana\r\n");

        Assert.Equal((byte)'\n', crlf[lineFeedAt]);
        Assert.Equal((byte)'\n', lf[lineFeedAt]);
        Assert.Equal(LineEnding.CrLf, Detect(crlf));
        Assert.Equal(LineEnding.Lf, Detect(lf));
    }

    [Fact]
    public void The_search_ends_at_its_limit()
    {
        // A first line far longer than the longest line that is read: the search gives up, and the scan reports
        // the line.
        byte[] file = Encoding.ASCII.GetBytes("1. " + new string('x', 300000) + "\r\n");

        Assert.Equal(LineEnding.CrLf, Detect(file));
        Assert.Equal(LineEnding.Lf, Detect(file, searchLimit: 1000));
    }

    [Fact]
    public void The_stream_is_back_at_its_start_afterwards()
    {
        using MemoryStream stream = new(Encoding.ASCII.GetBytes("1. Apple\r\n2. Banana\r\n"));
        stream.Position = 5;

        LineEnding detected = LineEndingDetector.Detect(stream, NoLimit);

        // The search starts at the start of the file, wherever the stream was.
        Assert.Equal(LineEnding.CrLf, detected);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void The_stream_is_back_at_its_start_after_a_failed_read_too()
    {
        using FailingStream stream = new(Encoding.ASCII.GetBytes("1. Apple\r\n"), failAt: 0);

        Assert.Throws<IOException>(() => LineEndingDetector.Detect(stream, NoLimit));
        Assert.Equal(0, stream.Position);
    }
}
