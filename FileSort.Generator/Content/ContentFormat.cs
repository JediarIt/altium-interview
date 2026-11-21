namespace FileSort.Generator.Content;

/// <summary>
/// The constants that the bytes of a generated file depend on, frozen under one version number. A change to any of
/// them, to the dictionary, to the edge case rates or to the way random bits are used alters the output for an
/// existing seed and has to raise <see cref="Version"/>; the reference hashes in the tests fail until it does.
/// </summary>
internal static class ContentFormat
{
    /// <summary>The content version, printed in the summary of every run.</summary>
    public const int Version = 1;

    /// <summary>
    /// The capacity of a block in bytes. A file is the concatenation of blocks, each filled with whole lines by its
    /// own random stream, so the capacity decides where one stream ends and the next begins.
    /// </summary>
    public const int BlockCapacity = 1024 * 1024;

    /// <summary>The number of Strings in the pool of repeated Strings.</summary>
    public const int PoolSize = 1024;

    /// <summary>The number of random bits that select a pool String: 2^10 is <see cref="PoolSize"/>.</summary>
    public const int PoolIndexBits = 10;
}
