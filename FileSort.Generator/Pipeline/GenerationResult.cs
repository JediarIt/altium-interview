namespace FileSort.Generator.Pipeline;

/// <summary>What a run of the pipeline produced.</summary>
internal readonly record struct GenerationResult(long Bytes, long Lines);
