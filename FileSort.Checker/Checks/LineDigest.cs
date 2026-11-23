using System.IO.Hashing;

namespace FileSort.Checker.Checks;

/// <summary>
/// What is kept of a set of lines to tell whether another set has the same lines, each as many times: the count
/// of lines, the count of their bytes, and the sum of the 128-bit hashes of the lines modulo 2^128. A line is its
/// bytes without the terminator. The digest does not depend on the order of the lines. It is a sum and not an
/// exclusive or, because under exclusive or two identical lines cancel each other: a file that lost a pair of
/// identical lines and gained a pair of others would look unchanged.
/// </summary>
internal readonly record struct LineDigest(long Lines, long Bytes, UInt128 HashSum)
{
    /// <summary>The digest of these lines and one more.</summary>
    public LineDigest Add(ReadOnlySpan<byte> line)
    {
        // The sum wraps around at 2^128 on purpose.
        return new LineDigest(Lines + 1, Bytes + line.Length, unchecked(HashSum + XxHash128.HashToUInt128(line)));
    }

    /// <summary>
    /// The digest of these lines and the lines of another digest together. The lines of a file can be added up in
    /// parts, in any order of the parts.
    /// </summary>
    public LineDigest Add(LineDigest other)
    {
        return new LineDigest(Lines + other.Lines, Bytes + other.Bytes, unchecked(HashSum + other.HashSum));
    }
}
