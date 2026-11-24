using System.Globalization;

namespace FileSort.Engine.Runs;

/// <summary>A volume that the sort writes to has less free space than the sort needs.</summary>
public sealed class NotEnoughSpaceException : IOException
{
    public NotEnoughSpaceException()
    {
    }

    public NotEnoughSpaceException(string message)
        : base(message)
    {
    }

    public NotEnoughSpaceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal NotEnoughSpaceException(string directory, long required, long available)
        : base(string.Create(
            CultureInfo.InvariantCulture,
            $"Not enough free space for '{directory}': {required} bytes are needed, {available} are free."))
    {
        Directory = directory;
        Required = required;
        Available = available;
    }

    /// <summary>A directory on the volume that is short of space.</summary>
    public string? Directory { get; }

    /// <summary>The bytes that the sort needs on that volume.</summary>
    public long Required { get; }

    /// <summary>The bytes free there, counting the files that the sort replaces.</summary>
    public long Available { get; }
}
