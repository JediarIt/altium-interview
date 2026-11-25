using System.Text.Json;

namespace FileSort.Tests.Sorter;

/// <summary>
/// The sorter is published for the installed .NET runtime, not as a standalone executable, so the settings it gives
/// the runtime's host decide whether it starts at all. The build writes them next to the program, and next to these
/// tests, which reference it.
/// </summary>
public class RuntimeVersionTests
{
    [Fact]
    public void The_sorter_runs_on_the_next_major_version_of_the_runtime_when_its_own_is_not_installed()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "FileSort.Sorter.runtimeconfig.json");
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement options = settings.RootElement.GetProperty("runtimeOptions");

        // Without it a machine that has only a newer .NET refuses to start the sorter: the host does not pass from
        // one major version to the next unless the program allows it.
        Assert.True(options.TryGetProperty("rollForward", out JsonElement rollForward), "No rollForward setting.");
        Assert.Equal("Major", rollForward.GetString());
    }
}
