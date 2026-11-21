namespace FileSort.Tests;

/// <summary>A directory of its own for one test, deleted with everything in it afterwards.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("FileSort.Tests.").FullName;

    public string File(string name)
    {
        return System.IO.Path.Combine(Path, name);
    }

    public string[] FileNames()
    {
        return [.. Directory.GetFiles(Path, "*", SearchOption.AllDirectories)
            .Select(file => System.IO.Path.GetRelativePath(Path, file))
            .Order(StringComparer.Ordinal)];
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover in the temporary directory must not fail a test that passed.
        }
    }
}
