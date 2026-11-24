using System.Text;

using FileSort.Checker.Lines;

namespace FileSort.Tests.Checker.Lines;

/// <summary>One line as the reader gave it, with what the reader says about it and what the parser makes of
/// it.</summary>
internal sealed record ReadLine(LineRead Read, byte[] Bytes, long Number, long Offset, bool HasTerminator)
{
    public string Text => Encoding.UTF8.GetString(Bytes);

    /// <summary>Why the line is not valid, for the reader and the parser together.</summary>
    public LineFault Fault => Read == LineRead.BareLineFeed ? LineFault.BareLineFeed : LineParser.Parse(Bytes, out _);

    /// <summary>The String of a valid line.</summary>
    public byte[] StringBytes
    {
        get
        {
            Assert.Equal(LineFault.None, LineParser.Parse(Bytes, out int numberLength));
            return LineParser.Text(Bytes, numberLength).ToArray();
        }
    }

    public bool Equals(ReadLine? other)
    {
        return other is not null && Read == other.Read && Bytes.AsSpan().SequenceEqual(other.Bytes)
            && Number == other.Number && Offset == other.Offset && HasTerminator == other.HasTerminator;
    }

    public override int GetHashCode() => HashCode.Combine(Read, Bytes.Length, Number, Offset, HasTerminator);
}
