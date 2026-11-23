using FileSort.Cli;

namespace FileSort.Tests.Cli;

/// <summary>
/// The exit codes are a contract with scripts: a change of a value is a breaking change, so the values are pinned.
/// </summary>
public class ExitCodeTests
{
    [Fact]
    public void The_codes_have_the_documented_values()
    {
        Assert.Equal(0, ExitCode.Success);
        Assert.Equal(1, ExitCode.Failure);
        Assert.Equal(2, ExitCode.InvalidArguments);
        Assert.Equal(3, ExitCode.InvalidFile);
        Assert.Equal(4, ExitCode.NotCorrect);
        Assert.Equal(130, ExitCode.Cancelled);
    }
}
