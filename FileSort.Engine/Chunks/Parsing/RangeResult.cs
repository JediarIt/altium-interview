namespace FileSort.Engine.Chunks.Parsing;

/// <summary>What the parsing of one range found: the lines it parsed, and the line it stopped at, if any.</summary>
internal readonly record struct RangeResult(int Lines, RangeProblem? Problem);
