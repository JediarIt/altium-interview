using System.Numerics;
using System.Runtime.CompilerServices;

namespace FileSort.Generator.Random;

/// <summary>
/// xoshiro256** by David Blackman and Sebastiano Vigna. The generator owns its algorithm instead of using
/// <see cref="System.Random"/>, whose sequence for a seed is not promised across .NET versions: the bytes of a
/// generated file must depend on the seed alone. The constructor takes a raw state, which must not be all zeros;
/// <see cref="Create"/> derives the state of a stream from a seed.
/// </summary>
/// <remarks>
/// The framework has this algorithm too, behind <see cref="System.Random.Shared"/> and an unseeded
/// <see cref="System.Random"/>, but that implementation is internal and cannot be given a seed. A package for these
/// twenty lines would be the generator's second dependency; the lines follow the authors' reference code and are
/// pinned by known-answer tests against its published values.
/// </remarks>
internal struct Xoshiro256(ulong s0, ulong s1, ulong s2, ulong s3)
{
    /// <summary>The stream id of the pool of repeated Strings; block <c>n</c> uses the stream id <c>n</c>.</summary>
    public const ulong PoolStream = ulong.MaxValue;

    private ulong _s0 = s0;
    private ulong _s1 = s1;
    private ulong _s2 = s2;
    private ulong _s3 = s3;

    /// <summary>
    /// Creates the generator of one stream of a seed. Streams of one seed are independent sequences: each block of a
    /// file has its own, so any thread can produce any block.
    /// </summary>
    public static Xoshiro256 Create(ulong seed, ulong stream)
    {
        // The seed is mixed before the stream id is combined with it, so that (seed 1, stream 2) and
        // (seed 2, stream 1) do not start from the same state.
        SplitMix64 expander = new(new SplitMix64(seed).Next() ^ stream);
        return new Xoshiro256(expander.Next(), expander.Next(), expander.Next(), expander.Next());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Next()
    {
        // The authors' reference code, line for line. The shifts, rotations and exclusive ors mix the state; they are
        // not meant to be read bit by bit. Known-answer tests compare the output with the authors' published values.
        ulong result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);

        return result;
    }
}
