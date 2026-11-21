namespace FileSort.Generator.Content.Words;

/// <summary>
/// The words that the <c>unicode</c> edge case puts in place of dictionary words. No word contains an ASCII
/// character. Together they cover every UTF-8 length above one byte and the pairs that separate comparers:
/// U+FF21 against U+1F600 orders differently by UTF-8 bytes and by UTF-16 code units, and the last two words are
/// one word spelled with a precomposed and with a decomposed letter. The list is part of the content version.
/// </summary>
internal static class NonAsciiWords
{
    /// <summary>The number of words: a word index is 5 random bits.</summary>
    public const int WordCount = 32;

    /// <summary>The number of random bits that select a word.</summary>
    public const int IndexBits = 5;

    /// <summary>The length of the longest word in bytes.</summary>
    public const int MaxWordLength = 21;

    private static readonly byte[][] Words =
    [
        // Two bytes per character: Cyrillic, Greek, Hebrew, Arabic.
        "яблоко"u8.ToArray(),
        "вишня"u8.ToArray(),
        "Ёлка"u8.ToArray(),
        "ёж"u8.ToArray(),
        "щука"u8.ToArray(),
        "αλφα"u8.ToArray(),
        "Ωμέγα"u8.ToArray(),
        "λόγος"u8.ToArray(),
        "שלום"u8.ToArray(),
        "سلام"u8.ToArray(),

        // Three bytes per character: CJK, kana, hangul, Thai, Georgian, a currency sign.
        "日本"u8.ToArray(),
        "東京"u8.ToArray(),
        "中文"u8.ToArray(),
        "한국"u8.ToArray(),
        "ありがとう"u8.ToArray(),
        "カタカナ"u8.ToArray(),
        "ไทย"u8.ToArray(),
        "ქართული"u8.ToArray(),
        "€"u8.ToArray(),

        // Fullwidth forms, three bytes each, near the top of the Basic Multilingual Plane. The first is U+FF21 alone.
        "Ａ"u8.ToArray(),
        "ＡＢＣ"u8.ToArray(),
        "ａｐｐｌｅ"u8.ToArray(),

        // Four bytes per character: outside the Basic Multilingual Plane. The first is U+1F600 alone.
        "\U0001F600"u8.ToArray(),
        "\U0001F600\U0001F601"u8.ToArray(),
        "\U0001F34E"u8.ToArray(),
        "\U0001F352"u8.ToArray(),
        "\U0001F680"u8.ToArray(),
        "\U0001F389"u8.ToArray(),
        "\U0001D49C"u8.ToArray(),
        "\U00010348"u8.ToArray(),

        // One word in two spellings: U+0439 precomposed, and decomposed into U+0438 U+0306.
        "йод"u8.ToArray(),
        "йод"u8.ToArray(),
    ];

    /// <summary>The bytes of one word.</summary>
    public static ReadOnlySpan<byte> Word(int index)
    {
        return Words[index];
    }
}
