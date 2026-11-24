namespace FileSort.Tests.Format;

/// <summary>
/// Byte arrays compared byte by byte, as unsigned bytes, the shorter first when one is the beginning of the other:
/// the order in which the sorter writes lines with equal keys.
/// </summary>
internal sealed class ByteOrder : IComparer<byte[]>
{
    public static ByteOrder Instance { get; } = new();

    public int Compare(byte[]? x, byte[]? y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

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
