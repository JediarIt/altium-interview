using FileSort.Engine.Lines.Parsing;

namespace FileSort.Tests.Engine.Lines.Parsing;

/// <summary>
/// The standard constructors of the exceptions about lines, which the engine does not use itself: they keep the
/// message and the cause, and leave the details of a line empty.
/// </summary>
public class LineExceptionsTests
{
    private static readonly IOException Cause = new("The cause.");

    [Fact]
    public void An_invalid_line()
    {
        Assert.NotEmpty(new InvalidLineException().Message);
        Assert.Equal("Not valid.", new InvalidLineException("Not valid.").Message);
        Assert.Same(Cause, new InvalidLineException("Not valid.", Cause).InnerException);
        Assert.Equal(0, new InvalidLineException("Not valid.").LineNumber);
        Assert.Equal(LineFault.None, new InvalidLineException("Not valid.").Fault);
    }

    [Fact]
    public void A_line_that_is_too_long()
    {
        Assert.NotEmpty(new LineTooLongException().Message);
        Assert.Equal("Too long.", new LineTooLongException("Too long.").Message);
        Assert.Same(Cause, new LineTooLongException("Too long.", Cause).InnerException);
        Assert.Equal(0, new LineTooLongException("Too long.").Limit);
    }
}
