using FileSort.Engine;

namespace FileSort.Tests.Engine.InMemory;

/// <summary>What a sort in memory did and wrote.</summary>
internal sealed record SortOutcome(SortResult Result, byte[] Output, MemoryRunStorage Storage, SortProgress Progress);
