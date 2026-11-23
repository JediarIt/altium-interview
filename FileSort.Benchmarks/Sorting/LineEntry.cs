namespace FileSort.Benchmarks.Sorting;

/// <summary>Where a line lies in a file held in memory: its first byte, its length and the length of its Number.</summary>
internal readonly record struct LineEntry(int Offset, int Length, int NumberLength);
