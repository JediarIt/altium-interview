using System.Text;

using FileSort.Engine.Lines;

namespace FileSort.Tests.Engine.Lines;

/// <summary>The prefix of a String in an index entry, against worked examples.</summary>
public class StringPrefixTests
{
    [Theory]
    [InlineData("Apple pie", 0x4170706C65207069UL)]
    [InlineData("Apple pi", 0x4170706C65207069UL)]
    [InlineData("AB", 0x4142000000000000UL)]
    [InlineData("", 0UL)]
    [InlineData("ÿ", 0xC3BF000000000000UL)]
    public void The_prefix_is_the_first_8_bytes_big_endian_padded_with_zeros(string text, ulong expected)
    {
        Assert.Equal(expected, StringPrefix.Read(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void Prefixes_compare_as_their_bytes()
    {
        string[] ordered = ["", "\u0001", "A", "AB", "AB\u0001", "ABC", "Apple", "Apple pi", "a", "ÿ", "Ａ"];

        for (int i = 1; i < ordered.Length; i++)
        {
            Assert.True(Prefix(ordered[i - 1]) < Prefix(ordered[i]), $"'{ordered[i - 1]}' before '{ordered[i]}'");
        }
    }

    [Fact]
    public void Zero_bytes_at_the_end_do_not_change_the_prefix()
    {
        // U+0000 is valid UTF-8, and the padding is zero bytes too: the comparison tells such Strings apart by their
        // lengths.
        Assert.Equal(Prefix(""), Prefix("\0"));
        Assert.Equal(Prefix("AB"), Prefix("AB\0\0"));
        Assert.Equal(Prefix("Apple pi"), Prefix("Apple pie"));
    }

    private static ulong Prefix(string text) => StringPrefix.Read(Encoding.UTF8.GetBytes(text));
}
