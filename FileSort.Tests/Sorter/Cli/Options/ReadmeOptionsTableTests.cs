using FileSort.Sorter.Cli.Options;

namespace FileSort.Tests.Sorter.Cli.Options;

/// <summary>
/// The README of the sorter lists its options in a table typed by hand. These tests compare the option names and the
/// fixed defaults of that table with the command line itself, so the table cannot silently fall behind the code.
/// </summary>
public class ReadmeOptionsTableTests
{
    private const string Readme = "FileSort.Sorter.README.md";

    // Options whose default depends on the machine, so the table describes it in words.
    private static readonly string[] DefaultsInWords = ["--threads"];

    [Fact]
    public void The_table_lists_exactly_the_options_of_the_command()
    {
        ReadmeOptionsTable.Read(Readme).AssertListsExactlyTheOptionsOf(new SorterCommand().Root);
    }

    [Fact]
    public void The_fixed_defaults_of_the_table_are_the_defaults_of_the_command()
    {
        ReadmeOptionsTable.Read(Readme).AssertHasTheFixedDefaultsOf(new SorterCommand().Root, DefaultsInWords);
    }
}
