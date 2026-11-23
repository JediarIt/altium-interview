using System.Text;

using FileSort.Generator.Content;
using FileSort.Tests.Format;
using FileSort.Tests.Generator;

using CommonLineEnding = FileSort.Common.LineEnding;

namespace FileSort.Tests.Checker.Checks;

/// <summary>
/// An input file and its correctly sorted lines, made without any code of the checker: the generator writes the
/// input, the tests add lines that the generator cannot write yet, and the naive sort orders them. A test takes the
/// sorted lines, breaks them in one way and joins them into an output.
/// </summary>
internal sealed class SortedPair
{
    /// <summary>
    /// Lines that the generator does not write: Numbers above 64 bits, Numbers with many leading zeros, Strings
    /// that end with a carriage return or a space, lines with equal keys and different bytes, and Strings whose
    /// order differs between the right comparison and the wrong ones (letter case, UTF-16 code units).
    /// </summary>
    public static readonly string[] HandWrittenLines =
    [
        "18446744073709551616. Apple",
        "100000000000000000000000000000. Apple",
        "000000000000000000000000000000000000007. Apple",
        "18446744073709551615. Apple",
        "007. Apple",
        "7. Apple",
        "5. Apple\r",
        "5. Apple ",
        "1. apple",
        "1. Banana",
        "1. Ａ",
        "1. \U0001F600",
    ];

    public SortedPair(byte[] input, string terminator)
    {
        Input = input;
        Terminator = terminator;
        Lines = NaiveSort.Sort(NaiveLineParser.ParseValidFile(input, terminator));
    }

    public byte[] Input { get; }

    public string Terminator { get; }

    /// <summary>The lines of the input in sorted order.</summary>
    public List<NaiveLine> Lines { get; }

    /// <summary>The correct output: the sorted lines, each followed by the terminator.</summary>
    public byte[] Output => Join(Lines);

    /// <summary>
    /// A megabyte of generated content with the hand-written lines after it. The size is the one that the design
    /// names for the pairs of the tests.
    /// </summary>
    public static SortedPair Generated(EdgeCases edgeCases, CommonLineEnding lineEnding, ulong seed = 11)
    {
        string terminator = lineEnding == CommonLineEnding.CrLf ? "\r\n" : "\n";
        byte[] generated = TestContent.Generate(
            TestContent.Options(seed: seed, edgeCases: edgeCases, lineEnding: lineEnding), TestContent.Megabyte);
        byte[] added = Encoding.UTF8.GetBytes(string.Concat(HandWrittenLines.Select(line => line + terminator)));
        return new SortedPair([.. generated, .. added], terminator);
    }

    public byte[] Join(IEnumerable<NaiveLine> lines)
    {
        return NaiveSort.Join(lines, Terminator);
    }

    /// <summary>A file of these raw lines, each followed by the terminator; a line here may be anything.</summary>
    public byte[] JoinRaw(IEnumerable<byte[]> lines)
    {
        byte[] end = Encoding.ASCII.GetBytes(Terminator);
        return [.. lines.SelectMany(line => (byte[])[.. line, .. end])];
    }

    /// <summary>A copy of the sorted lines as raw bytes, for a test to break.</summary>
    public List<byte[]> RawLines()
    {
        return Lines.ConvertAll(line => line.ToBytes());
    }
}
