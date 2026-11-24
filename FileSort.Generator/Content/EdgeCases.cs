namespace FileSort.Generator.Content;

/// <summary>
/// Categories of valid but unusual lines. Without any of them the output is plain: canonical Numbers that fit in 32
/// bits and sentence-case ASCII words, which every reasonable comparer orders the same way.
/// </summary>
[Flags]
internal enum EdgeCases
{
    None = 0,

    /// <summary>Words without ASCII characters, in UTF-8 sequences of 2, 3 and 4 bytes.</summary>
    Unicode = 1 << 0,

    /// <summary>Strings that contain the separator, begin with digits and the separator, or begin with a
    /// space.</summary>
    Separator = 1 << 1,

    /// <summary>Words in lower case, capitalized and in upper case, in any position.</summary>
    Case = 1 << 2,

    /// <summary>
    /// Numbers above 4294967295: the values around 2^32, 2^63 and 2^64, Numbers of 33 to 64 bits, and Numbers of 21
    /// to 30 digits, which do not fit in 64 bits.
    /// </summary>
    BigNumbers = 1 << 3,

    /// <summary>Numbers with zeros in front.</summary>
    LeadingZeros = 1 << 4,

    /// <summary>Empty Strings.</summary>
    Empty = 1 << 5,

    /// <summary>Strings that end with a carriage return. The first line of a file never gets one.</summary>
    CarriageReturn = 1 << 6,

    All = Unicode | Separator | Case | BigNumbers | LeadingZeros | Empty | CarriageReturn,
}
