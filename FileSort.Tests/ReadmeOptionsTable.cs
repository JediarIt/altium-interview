using System.CommandLine;
using System.Text.RegularExpressions;

namespace FileSort.Tests;

/// <summary>
/// The table of options that the README of a program lists, typed by hand, read back for a comparison with the
/// command line of the program. The comparison guards the part of such a table that goes stale without anyone
/// noticing: the option names and the defaults that are fixed values. The README is copied next to the test
/// binaries as a linked file.
/// </summary>
internal sealed partial class ReadmeOptionsTable
{
    private readonly List<Row> _rows;

    private ReadmeOptionsTable(List<Row> rows)
    {
        _rows = rows;
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex CodeSpan();

    /// <summary>
    /// Reads the first table of the file whose header starts with <c>| Option |</c> and ends with a
    /// <c>Default</c> column.
    /// </summary>
    public static ReadmeOptionsTable Read(string linkedFileName)
    {
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, linkedFileName));
        int header = Array.FindIndex(lines, line => line.StartsWith("| Option |", StringComparison.Ordinal));
        Assert.True(header >= 0, $"{linkedFileName} has no table that starts with '| Option |'.");

        string[] columns = Cells(lines[header]);
        Assert.Equal("Default", columns[^1]);

        List<Row> rows = [];
        for (int i = header + 2; i < lines.Length && lines[i].StartsWith('|'); i++)
        {
            string[] cells = Cells(lines[i]);
            Assert.Equal(columns.Length, cells.Length);
            rows.Add(new Row([.. CodeSpan().Matches(cells[0]).Select(match => match.Groups[1].Value)], cells[^1]));
        }

        return new ReadmeOptionsTable(rows);
    }

    /// <summary>The table has a row for every option of the command, under every name a user can type, and no other.</summary>
    public void AssertListsExactlyTheOptionsOf(RootCommand command)
    {
        IEnumerable<string> expected = command.Options.SelectMany(Names).Order(StringComparer.Ordinal);

        IEnumerable<string> actual = _rows.SelectMany(row => row.Names).Order(StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Every default that is a fixed value stands in the table as it is in the command, and the table gives no
    /// default where the command has none.
    /// </summary>
    /// <param name="command">The command of the program.</param>
    /// <param name="defaultsInWords">
    /// Options whose default depends on the machine, so the table describes it in words.
    /// </param>
    public void AssertHasTheFixedDefaultsOf(RootCommand command, params string[] defaultsInWords)
    {
        foreach (Option option in command.Options)
        {
            Row row = _rows.Single(r => r.Names.Contains(option.Name));
            Match inTable = CodeSpan().Match(row.Default);

            // A flag is off unless given: the table has no value to show for it.
            bool isFlag = option.ValueType == typeof(bool);
            if (option.HasDefaultValue && !isFlag && !defaultsInWords.Contains(option.Name))
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

    private static string[] Cells(string line)
    {
        return [.. line.Trim().Trim('|').Split('|').Select(cell => cell.Trim())];
    }

    // The names that a user types: the option's name and its aliases with a dash.
    private static string[] Names(Option option)
    {
        return [option.Name, .. option.Aliases.Where(alias => alias.StartsWith('-')).Order(StringComparer.Ordinal)];
    }

    private sealed record Row(string[] Names, string Default);
}
