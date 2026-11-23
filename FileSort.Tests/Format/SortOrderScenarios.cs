namespace FileSort.Tests.Format;

/// <summary>
/// The scenarios of the <c>sort-order</c> spec that give lines and their order, as data: the naive sort and the
/// checker's comparison are both tested on the same rows. In every row all the lines have different keys, so the
/// expected order is the only sorted one.
/// </summary>
internal static class SortOrderScenarios
{
    /// <summary>The name of the scenario, its lines as the scenario lists them, and the same lines in sorted order.</summary>
    public static TheoryData<string, string[], string[]> Orders => new()
    {
        // Requirement: Order by String
        {
            "Different Strings",
            ["30432. Something something something", "32. Cherry is the best", "2. Banana is yellow"],
            ["2. Banana is yellow", "32. Cherry is the best", "30432. Something something something"]
        },
        { "Beginning of a longer String", ["5. Apple pie", "9. Apple"], ["9. Apple", "5. Apple pie"] },
        { "Empty String", ["5. Apple", "9. "], ["9. ", "5. Apple"] },
        { "Letter case", ["1. apple", "1. Banana"], ["1. Banana", "1. apple"] },
        { "Trailing space", ["1. Apple ", "1. Apple"], ["1. Apple", "1. Apple "] },

        // U+FF21 is the bytes EF BC A1 and one UTF-16 code unit, FF21. U+1F600 is the bytes F0 9F 98 80 and the
        // UTF-16 code units D83D DE00, which are smaller than FF21.
        { "UTF-8 bytes, not UTF-16 code units", ["1. \U0001F600", "1. Ａ"], ["1. Ａ", "1. \U0001F600"] },

        // U+0439 is the bytes D0 B9; U+0438 U+0306 is D0 B8 CC 86 and looks the same on screen.
        { "No normalization", ["1. й", "1. й"], ["1. й", "1. й"] },

        // Requirement: Order by Number
        { "Value, not text", ["10. Apple", "9. Apple"], ["9. Apple", "10. Apple"] },
        { "Leading zeros", ["8. Apple", "007. Apple", "6. Apple"], ["6. Apple", "007. Apple", "8. Apple"] },
        {
            "Numbers above 64 bits",
            ["18446744073709551616. Apple", "100000000000000000000000000000. Apple", "18446744073709551615. Apple"],
            ["18446744073709551615. Apple", "18446744073709551616. Apple", "100000000000000000000000000000. Apple"]
        },
        { "String first, then Number", ["1. Banana", "999. Apple"], ["999. Apple", "1. Banana"] },
        {
            "Example of the task",
            [
                "415. Apple", "30432. Something something something", "1. Apple", "32. Cherry is the best",
                "2. Banana is yellow",
            ],
            [
                "1. Apple", "415. Apple", "2. Banana is yellow", "32. Cherry is the best",
                "30432. Something something something",
            ]
        },
    };
}
