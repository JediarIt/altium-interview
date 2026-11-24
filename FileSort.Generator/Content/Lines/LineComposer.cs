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
    /// <summary>
    /// The most digits of a Number that the generator writes: a long Number of up to 30 digits with up to 3 leading
    /// zeros. Longer Numbers are valid; the generator writes none.
    /// </summary>
    public const int MaxNumberDigits = MaxLongDigits + MaxLeadingZeros;

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
    //   bits 0 to 27    which categories apply to the line, 4 bits each, see EdgeRoll
    //   bits 28 to 30   the kind of a big Number: 0 a boundary value, 1 a long Number, 2 to 7 a Number of 33 to 64
    //                   bits
    //   bits 31 to 38   which of the five boundary values
    //   bits 39 to 43   the bit length of a Number of 33 to 64 bits
    //   bits 44 to 51   the count of digits of a long Number, 21 to 30
    //   bits 52 to 59   how many leading zeros, 1 to 3
    //
    // A Number of 33 to 64 bits takes one more random value, a long Number two. Then the String takes its own values,
    // see StringComposer.
    private const int NumberLengthBits = 5;
    private const int PoolDecisionFirstBit = NumberLengthBits;
    private const int PoolDecisionBits = 16;
    private const int PoolIndexFirstBit = PoolDecisionFirstBit + PoolDecisionBits;

    private const int BigKindFirstBit = EdgeRoll.FreeBitsStart;
    private const int BigKindBits = 3;
    private const int BoundaryIndexFirstBit = BigKindFirstBit + BigKindBits;
    private const int BoundaryIndexBits = 8;
    private const int BigLengthFirstBit = BoundaryIndexFirstBit + BoundaryIndexBits;
    private const int BigLengthBits = 5;
    private const int LongDigitsFirstBit = BigLengthFirstBit + BigLengthBits;
    private const int LongDigitsBits = 8;
    private const int ZerosFirstBit = LongDigitsFirstBit + LongDigitsBits;
    private const int ZerosBits = 8;

    // The kinds of a big Number, read from three bits: two of the eight values have a kind of their own, the other
    // six give a Number of 33 to 64 bits.
    private const int BoundaryKind = 0;
    private const int LongKind = 1;

    private const int MinLongDigits = 21;
    private const int MaxLongDigits = 30;
    private const int MaxLeadingZeros = 3;

    // The digits that the longest line counts for its Number when no Number category is enabled. A plain Number has
    // at most 10 digits, but content version 1 counted 20 for every line, and the longest line decides where a block
    // ends: counting 10 would move the ends of the blocks and change the bytes of plain content, which version 2
    // keeps.
    private const int PlainNumberDigitsCounted = 20;

    private const EdgeCases NumberCategories = EdgeCases.BigNumbers | EdgeCases.LeadingZeros;

    /// <summary>
    /// The values where a comparer of Numbers is most likely to be wrong: around 2^32, 2^63 and 2^64. The last one is
    /// the first Number that does not fit in 64 bits.
    /// </summary>
    private static readonly UInt128[] BoundaryNumbers =
        [4294967296, 9223372036854775807, 9223372036854775808, LineFormat.MaxNumber, (UInt128)LineFormat.MaxNumber + 1];

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

        int numberDigits = (_enabled & NumberCategories) != 0 ? MaxNumberDigits : PlainNumberDigitsCounted;
        int carriageReturn = (_enabled & EdgeCases.CarriageReturn) != 0 ? 1 : 0;
        MaxLineLength = numberDigits + LineFormat.SeparatorLength + _strings.MaxLength + carriageReturn
            + _lineEnding.TerminatorLength();
    }

    /// <summary>The length of the longest line that these options can produce, terminator included.</summary>
    public int MaxLineLength { get; }

    /// <summary>
    /// Writes one line with its terminator and returns its length. The destination needs
    /// <see cref="MaxLineLength"/> plus <see cref="StringComposer.Slack"/> bytes. The first line of a file never gets
    /// a carriage return at the end of its String, so that the line ending of a file can be told from that line.
    /// </summary>
    public int WriteLine(ref Xoshiro256 random, Span<byte> destination, bool firstLineOfFile)
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

        // The carriage return is decided for the line, so a pool String gets it too. Before a \r\n terminator the line
        // ends with \r\r\n, which a reader with that line ending takes as a String that ends with \r.
        if ((applied & EdgeCases.CarriageReturn) != 0 && !firstLineOfFile)
        {
            destination[position++] = LineFormat.CarriageReturn;
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
        UInt128 number = (applied & EdgeCases.BigNumbers) == 0 ? PlainNumber(head) : BigNumber(ref random, roll);

        // One to three zeros, about equally likely.
        int zeros = (applied & EdgeCases.LeadingZeros) == 0
            ? 0
            : 1 + RandomBits.Pick(roll, ZerosFirstBit, ZerosBits, MaxLeadingZeros);

        return LineWriter.WriteNumber(destination, number, zeros);
    }

    private static UInt128 BigNumber(ref Xoshiro256 random, ulong roll)
    {
        switch (RandomBits.Bits(roll, BigKindFirstBit, BigKindBits))
        {
            case BoundaryKind:
                // One case of 8: one of the five boundary values, each about equally likely.
                int boundary = RandomBits.Pick(roll, BoundaryIndexFirstBit, BoundaryIndexBits, BoundaryNumbers.Length);
                return BoundaryNumbers[boundary];

            case LongKind:
                // One case of 8: a Number of 21 to 30 digits, every count about equally likely. Even the smallest,
                // 100000000000000000000, is above the largest Number of 64 bits.
                int digits = MinLongDigits
                    + RandomBits.Pick(roll, LongDigitsFirstBit, LongDigitsBits, MaxLongDigits - MinLongDigits + 1);
                return LongNumber(ref random, digits);

            default:
                // Six cases of 8: a Number of exactly 33 to 64 bits, so it never fits in 32 bits: with 33 bits it is
                // from 2^32 = 4294967296 to 2^33 - 1.
                int bitLength = 33 + RandomBits.Bits(roll, BigLengthFirstBit, BigLengthBits);
                return RandomBits.WithBitLength(random.Next(), bitLength);
        }
    }

    // A Number of exactly the given count of digits: the smallest one, 10^(digits - 1), plus a random amount below
    // 9 × 10^(digits - 1), which is how many Numbers have that count of digits. With 21 digits it lies from
    // 100000000000000000000 to 999999999999999999999. The amount is the remainder of a random 128-bit value, made of
    // two random values. A remainder favours some amounts over others, here by at most 9 × 10^29 / 2^128, less than
    // one in 300 million.
    private static UInt128 LongNumber(ref Xoshiro256 random, int digits)
    {
        // 10^(digits - 1) by multiplication: BigInteger.Pow would allocate on every call, and a block allocates
        // nothing.
        UInt128 smallest = 1;
        for (int digit = 1; digit < digits; digit++)
        {
            smallest *= 10;
        }

        UInt128 random128 = new(random.Next(), random.Next());
        return smallest + (random128 % (9 * smallest));
    }
}
