using System.Runtime.CompilerServices;

namespace FileSort.Engine.Lines;

/// <summary>
/// The last field of an index entry: two facts about a line packed into one 32-bit number, so that the entry stays
/// 16 bytes.
/// <code>
///   bits 0 to 20    the length of the line, its terminator not counted: 0 to 2097151 bytes
///   bits 21 to 30   the count of digits of the Number: 0 to 1022 exactly, 1023 for 1023 digits or more
///   bit 31          not used
/// </code>
/// For the line <c>415. Apple</c>, 10 bytes with a Number of 3 digits, the shape is 10 + 3 × 2^21 = 0x0060000A.
/// </summary>
internal static class EntryShape
{
    /// <summary>The longest line that a shape can hold, 2 MiB less one byte: above the limit of the sorter.</summary>
    public const int MaxLineLength = (1 << LineLengthBits) - 1;

    /// <summary>
    /// The count of digits stored for a Number of this many digits or more. Such a Number is valid, and absurd enough
    /// that counting its digits again, whenever they are needed, costs nothing that matters.
    /// </summary>
    public const int SaturatedNumberLength = (1 << NumberLengthBits) - 1;

    private const int LineLengthBits = 21;
    private const int NumberLengthBits = 10;
    private const int NumberLengthFirstBit = LineLengthBits;

    /// <summary>
    /// Packs the two facts. A Number of more than 1022 digits is stored as 1023, see
    /// <see cref="SaturatedNumberLength"/>; a longer line than <see cref="MaxLineLength"/> cannot be stored.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Pack(int lineLength, int numberLength)
    {
        uint digits = (uint)Math.Min(numberLength, SaturatedNumberLength);
        return (uint)lineLength | (digits << NumberLengthFirstBit);
    }

    /// <summary>The bits 0 to 20: 10 for <c>0x0060000A</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LineLength(uint shape)
    {
        return (int)(shape & MaxLineLength);
    }

    /// <summary>
    /// The bits 21 to 30: 3 for <c>0x0060000A</c>. The value <see cref="SaturatedNumberLength"/> means 1023 digits or
    /// more.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int StoredNumberLength(uint shape)
    {
        return (int)((shape >> NumberLengthFirstBit) & SaturatedNumberLength);
    }
}
