namespace FileSort.Common;

/// <summary>The bytes that a <see cref="LineEnding"/> stands for.</summary>
public static class LineEndingExtensions
{
    /// <summary>The bytes that end every line of a file with this line ending.</summary>
    public static ReadOnlySpan<byte> Terminator(this LineEnding lineEnding)
    {
        return lineEnding == LineEnding.CrLf ? "\r\n"u8 : "\n"u8;
    }

    /// <summary>The length of the terminator in bytes.</summary>
    public static int TerminatorLength(this LineEnding lineEnding)
    {
        return lineEnding.Terminator().Length;
    }
}
