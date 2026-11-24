using FileSort.Engine.Lines.Parsing;

namespace FileSort.Tests.Engine;

/// <summary>
/// The engine is a library with no console dependencies, so that tests and measurements run it in-process and the
/// sorter is only a host around it. Project references enforce that, and this test keeps the rule visible: it fails
/// the day the engine's assembly uses console code.
/// </summary>
public class EngineIndependenceTests
{
    [Fact]
    public void The_assembly_of_the_engine_references_no_console_code()
    {
        string?[] referenced = [.. typeof(LineFault).Assembly.GetReferencedAssemblies().Select(name => name.Name)];

        Assert.DoesNotContain("System.CommandLine", referenced);
        Assert.DoesNotContain("FileSort.Cli", referenced);
        Assert.DoesNotContain("Spectre.Console", referenced);
        Assert.DoesNotContain("System.Console", referenced);
        Assert.DoesNotContain("FileSort.Checker", referenced);
        Assert.DoesNotContain("FileSort.Sorter", referenced);
    }
}
