using System.Globalization;

using FileSort.Engine.Lines;

namespace FileSort.Benchmarks.Micro.Sorter.Candidates;

/// <summary>
/// An index entry of 24 bytes, the entry that D6 of the design planned and M1 measured against the engine's entry of
/// 16 bytes: the prefix of the String, the value of the Number when it fits in 64 bits, the offset and the shape. Lines
/// with equal Strings compare their Numbers as integers when both fit, without reading the digits.
/// </summary>
/// <param name="Prefix">The first 8 bytes of the String, as in the engine's entry.</param>
/// <param name="Number">The value of the Number when it fits in 64 bits, otherwise 0.</param>
/// <param name="Offset">Where the line starts in the buffer.</param>
/// <param name="Shape">The shape of the engine's entry, and bit 31 set when the Number fits in 64 bits.</param>
internal readonly record struct WideEntry(ulong Prefix, ulong Number, uint Offset, uint Shape)
{
    private const uint FitsBit = 1u << 31;

    public bool NumberFits => (Shape & FitsBit) != 0;

    /// <summary>The wide entry of a line whose engine entry is given.</summary>
    public static WideEntry From(IndexEntry entry, byte[] lines)
    {
        // Leading zeros parse; a value above 64 bits does not.
        ReadOnlySpan<byte> digits = lines.AsSpan((int)entry.Offset, EntryShape.StoredNumberLength(entry.Shape));
        bool fits = ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value);
        return new WideEntry(entry.Prefix, fits ? value : 0, entry.Offset, entry.Shape | (fits ? FitsBit : 0));
    }
}
