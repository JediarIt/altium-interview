using FileSort.Generator.Random;

namespace FileSort.Tests.Generator.Random;

public class RandomBitsTests
{
    [Theory]
    [InlineData(0b11010110UL, 2, 3, 0b101)]
    [InlineData(0xABCDUL, 0, 4, 0xD)]
    [InlineData(0xABCDUL, 4, 8, 0xBC)]
    [InlineData(0xABCDUL, 12, 4, 0xA)]
    [InlineData(ulong.MaxValue, 48, 16, 0xFFFF)]
    [InlineData(0x8000_0000_0000_0000UL, 63, 1, 1)]
    public void Bits_reads_a_group_of_bits(ulong value, int first, int count, int expected)
    {
        Assert.Equal(expected, RandomBits.Bits(value, first, count));
    }

    [Fact]
    public void IsSet_reads_one_bit()
    {
        Assert.True(RandomBits.IsSet(0b100, 2));
        Assert.False(RandomBits.IsSet(0b100, 1));
        Assert.True(RandomBits.IsSet(ulong.MaxValue, 63));
    }

    [Fact]
    public void Pick_spreads_the_values_of_the_bits_evenly()
    {
        int[] counts = new int[3];
        for (ulong r = 0; r < 256; r++)
        {
            counts[RandomBits.Pick(r << 8, 8, 8, 3)]++;
        }

        Assert.Equal([86, 85, 85], counts);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(100)]
    [InlineData(1024)]
    public void Pick_covers_every_choice_and_stays_below_the_number_of_choices(int choices)
    {
        bool[] seen = new bool[choices];
        for (ulong r = 0; r < 65536; r++)
        {
            seen[RandomBits.Pick(r, 0, 16, choices)] = true;
        }

        Assert.DoesNotContain(false, seen);
    }

    [Fact]
    public void Pick_reads_only_its_own_bits()
    {
        Assert.Equal(RandomBits.Pick(0x00AB_0000UL, 16, 8, 100), RandomBits.Pick(0xFFAB_FFFFUL, 16, 8, 100));
    }

    [Theory]
    [InlineData(0xF000_0000_0000_0000UL, 4, 15UL)]
    [InlineData(0x8000_0000_0000_0000UL, 1, 1UL)]
    [InlineData(0x1234_5678_9ABC_DEF0UL, 32, 0x1234_5678UL)]
    [InlineData(0x1234_5678_9ABC_DEF0UL, 64, 0x1234_5678_9ABC_DEF0UL)]
    public void Highest_reads_the_top_bits(ulong value, int count, ulong expected)
    {
        Assert.Equal(expected, RandomBits.Highest(value, count));
    }

    [Theory]
    [InlineData(0UL, 33, 4294967296UL)]
    [InlineData(ulong.MaxValue, 33, 8589934591UL)]
    [InlineData(0UL, 64, 9223372036854775808UL)]
    [InlineData(ulong.MaxValue, 64, ulong.MaxValue)]
    [InlineData(0UL, 1, 1UL)]
    public void WithBitLength_gives_a_number_of_exactly_that_many_bits(ulong value, int bitLength, ulong expected)
    {
        Assert.Equal(expected, RandomBits.WithBitLength(value, bitLength));
    }
}
