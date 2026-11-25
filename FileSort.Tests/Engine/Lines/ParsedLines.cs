using System.Text;

using FileSort.Engine.Lines;
using FileSort.Engine.Lines.Parsing;
using FileSort.Engine.Merging;

namespace FileSort.Tests.Engine.Lines;

/// <summary>
/// Lines laid out one after another in one buffer, each followed by a line feed, and their index entries as the
/// engine's parser makes them: the shape in which the engine sorts and compares lines.
/// </summary>
internal sealed class ParsedLines
{
    private ParsedLines(byte[] buffer, IndexEntry[] entries, byte[][] lines)
    {
        Buffer = buffer;
        Entries = entries;
        Lines = lines;
    }

    public byte[] Buffer { get; }

    public IndexEntry[] Entries { get; }

    /// <summary>The bytes of each line, without its terminator, in the order they were given.</summary>
    public byte[][] Lines { get; }

    public EntryComparer Comparer => new(Buffer);

    /// <summary>The comparison of the heads of a merge over the same lines.</summary>
    public MergeHeadComparer HeadComparer => new(Buffer);

    /// <summary>The head that a merge makes of the line of an entry.</summary>
    public MergeHead HeadOf(IndexEntry entry)
    {
        return new MergeHead(in entry, Buffer.AsSpan((int)entry.Offset, entry.LineLength));
    }

    public static ParsedLines From(params string[] lines)
    {
        return From([.. lines.Select(Encoding.UTF8.GetBytes)]);
    }

    /// <summary>Lays the lines out and parses them; every line must be valid.</summary>
    public static ParsedLines From(IReadOnlyList<byte[]> lines)
    {
        using MemoryStream buffer = new();
        IndexEntry[] entries = new IndexEntry[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            LineFault fault = LineParser.Parse(lines[i], (uint)buffer.Length, out entries[i]);
            Assert.True(fault == LineFault.None, $"Line {i + 1} is not valid: {fault}");
            buffer.Write(lines[i]);
            buffer.WriteByte((byte)'\n');
        }

        return new ParsedLines(buffer.ToArray(), entries, [.. lines]);
    }

    /// <summary>The bytes of the line that an entry points to.</summary>
    public byte[] LineOf(IndexEntry entry)
    {
        return Buffer.AsSpan((int)entry.Offset, entry.LineLength).ToArray();
    }

    public string TextOf(IndexEntry entry)
    {
        return Encoding.UTF8.GetString(LineOf(entry));
    }
}
