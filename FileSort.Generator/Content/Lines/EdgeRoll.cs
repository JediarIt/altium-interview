using System.Runtime.CompilerServices;

using FileSort.Generator.Random;

namespace FileSort.Generator.Content.Lines;

/// <summary>
/// Decides which enabled categories apply to one line or one String: each with probability 1/16, independently of
/// the others, from four bits of one random value per category. The layout of the bits is part of the content
/// version.
/// </summary>
internal static class EdgeRoll
{
    /// <summary>The number of categories; category <c>i</c> owns the bits <c>4i</c> to <c>4i + 3</c>.</summary>
    public const int CategoryCount = 7;

    /// <summary>The first bit of the roll that the categories do not use.</summary>
    public const int FreeBitsStart = CategoryCount * BitsPerCategory;

    private const int BitsPerCategory = 4;

    public static EdgeCases Apply(ulong roll, EdgeCases enabled)
    {
        // In hexadecimal every digit is 4 bits, so category i owns digit i of the roll, counting from the right:
        // unicode the last digit, separator the one before it, and so on. A category applies when its digit is 0, one
        // of 16 equally likely digits. The roll 0x...F3A0 applies unicode only: its last digit is 0, the next ones are
        // A, 3 and F.
        EdgeCases applied = EdgeCases.None;
        for (int category = 0; category < CategoryCount; category++)
        {
            if (RandomBits.Bits(roll, category * BitsPerCategory, BitsPerCategory) == 0)
            {
                applied |= Flag(category);
            }
        }

        // The categories that came up but are not enabled are dropped.
        return applied & enabled;
    }

    // The flag of category i is bit i of EdgeCases, as the enum declares it: 1 << 0 is Unicode, 1 << 1 is Separator,
    // and so on. The order of the enum is therefore part of the content version. A table of the flags would read the
    // same, but the machine code of Apply doubled with it, and Apply runs once per line.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static EdgeCases Flag(int category)
    {
        return (EdgeCases)(1 << category);
    }
}
