using System.Runtime.CompilerServices;

using FileSort.Engine.Lines;

namespace FileSort.Tests.Engine.Lines;

/// <summary>The packing of an index entry, against worked examples.</summary>
public class EntryShapeTests
{
    [Fact]
    public void An_entry_is_16_bytes()
    {
        Assert.Equal(16, Unsafe.SizeOf<IndexEntry>());
    }

    [Fact]
    public void The_worked_example_of_the_shape()
    {
        // 415. Apple: 10 bytes, a Number of 3 digits.
        uint shape = EntryShape.Pack(10, 3);

        Assert.Equal(0x0060000AU, shape);
        Assert.Equal(10, EntryShape.LineLength(shape));
        Assert.Equal(3, EntryShape.StoredNumberLength(shape));
    }

    [Theory]
    [InlineData(0, 0, 0x00000000U)]
    [InlineData(1, 1, 0x00200001U)]
    [InlineData(2097151, 1022, 0x7FDFFFFFU)]
    [InlineData(1048576, 20, 0x02900000U)]
    public void The_two_fields_stay_apart(int lineLength, int numberLength, uint expected)
    {
        uint shape = EntryShape.Pack(lineLength, numberLength);

        Assert.Equal(expected, shape);
        Assert.Equal(lineLength, EntryShape.LineLength(shape));
        Assert.Equal(numberLength, EntryShape.StoredNumberLength(shape));
    }

    [Theory]
    [InlineData(1022, 1022)]
    [InlineData(1023, 1023)]
    [InlineData(1024, 1023)]
    [InlineData(100000, 1023)]
    public void A_Number_of_1023_digits_or_more_is_stored_as_1023(int digits, int stored)
    {
        uint shape = EntryShape.Pack(Math.Min(digits + 2, EntryShape.MaxLineLength), digits);

        Assert.Equal(stored, EntryShape.StoredNumberLength(shape));
    }

    [Theory]
    [InlineData(1022)]
    [InlineData(1023)]
    [InlineData(1024)]
    public void A_long_Number_keeps_its_String_in_reach(int digits)
    {
        // The comparison counts the digits again when the entry stores 1023: the String starts after all of them.
        string number = new('7', digits);
        ParsedLines lines = ParsedLines.From($"{number}. Apple", $"{number}. Apple pie", $"1{number}. Apple");

        Assert.Equal(Math.Min(digits, 1023), EntryShape.StoredNumberLength(lines.Entries[0].Shape));
        Assert.True(lines.Comparer.Compare(in lines.Entries[0], in lines.Entries[1]) < 0);
        Assert.True(lines.Comparer.Compare(in lines.Entries[0], in lines.Entries[2]) < 0);
        Assert.True(lines.Comparer.Compare(in lines.Entries[2], in lines.Entries[1]) < 0);
        Assert.Equal(0, lines.Comparer.Compare(in lines.Entries[0], in lines.Entries[0]));
    }

    [Fact]
    public void The_shape_holds_the_longest_line_of_the_sorter()
    {
        Assert.Equal(2097151, EntryShape.MaxLineLength);
        Assert.True(EntryShape.MaxLineLength > 1024 * 1024);
    }
}
