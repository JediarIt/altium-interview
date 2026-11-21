namespace FileSort.Generator.Random;

/// <summary>
/// SplitMix64 by Sebastiano Vigna: a 64-bit generator with a 64-bit state. Used only to turn a seed into the state of
/// <see cref="Xoshiro256"/>, as its authors recommend.
/// </summary>
internal struct SplitMix64(ulong state)
{
    private ulong _state = state;

    public ulong Next()
    {
        // The authors' reference code with their constants: it mixes the bits so that close seeds, such as 1 and 2,
        // give unrelated values. Not meant to be read bit by bit.
        ulong z = _state += 0x9E3779B97F4A7C15;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }
}
