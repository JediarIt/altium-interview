namespace FileSort.Engine.Runs;

/// <summary>A sorted run in the run storage: its number there and its length in bytes.</summary>
internal readonly record struct Run(int Id, long Length);
