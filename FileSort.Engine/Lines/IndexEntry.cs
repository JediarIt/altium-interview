namespace FileSort.Engine.Lines;

/// <summary>
/// One line in the index that a chunk sorts instead of its lines: 16 bytes. The first 8 decide most comparisons
/// without reading the line; the last 8 say where the line lies and what shape it has. The value of the Number is not
/// kept: M1 of the design measured the sort with it in the entry, 24 bytes, no faster than without it, and an entry of
/// 16 bytes leaves room for more lines in a chunk.
/// </summary>
/// <param name="Prefix">The first 8 bytes of the String as one number, see <see cref="StringPrefix"/>.</param>
/// <param name="Offset">Where the line starts in the buffer that holds it.</param>
/// <param name="Shape">The lengths of the line and of its Number, see <see cref="EntryShape"/>.</param>
internal readonly record struct IndexEntry(ulong Prefix, uint Offset, uint Shape)
{
    /// <summary>The length of the line, its terminator not counted.</summary>
    public int LineLength => EntryShape.LineLength(Shape);
}
