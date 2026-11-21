using System.Globalization;
using System.Text;

using FileSort.Generator.Content.Lines;
using FileSort.Generator.Random;

namespace FileSort.Tests.Generator.Content.Lines;

public class PlainNumberWriterTests
{
    private static string Write(uint value)
    {
        byte[] buffer = new byte[PlainNumberWriter.RequiredLength];
        int length = PlainNumberWriter.Write(buffer, value);
        return Encoding.ASCII.GetString(buffer, 0, length);
    }

    [Fact]
    public void Values_around_every_change_of_length_are_written_in_canonical_form()
    {
        List<uint> values = [0, 1, 9, uint.MaxValue, uint.MaxValue - 1, 99_999_999, 100_000_000, 100_000_001];
        for (uint power = 10; power <= 1_000_000_000; power *= 10)
        {
            values.AddRange([power - 1, power, power + 1, (power * 4) + 2]);
            if (power == 1_000_000_000)
            {
                break;
            }
        }

        for (int bit = 0; bit < 32; bit++)
        {
            values.AddRange([(1u << bit) - 1, 1u << bit, (1u << bit) + 1]);
        }

        Assert.All(values, value => Assert.Equal(value.ToString(CultureInfo.InvariantCulture), Write(value)));
    }

    [Fact]
    public void Random_values_of_every_bit_length_are_written_in_canonical_form()
    {
        Xoshiro256 random = Xoshiro256.Create(1, 0);
        for (int i = 0; i < 2_000_000; i++)
        {
            ulong bits = random.Next();
            uint value = (uint)(bits >> (64 - ((int)(bits & 31) + 1)));

            string written = Write(value);

            if (written != value.ToString(CultureInfo.InvariantCulture))
            {
                Assert.Fail($"{value} was written as '{written}'.");
            }
        }
    }

    [Fact]
    public void Every_two_digit_and_four_digit_group_is_split_correctly()
    {
        // The divisions by 100 and by 10 are multiplications that are exact only below 10^4 and below 100: every
        // value of those ranges is checked in every position of the eight low digits.
        for (uint group = 0; group < 10_000; group++)
        {
            foreach (uint value in new[] { group, group * 10_000, (group * 10_000) + group, (group * 10_000) + 9_999 - group })
            {
                Assert.Equal(value.ToString(CultureInfo.InvariantCulture), Write(value));
            }
        }
    }
}
