using System.CommandLine;
using System.Text.RegularExpressions;

using FileSort.Generator.Cli.Options;

namespace FileSort.Tests.Generator.Cli.Options;

/// <summary>
/// The README of the generator lists its options in a table typed by hand. This test guards the part of it that goes
/// stale without anyone noticing: the option names and the defaults that are fixed values. That README is
/// copied next to the test binaries as a linked file.
/// </summary>
public partial class ReadmeOptionsTableTests
{
    // Options whose default depends on the machine, so the table describes it in words.
    private static readonly string[] DefaultsInWords = ["--threads"];

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex CodeSpan();

    private sealed record Row(string[] Names, string Default);

    private static List<Row> ReadTable()
    {
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "FileSort.Generator.README.md"));
        int header = Array.FindIndex(lines, line => line.StartsWith("| Option |", StringComparison.Ordinal));
        Assert.True(header >= 0, "The README of the generator has no table that starts with '| Option |'.");

        string[] columns = Cells(lines[header]);
        Assert.Equal("Default", columns[^1]);

        List<Row> rows = [];
        for (int i = header + 2; i < lines.Length && lines[i].StartsWith('|'); i++)
        {
            string[] cells = Cells(lines[i]);
            Assert.Equal(columns.Length, cells.Length);
            rows.Add(new Row([.. CodeSpan().Matches(cells[0]).Select(match => match.Groups[1].Value)], cells[^1]));
        }

        return rows;
    }

    private static string[] Cells(string line)
    {
        return [.. line.Trim().Trim('|').Split('|').Select(cell => cell.Trim())];
    }

    // The names that a user types: the option's name and its aliases with a dash.
    private static string[] Names(Option option)
    {
        return [option.Name, .. option.Aliases.Where(alias => alias.StartsWith('-')).Order(StringComparer.Ordinal)];
    }

    [Fact]
    public void The_table_lists_exactly_the_options_of_the_command()
    {
        IEnumerable<string> expected = new GeneratorCommand().Root.Options.SelectMany(Names).Order(StringComparer.Ordinal);

        IEnumerable<string> actual = ReadTable().SelectMany(row => row.Names).Order(StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void The_fixed_defaults_of_the_table_are_the_defaults_of_the_command()
    {
        List<Row> rows = ReadTable();

        foreach (Option option in new GeneratorCommand().Root.Options)
        {
            Row row = rows.Single(r => r.Names.Contains(option.Name));
            Match inTable = CodeSpan().Match(row.Default);

            // A flag is off unless given: the table has no value to show for it.
            bool isFlag = option.ValueType == typeof(bool);
            if (option.HasDefaultValue && !isFlag && !DefaultsInWords.Contains(option.Name))
            {
                Assert.True(inTable.Success, $"{option.Name}: the table gives no default value.");
                Assert.Equal(option.GetDefaultValue()?.ToString(), inTable.Groups[1].Value);
            }
            else
            {
                Assert.False(inTable.Success, $"{option.Name}: the table gives a default value, the command has none.");
            }
        }
    }
}
