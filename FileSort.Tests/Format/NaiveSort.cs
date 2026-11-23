using System.Text;

namespace FileSort.Tests.Format;

/// <summary>
/// A sort of a whole file in memory, built on the naive parser and the naive comparison. It is what a correct
/// sorter must produce, made in the simplest way: the tests of the checker get their sorted files from it.
/// </summary>
internal static class NaiveSort
{
    /// <summary>
    /// The lines in the order of <c>sort-order</c>. The sort is stable: lines with equal keys keep the order they
    /// had, so the result is the same on every run.
    /// </summary>
    public static List<NaiveLine> Sort(IEnumerable<NaiveLine> lines)
    {
        return [.. lines.Order(NaiveLineComparer.Instance)];
    }

    /// <summary>Whether no line comes before its predecessor in the order of <c>sort-order</c>.</summary>
    public static bool IsSorted(IReadOnlyList<NaiveLine> lines)
    {
        for (int i = 1; i < lines.Count; i++)
        {
            if (NaiveLineComparer.Instance.Compare(lines[i - 1], lines[i]) > 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The sorted version of a valid file: the same lines, byte for byte, in sorted order, every line followed by
    /// the terminator. A byte order mark of the input is not carried over.
    /// </summary>
    public static byte[] SortFile(byte[] file, string terminator)
    {
        return Join(Sort(NaiveLineParser.ParseValidFile(file, terminator)), terminator);
    }

    /// <summary>The bytes of a file with these lines, every line followed by the terminator.</summary>
    public static byte[] Join(IEnumerable<NaiveLine> lines, string terminator)
    {
        byte[] end = Encoding.ASCII.GetBytes(terminator);
        using MemoryStream file = new();
        foreach (NaiveLine line in lines)
        {
            file.Write(line.ToBytes());
            file.Write(end);
        }

        return file.ToArray();
    }
}
