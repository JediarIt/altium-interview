using System.Text;

namespace FileSort.Tests.Format;

/// <summary>A line as the naive parser sees it.</summary>
internal sealed record NaiveLine(ulong Number, string NumberText, byte[] Text)
{
    public string TextAsString => Encoding.UTF8.GetString(Text);
}
