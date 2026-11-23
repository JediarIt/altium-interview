namespace FileSort.Tests.Format;

/// <summary>
/// The order of the <c>sort-order</c> spec in the plainest form: a loop over the bytes of the two Strings, then the
/// values of the Numbers. It shares no code with the checker or with <c>FileSort.Common</c>, so it is the oracle for
/// their comparisons.
/// </summary>
internal sealed class NaiveLineComparer : IComparer<NaiveLine>
{
    public static NaiveLineComparer Instance { get; } = new();

    public int Compare(NaiveLine? x, NaiveLine? y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        int strings = CompareStrings(x.Text, y.Text);
        return strings != 0 ? strings : x.Number.CompareTo(y.Number);
    }

    // Byte by byte, as unsigned bytes; a String that is the beginning of a longer one comes first.
    private static int CompareStrings(byte[] x, byte[] y)
    {
        for (int i = 0; i < x.Length && i < y.Length; i++)
        {
            if (x[i] != y[i])
            {
                return x[i] < y[i] ? -1 : 1;
            }
        }

        return x.Length.CompareTo(y.Length);
    }
}
