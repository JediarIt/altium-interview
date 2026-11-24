using System.Reflection;

using FileSort.Checker.Lines;

namespace FileSort.Tests.Checker;

/// <summary>
/// The checker is an oracle with its own parsing and comparison. That holds because its project has no reference
/// to the shared line code, and this test keeps the rule visible: it fails the day the checker's assembly uses
/// <c>FileSort.Common</c>, or the engine of the sorter, which holds the sorter's line parser.
/// </summary>
public class CheckerIndependenceTests
{
    [Fact]
    public void The_assembly_of_the_checker_does_not_reference_the_shared_line_code()
    {
        string?[] referenced = [.. typeof(LineParser).Assembly.GetReferencedAssemblies().Select(name => name.Name)];

        Assert.DoesNotContain("FileSort.Common", referenced);
        Assert.DoesNotContain("FileSort.Generator", referenced);
        Assert.DoesNotContain("FileSort.Sorter", referenced);
        Assert.DoesNotContain("FileSort.Engine", referenced);
    }

    [Fact]
    public void The_shared_console_library_does_not_reference_the_shared_line_code()
    {
        AssemblyName[] references = typeof(FileSort.Cli.ExitCode).Assembly.GetReferencedAssemblies();
        string?[] referenced = [.. references.Select(name => name.Name)];

        Assert.DoesNotContain("FileSort.Common", referenced);
    }
}
