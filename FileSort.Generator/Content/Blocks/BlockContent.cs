namespace FileSort.Generator.Content.Blocks;

/// <summary>The result of filling one block: how many bytes and how many lines it holds.</summary>
internal readonly record struct BlockContent(int Bytes, int Lines);
