namespace FileSort.Engine.Lines;

/// <summary>
/// The order of lines, compared through values that stand for them: the entries of the index, or the heads of a
/// merge, which carry more of the key. A merge is written once for both; a struct that implements this is called
/// directly, without the call through an interface, because the merge is compiled for each such struct apart.
/// </summary>
/// <typeparam name="T">What stands for a line.</typeparam>
internal interface ILineOrder<T>
    where T : struct
{
    /// <summary>Negative when the line of <paramref name="x"/> comes first, positive when that of
    /// <paramref name="y"/> does, 0 for identical lines.</summary>
    int Compare(in T x, in T y);
}
