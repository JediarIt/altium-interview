using FileSort.Common;
using FileSort.Generator.Random;

namespace FileSort.Generator.Content.Lines;

/// <summary>
/// Composes whole lines straight into a span: the Number, the separator, a fresh String or a pool String, and the
/// terminator. No <see cref="string"/> and no heap allocation per line. The way random bits are spent here is part
/// of the content version.
/// </summary>
internal sealed class LineComposer
{
    // How the random values of a line are spent; RandomBits reads the groups of bits.
    //
    // The first value, the head:
    //   bits 0 to 4     the bit length of the Number, 1 to 32
    //   bits 5 to 20    whether the String comes from the pool
    //   bits 21 to 30   which of the 1024 pool Strings
    //   bits 32 to 63   the value of the Number: as many of the highest bits as the bit length says
    // Bit 31 is not used.
    //
    // The second value, the edge case roll, is drawn only when a category is enabled:
    //   bits 0 to 23    which categories apply to the line, 4 bits each, see EdgeRoll
    //   bits 24 to 26   big Numbers: all three are 0 in one case of 8, and then the Number is a boundary value
    //   bits 27 to 28   which of the four boundary values
    //   bits 29 to 33   the bit length of any other big Number, 33 to 64
    //   bits 34 to 41   how many leading zeros, 1 to 3
    //
    // Then the String takes its own values, see StringComposer.
    private const int NumberLengthBits = 5;
    private const int PoolDecisionFirstBit = NumberLengthBits;
    private const int PoolDecisionBits = 16;
    private const int PoolIndexFirstBit = PoolDecisionFirstBit + PoolDecisionBits;

    private const int BoundaryDecisionFirstBit = EdgeRoll.FreeBitsStart;
    private const int BoundaryDecisionBits = 3;
    private const int BoundaryIndexFirstBit = BoundaryDecisionFirstBit + BoundaryDecisionBits;
    private const int BoundaryIndexBits = 2;
    private const int BigLengthFirstBit = BoundaryIndexFirstBit + BoundaryIndexBits;
    private const int BigLengthBits = 5;
    private const int ZerosFirstBit = BigLengthFirstBit + BigLengthBits;
    private const int ZerosBits = 8;

    private const EdgeCases NumberCategories = EdgeCases.BigNumbers | EdgeCases.LeadingZeros;

    private readonly StringComposer _strings;
    private readonly RepeatPool? _pool;
    private readonly int _duplicatesPercent;
    private readonly EdgeCases _enabled;
    private readonly LineEnding _lineEnding;

    public LineComposer(ContentOptions options)
    {
        _strings = new StringComposer(options);
        _pool = options.DuplicatesPercent > 0 ? new RepeatPool(options, _strings) : null;
        _duplicatesPercent = options.DuplicatesPercent;
        _enabled = options.EdgeCases;
        _lineEnding = options.LineEnding;
        MaxLineLength = LineFormat.MaxNumberDigits + LineFormat.SeparatorLength + _strings.MaxLength
            + _lineEnding.TerminatorLength();
    }

    /// <summary>The values where a comparer of Numbers is most likely to be wrong: around 2^32, 2^63 and 2^64.</summary>
    private static ReadOnlySpan<ulong> BoundaryNumbers =>
        [4294967296, 9223372036854775807, 9223372036854775808, LineFormat.MaxNumber];

    /// <summary>The length of the longest line that these options can produce, terminator included.</summary>
    public int MaxLineLength { get; }

    /// <summary>
    /// Writes one line with its terminator and returns its length. The destination needs
    /// <see cref="MaxLineLength"/> plus <see cref="StringComposer.Slack"/> bytes.
    /// </summary>
    public int WriteLine(ref Xoshiro256 random, Span<byte> destination)
    {
        ulong head = random.Next();

        // The roll is drawn only when a category is enabled, so plain content does not pay for edge cases.
        ulong roll = 0;
        EdgeCases applied = EdgeCases.None;
        if (_enabled != EdgeCases.None)
        {
            roll = random.Next();
            applied = EdgeRoll.Apply(roll, _enabled);
        }

        int position;
        if ((applied & NumberCategories) == 0)
        {
            position = PlainNumberWriter.Write(destination, (uint)PlainNumber(head));
        }
        else
        {
            position = WriteUnusualNumber(ref random, head, roll, applied, destination);
        }

        position += LineWriter.WriteSeparator(destination[position..]);

        // A whole percentage from 0 to 99, each about equally likely. The String comes from the pool when the
        // percentage is below --duplicates: with 30, the percentages 0 to 29 take a pool String, which is 30% of the
        // lines. Integers only, so the result is the same on every platform.
        if (RandomBits.Pick(head, PoolDecisionFirstBit, PoolDecisionBits, 100) < _duplicatesPercent)
        {
            ReadOnlySpan<byte> repeated = _pool![RandomBits.Bits(head, PoolIndexFirstBit, ContentFormat.PoolIndexBits)];
            repeated.CopyTo(destination[position..]);
            position += repeated.Length;
        }
        else
        {
            position += _strings.Compose(ref random, applied, destination[position..]);
        }

        position += LineWriter.WriteTerminator(destination[position..], _lineEnding);
        return position;
    }

    // A bit length from 1 to 32 with equal probability, then a uniform value of that many bits: with a bit length of
    // 4 the Number is from 0 to 15, with 32 from 0 to 4294967295. Short and long Numbers are equally likely, so equal
    // Numbers and fully identical lines occur even in a small file.
    private static ulong PlainNumber(ulong head)
    {
        int bitLength = RandomBits.Bits(head, 0, NumberLengthBits) + 1;
        return RandomBits.Highest(head, bitLength);
    }

    private static int WriteUnusualNumber(
        ref Xoshiro256 random, ulong head, ulong roll, EdgeCases applied, Span<byte> destination)
    {
        ulong number;
        if ((applied & EdgeCases.BigNumbers) == 0)
        {
            number = PlainNumber(head);
        }
        else if (RandomBits.Bits(roll, BoundaryDecisionFirstBit, BoundaryDecisionBits) == 0)
        {
            // One case of 8: one of the four boundary values.
            number = BoundaryNumbers[RandomBits.Bits(roll, BoundaryIndexFirstBit, BoundaryIndexBits)];
        }
        else
        {
            // A Number of exactly 33 to 64 bits, so it never fits in 32 bits: with 33 bits it is from
            // 2^32 = 4294967296 to 2^33 - 1.
            int bitLength = 33 + RandomBits.Bits(roll, BigLengthFirstBit, BigLengthBits);
            number = RandomBits.WithBitLength(random.Next(), bitLength);
        }

        int zeros = 0;
        if ((applied & EdgeCases.LeadingZeros) != 0)
        {
            // One to three zeros, as far as the limit of 20 digits allows: a Number of 18 digits gets at most two
            // zeros, one of 20 digits none.
            zeros = 1 + RandomBits.Pick(roll, ZerosFirstBit, ZerosBits, 3);
            zeros = Math.Min(zeros, LineFormat.MaxNumberDigits - LineWriter.CountDigits(number));
        }

        return LineWriter.WriteNumber(destination, number, zeros);
    }
}
