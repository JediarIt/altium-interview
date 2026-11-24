namespace FileSort.Engine;

/// <summary>The phase a sort is in.</summary>
public enum SortPhase
{
    /// <summary>The files are opened and checked; nothing is read yet.</summary>
    Starting,

    /// <summary>The input is read chunk by chunk, and every chunk is sorted and written as a run.</summary>
    Chunks,

    /// <summary>The runs are merged, in one or more passes, the last of which writes the output.</summary>
    Merge,
}
