using System.Runtime.CompilerServices;
using System.Text;

using FileSort.Common;
using FileSort.Generator.Content.Words;
using FileSort.Generator.Random;

namespace FileSort.Generator.Content.Lines;

/// <summary>
/// Composes the String of a line straight into a span: dictionary words joined by single spaces, the first one
/// capitalized, plus the edge cases that change a String. The way random bits are spent here is part of the content
/// version.
/// </summary>
internal sealed class StringComposer
{
    /// <summary>
    /// Free bytes that a destination needs after the longest String: a word is copied as a whole slot of the
    /// dictionary, whatever its length.
    /// </summary>
    public const int Slack = Dictionary.SlotSize;

    // The first random value of a String: bits 0 to 13 choose the number of words, and the other 50 bits hold the
    // first five words, 10 bits each (a dictionary of 1024 words takes 10 bits per word). Every next value holds six
    // words in its lowest 60 bits; its top 4 bits are not used.
    private const int CountBits = 14;

    // The longest prefix that the separator category puts in front of the words: two digits and the separator.
    private const int MaxPrefixLength = 2 + LineFormat.SeparatorLength;

    // The bit that tells a lower-case ASCII letter from an upper-case one, see Capitalize.
    private const byte CaseBit = 0x20;

    private readonly int _minWords;
    private readonly int _wordCounts;
    private readonly EdgeCases _enabled;

    public StringComposer(ContentOptions options)
    {
        _minWords = options.Words.Min;
        _wordCounts = options.Words.Max - options.Words.Min + 1;
        _enabled = options.EdgeCases;

        int maxWord = _enabled.HasFlag(EdgeCases.Unicode) ? NonAsciiWords.MaxWordLength : Dictionary.MaxWordLength;
        int maxWords = options.Words.Max;
        MaxLength = (maxWords * maxWord) + (maxWords - 1);
        if (_enabled.HasFlag(EdgeCases.Separator))
        {
            // Either a prefix in front of the words, or one space between two words that becomes the separator.
            MaxLength += MaxPrefixLength;
        }
    }

    /// <summary>The length of the longest String that these options can produce, in bytes.</summary>
    public int MaxLength { get; }

    /// <summary>
    /// Writes one String and returns its length. The destination needs <see cref="MaxLength"/> plus
    /// <see cref="Slack"/> bytes. <paramref name="applied"/> holds the edge cases that were rolled for this String.
    /// </summary>
    public int Compose(ref Xoshiro256 random, EdgeCases applied, Span<byte> destination)
    {
        if ((applied & EdgeCases.Empty) != 0)
        {
            return 0;
        }

        // The number of words: one of the counts from the minimum to the maximum. With the default range of 1 to 5
        // there are five counts, each with a probability of about 1/5.
        ulong bits = random.Next();
        int words = _minWords + RandomBits.Pick(bits, 0, CountBits, _wordCounts);

        if ((applied & (EdgeCases.Unicode | EdgeCases.Separator | EdgeCases.Case)) != 0)
        {
            return ComposeUnusual(ref random, applied, words, destination);
        }

        ReadOnlySpan<byte> slots = Dictionary.Slots;
        ReadOnlySpan<byte> lengths = Dictionary.Lengths;
        int position = 0;

        // Where the index of the next word starts in the current random value.
        int firstBit = CountBits;

        for (int word = 0; word < words; word++)
        {
            // The current value has no 10 bits left for another word: take the next value.
            if (firstBit + Dictionary.IndexBits > 64)
            {
                bits = random.Next();
                firstBit = 0;
            }

            int index = RandomBits.Bits(bits, firstBit, Dictionary.IndexBits);
            firstBit += Dictionary.IndexBits;

            // A whole slot is copied: one fixed-size move instead of a copy whose length is known only at run time.
            // The bytes after the word are overwritten by what follows.
            slots.Slice(index * Dictionary.SlotSize, Dictionary.SlotSize)
                .CopyTo(destination.Slice(position, Dictionary.SlotSize));
            position += lengths[index];
            destination[position] = (byte)' ';
            position++;
        }

        // "apple pie " becomes "Apple pie": the space after the last word is not part of the String.
        Capitalize(destination);
        return position - 1;
    }

    // The rare path: at least one of the categories that change the words applies. Examples for the three words
    // "apple pie crust" drawn from the dictionary:
    //
    //   separator, inside         "Apple. pie crust"     one of the spaces between the words becomes ". "
    //   separator, prefix         "42. Apple pie crust"  a Number from 0 to 99 and ". " come first
    //   separator, leading space  " Apple pie crust"     the String begins with a space
    //   unicode                   "Apple яблоко crust"   some words are replaced with words without ASCII letters
    //   case                      "apple PIE Crust"      each word is lower case, capitalized or upper case
    //
    // The categories combine: "42. apple 日本 CRUST" has all three.
    //
    // One random value, the shape, decides what concerns the whole String:
    //   bits 0 to 7     the form of the separator: one of three, or one of the last two for a String of one word
    //   bits 8 to 15    the Number of the prefix form
    //   bits 16 to 31   the word after which the inside form puts the separator
    //   bits 32 to 47   the word that the unicode category always replaces, so at least one word is non-ASCII
    // Then each word takes one random value of its own:
    //   bits 0 to 9     the dictionary word
    //   bit 10          whether the unicode category replaces this word too: a probability of 1/2
    //   bits 11 to 15   the non-ASCII word, one of 32
    //   bits 16 to 23   the case of the word, under the case category
    // The bits that are not listed are not used.
    private static int ComposeUnusual(ref Xoshiro256 random, EdgeCases applied, int words, Span<byte> destination)
    {
        ulong shape = random.Next();
        int position = 0;

        // The word after which the separator goes; -1 means no separator inside the String.
        int separatorGap = -1;
        if ((applied & EdgeCases.Separator) != 0)
        {
            // The form: 0 inside, 1 prefix, 2 leading space, about 1/3 each. A String of one word has no space to
            // replace, so it takes one of the last two.
            int form = words == 1 ? 1 + RandomBits.Pick(shape, 0, 8, 2) : RandomBits.Pick(shape, 0, 8, 3);
            switch (form)
            {
                case 0:
                    // Inside: one of the words - 1 spaces, so a word from 0 to words - 2. The loop below writes the
                    // separator after that word instead of a space: for three words, "Apple. pie crust" or
                    // "Apple pie. crust", never ". " at the end.
                    separatorGap = RandomBits.Pick(shape, 16, 16, words - 1);
                    break;
                case 1:
                    // Prefix: a Number from 0 to 99, written before the words with ". " after it.
                    position += LineWriter.WriteNumber(destination, (ulong)RandomBits.Pick(shape, 8, 8, 100));
                    position += LineWriter.WriteSeparator(destination[position..]);
                    break;
                default:
                    // Leading space.
                    destination[position++] = (byte)' ';
                    break;
            }
        }

        // The unicode category replaces each word with probability 1/2, and this one always. -1 when the category does
        // not apply, and then no word is replaced.
        int forcedNonAscii = (applied & EdgeCases.Unicode) != 0 ? RandomBits.Pick(shape, 32, 16, words) : -1;

        for (int word = 0; word < words; word++)
        {
            // The word's own random value; its bits are listed above the method.
            ulong bits = random.Next();

            if (word == forcedNonAscii || ((applied & EdgeCases.Unicode) != 0 && RandomBits.IsSet(bits, 10)))
            {
                // A non-ASCII word, copied as it is: its case never changes.
                ReadOnlySpan<byte> nonAscii = NonAsciiWords.Word(RandomBits.Bits(bits, 11, NonAsciiWords.IndexBits));
                nonAscii.CopyTo(destination[position..]);
                position += nonAscii.Length;
            }
            else
            {
                int index = RandomBits.Bits(bits, 0, Dictionary.IndexBits);
                Span<byte> written = destination.Slice(position, Dictionary.Lengths[index]);
                Dictionary.Word(index).CopyTo(written);
                position += written.Length;

                // 0 keeps lower case ("crust"), 1 capitalizes ("Crust"), 2 makes upper case ("CRUST"). Under the case
                // category each is chosen with a probability of about 1/3. Without it the first word is capitalized
                // and the others stay lower case, as in a plain String.
                int letterCase = (applied & EdgeCases.Case) != 0
                    ? RandomBits.Pick(bits, 16, 8, 3)
                    : word == 0 ? 1 : 0;
                if (letterCase == 1)
                {
                    Capitalize(written);
                }
                else if (letterCase == 2)
                {
                    Ascii.ToUpperInPlace(written, out _);
                }
            }

            // After the word: the separator after the chosen word, a space after any other, nothing after the last.
            if (word == separatorGap)
            {
                position += LineWriter.WriteSeparator(destination[position..]);
            }
            else if (word < words - 1)
            {
                destination[position++] = (byte)' ';
            }
        }

        return position;
    }

    // Makes the first letter of a lower-case word upper case: "crust" becomes "Crust". 'c' is 0x63 and 'C' is 0x43:
    // a lower-case ASCII letter and its upper-case form differ only in the bit 0x20, and the exclusive or flips it.
    // The dictionary holds lower-case words only. This runs once per String, so it does not call
    // Ascii.ToUpperInPlace, which is not inlined: CapitalLetterBenchmarks measured 2.2 ns per word with the bit
    // against 5.3 ns with the framework, the copy of the word included. A whole word in upper case is rare and goes
    // through the framework.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Capitalize(Span<byte> word)
    {
        word[0] ^= CaseBit;
    }
}
