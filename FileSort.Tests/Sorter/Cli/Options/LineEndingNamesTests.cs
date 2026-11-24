using FileSort.Common;
using FileSort.Sorter.Cli.Options;

namespace FileSort.Tests.Sorter.Cli.Options;

/// <summary>The values of <c>--line-ending</c> and the names that the reports give a line ending.</summary>
public class LineEndingNamesTests
{
    [Theory]
    [InlineData(LineEndingNames.Lf, LineEnding.Lf)]
    [InlineData(LineEndingNames.CrLf, LineEnding.CrLf)]
    public void A_name_and_its_line_ending(string name, LineEnding lineEnding)
    {
        Assert.Equal(lineEnding, LineEndingNames.Parse(name));
        Assert.Equal(name, LineEndingNames.Format(lineEnding));
    }

    [Fact]
    public void Auto_is_no_line_ending_yet()
    {
        Assert.Null(LineEndingNames.Parse(LineEndingNames.Auto));
    }

    [Fact]
    public void A_value_that_the_option_does_not_accept_is_refused()
    {
        // The command line refuses it first; this is the last line of defence.
        Assert.Throws<ArgumentException>(() => LineEndingNames.Parse("cr"));
    }
}
