using FileSort.Cli.Terminal;

namespace FileSort.Tests.Cli.Terminal;

public class AppConsoleExtensionsTests
{
    [Fact]
    public void An_error_goes_to_the_error_stream()
    {
        FakeConsole console = new();

        console.TryWriteError("Cannot read the file.");

        Assert.Equal("Cannot read the file." + Environment.NewLine, console.ErrorText);
        Assert.Empty(console.OutText);
    }

    [Fact]
    public void An_error_written_to_a_broken_stream_does_not_fail()
    {
        // A stream redirected to a file on a full disk fails like this.
        FakeConsole console = new()
        {
            OnErrorWrite = _ => throw new IOException("There is not enough space on the disk."),
        };

        console.TryWriteError("Cannot read the file.");

        Assert.Empty(console.ErrorText);
    }
}
