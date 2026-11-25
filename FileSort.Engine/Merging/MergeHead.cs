using System.Buffers.Text;

using FileSort.Common;
using FileSort.Engine.Lines;

namespace FileSort.Engine.Merging;

/// <summary>
/// The current line of one source of a merge: its index entry and, beside it, more of its key, so that two heads
/// compare as numbers without reading their lines. The heads of a merge are neighbours in the sorted order, and such
/// lines mostly agree in the 8 bytes of String that an entry holds: of the neighbours in a sorted file of default
/// content, 1 in 100 differs there, and more than half have equal Strings. A head therefore holds the first 24 bytes
/// of the String and the value of the Number.
/// <para>
/// An entry of the index has no room for this: there is one for every line of a chunk, and 8 bytes more in each mean
/// a fifth fewer lines in a chunk. A merge has one head for every source, a few hundred at most.
/// </para>
/// </summary>
internal readonly struct MergeHead
{
    /// <summary>The bytes of a String that a head holds: the 8 of its entry and 16 more.</summary>
    public const int KeyLength = 3 * StringPrefix.Length;

    /// <param name="entry">The entry of the line.</param>
    /// <param name="line">The line, without its terminator.</param>
    public MergeHead(in IndexEntry entry, ReadOnlySpan<byte> line)
    {
        int digits = entry.NumberLength(line);
        ReadOnlySpan<byte> text = line[(digits + LineFormat.SeparatorLength)..];
        bool parsed = Utf8Parser.TryParse(line[..digits], out ulong number, out int consumed);

        Entry = entry;
        StringLength = text.Length;
        Second = KeyPart(text, StringPrefix.Length);
        Third = KeyPart(text, 2 * StringPrefix.Length);
        NumberFits = parsed && consumed == digits;
        Number = number;
    }

    private MergeHead(in IndexEntry entry)
    {
        Entry = entry;
        StringLength = int.MaxValue;
    }

    /// <summary>
    /// A head that carries its entry and no more of the key: whatever the prefixes of two such heads leave open is
    /// decided by reading the lines, as entries compare. Making the key costs about 22 ns a line and saves about 15 ns
    /// in a comparison, so it pays only where a line meets several others on its way through the tree: a merge of a
    /// few runs uses these heads. All heads of one merge are of one kind.
    /// </summary>
    public static MergeHead WithoutKey(in IndexEntry entry)
    {
        return new MergeHead(in entry);
    }

    public IndexEntry Entry { get; }

    /// <summary>The bytes 8 to 15 of the String, as the prefix of the entry holds the bytes 0 to 7.</summary>
    public ulong Second { get; }

    /// <summary>The bytes 16 to 23 of the String.</summary>
    public ulong Third { get; }

    /// <summary>The value of the Number, when <see cref="NumberFits"/>.</summary>
    public ulong Number { get; }

    /// <summary>
    /// The bytes of the String. A head without a key says more than any String has, so that two of them are never
    /// taken for equal Strings by their lengths.
    /// </summary>
    public int StringLength { get; }

    /// <summary>Whether the value of the Number fits in 64 bits. A longer Number is compared by its digits.</summary>
    public bool NumberFits { get; }

    // The 8 bytes of the String from the offset on, padded with zero bytes like a prefix; 0 for a String that ends
    // before them.
    private static ulong KeyPart(ReadOnlySpan<byte> text, int offset)
    {
        return text.Length > offset ? StringPrefix.Read(text[offset..]) : 0;
    }
}
