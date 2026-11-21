using FileSort.Generator.Random;

namespace FileSort.Tests.Generator.Random;

public class RandomTests
{
    private static ulong[] Take(Xoshiro256 random, int count)
    {
        ulong[] values = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = random.Next();
        }

        return values;
    }

    [Fact]
    public void SplitMix64_matches_the_reference_values()
    {
        SplitMix64 random = new(1234567);

        ulong[] values = [random.Next(), random.Next(), random.Next(), random.Next(), random.Next()];

        Assert.Equal(
            [
                6457827717110365317UL,
                3203168211198807973UL,
                9817491932198370423UL,
                4593380528125082431UL,
                16408922859458223821UL,
            ],
            values);
    }

    [Fact]
    public void Xoshiro256_matches_the_reference_values()
    {
        Xoshiro256 random = new(1, 2, 3, 4);

        Assert.Equal([11520UL, 0UL, 1509978240UL, 1215971899390074240UL], Take(random, 4));
    }

    [Fact]
    public void A_stream_is_seeded_through_SplitMix64()
    {
        const ulong seed = 42;
        const ulong stream = 7;
        SplitMix64 expander = new(new SplitMix64(seed).Next() ^ stream);
        Xoshiro256 expected = new(expander.Next(), expander.Next(), expander.Next(), expander.Next());

        Assert.Equal(Take(expected, 8), Take(Xoshiro256.Create(seed, stream), 8));
    }

    [Fact]
    public void The_same_seed_and_stream_give_the_same_sequence()
    {
        Assert.Equal(Take(Xoshiro256.Create(1, 0), 16), Take(Xoshiro256.Create(1, 0), 16));
    }

    [Fact]
    public void Different_seeds_give_different_sequences()
    {
        Assert.NotEqual(Take(Xoshiro256.Create(1, 0), 16), Take(Xoshiro256.Create(2, 0), 16));
    }

    [Fact]
    public void Different_streams_give_different_sequences()
    {
        Assert.NotEqual(Take(Xoshiro256.Create(1, 0), 16), Take(Xoshiro256.Create(1, 1), 16));
        Assert.NotEqual(Take(Xoshiro256.Create(1, 0), 16), Take(Xoshiro256.Create(1, Xoshiro256.PoolStream), 16));
    }

    [Fact]
    public void Swapping_the_seed_and_the_stream_gives_a_different_sequence()
    {
        Assert.NotEqual(Take(Xoshiro256.Create(1, 2), 16), Take(Xoshiro256.Create(2, 1), 16));
    }
}
