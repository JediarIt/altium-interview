using System.Text;

using FileSort.Generator.Content.Words;

namespace FileSort.Tests.Generator.Content.Words;

public class WordListTests
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static List<string> DictionaryWords()
    {
        return [.. Enumerable.Range(0, Dictionary.WordCount).Select(i => StrictUtf8.GetString(Dictionary.Word(i)))];
    }

    private static List<string> NonAsciiWordList()
    {
        return [.. Enumerable.Range(0, NonAsciiWords.WordCount).Select(i => StrictUtf8.GetString(NonAsciiWords.Word(i)))];
    }

    [Fact]
    public void The_dictionary_has_exactly_1024_distinct_words()
    {
        List<string> words = DictionaryWords();

        Assert.Equal(1024, Dictionary.WordCount);
        Assert.Equal(1 << Dictionary.IndexBits, Dictionary.WordCount);
        Assert.Equal(1024, words.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Dictionary_words_are_lower_case_letters_a_to_z_of_at_most_12_letters()
    {
        Assert.Equal(12, Dictionary.MaxWordLength);
        Assert.All(DictionaryWords(), word =>
        {
            Assert.InRange(word.Length, 1, Dictionary.MaxWordLength);
            Assert.All(word, letter => Assert.InRange(letter, 'a', 'z'));
        });
    }

    [Fact]
    public void The_dictionary_includes_the_words_of_the_task_examples()
    {
        List<string> words = DictionaryWords();

        Assert.All(
            ["apple", "something", "cherry", "is", "the", "best", "banana", "yellow"],
            word => Assert.Contains(word, words));
    }

    [Fact]
    public void Every_dictionary_slot_holds_its_word_padded_with_zeros()
    {
        Assert.Equal(Dictionary.WordCount * Dictionary.SlotSize, Dictionary.Slots.Length);
        for (int i = 0; i < Dictionary.WordCount; i++)
        {
            ReadOnlySpan<byte> slot = Dictionary.Slots.Slice(i * Dictionary.SlotSize, Dictionary.SlotSize);
            int length = Dictionary.Lengths[i];

            Assert.True(slot[..length].SequenceEqual(Dictionary.Word(i)));
            Assert.Equal(-1, slot[length..].IndexOfAnyExcept((byte)0));
        }
    }

    [Fact]
    public void The_non_ASCII_list_has_32_distinct_words_without_ASCII_characters()
    {
        List<string> words = NonAsciiWordList();

        Assert.Equal(32, NonAsciiWords.WordCount);
        Assert.Equal(1 << NonAsciiWords.IndexBits, NonAsciiWords.WordCount);
        Assert.Equal(32, words.Distinct(StringComparer.Ordinal).Count());
        for (int i = 0; i < NonAsciiWords.WordCount; i++)
        {
            Assert.Equal(-1, NonAsciiWords.Word(i).IndexOfAnyInRange((byte)0x00, (byte)0x7F));
        }
    }

    [Fact]
    public void Non_ASCII_words_cover_UTF8_sequences_of_2_3_and_4_bytes()
    {
        HashSet<int> lengths = [];
        int longest = 0;
        foreach (string word in NonAsciiWordList())
        {
            longest = Math.Max(longest, Encoding.UTF8.GetByteCount(word));
            foreach (Rune rune in word.EnumerateRunes())
            {
                lengths.Add(rune.Utf8SequenceLength);
            }
        }

        Assert.Equal([2, 3, 4], lengths.Order());
        Assert.Equal(NonAsciiWords.MaxWordLength, longest);
    }

    [Fact]
    public void Non_ASCII_words_include_the_characters_that_separate_comparers()
    {
        List<string> words = NonAsciiWordList();

        // U+FF21 sorts after U+1F600 by UTF-16 code units and before it by UTF-8 bytes.
        Assert.Contains("Ａ", words);
        Assert.Contains("\U0001F600", words);

        // One word, spelled with U+0439 and with U+0438 U+0306.
        Assert.Contains("йод", words);
        Assert.Contains("йод", words);
    }
}
