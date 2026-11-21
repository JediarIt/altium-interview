using System.Security.Cryptography;

using FileSort.Generator.Pipeline;

namespace FileSort.Tests.Generator;

/// <summary>
/// Keeps only the SHA-256 and the length of what it receives, so the content of a large output can be compared
/// without a disk and without holding it in memory.
/// </summary>
internal sealed class HashingSink : IBlockSink, IDisposable
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public long Length { get; private set; }

    public void Write(ReadOnlySpan<byte> data)
    {
        _hash.AppendData(data);
        Length += data.Length;
    }

    /// <summary>The SHA-256 of everything written so far, as upper-case hexadecimal digits.</summary>
    public string GetHashHex()
    {
        return Convert.ToHexString(_hash.GetCurrentHash());
    }

    public void Dispose()
    {
        _hash.Dispose();
    }
}
