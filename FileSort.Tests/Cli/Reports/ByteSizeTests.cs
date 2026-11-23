using FileSort.Cli.Reports;

namespace FileSort.Tests.Cli.Reports;

public class ByteSizeTests
{
    [Theory]
    [InlineData(0, "0 bytes (0.00 KiB)")]
    [InlineData(1536, "1536 bytes (1.50 KiB)")]
    [InlineData(1048575, "1048575 bytes (1024.00 KiB)")]
    [InlineData(1048576, "1048576 bytes (1.00 MiB)")]
    [InlineData(1073741824, "1073741824 bytes (1.00 GiB)")]
    [InlineData(107374182400, "107374182400 bytes (100.00 GiB)")]
    public void A_size_is_the_exact_bytes_and_a_binary_unit(long bytes, string expected)
    {
        Assert.Equal(expected, ByteSize.Describe(bytes));
    }
}
