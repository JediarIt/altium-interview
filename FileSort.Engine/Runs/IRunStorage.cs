namespace FileSort.Engine.Runs;

/// <summary>
/// Where the runs of a sort and its output go (D17 of the design). The sorter keeps runs in files of a temporary
/// folder and writes the output under a temporary name; tests and measurements of the processor keep everything in
/// memory, so that thousands of runs and several merge passes need no disk.
/// </summary>
internal interface IRunStorage
{
    /// <summary>A new run, empty. What is written to the stream until it is disposed is the run.</summary>
    Stream CreateRun(out int id);

    /// <summary>
    /// Reads bytes of a finished run from a position into the destination, and returns how many: as many as the
    /// destination holds unless the run ends first.
    /// </summary>
    int Read(int id, long position, Span<byte> destination);

    /// <summary>Deletes a run that has been merged.</summary>
    void Delete(int id);

    /// <summary>
    /// Opens the output for writing, once the whole input has been read. An existing output is deleted right before,
    /// as the user agreed to.
    /// </summary>
    Stream CreateOutput();

    /// <summary>Gives the output that has been written in full its name: the sort is complete.</summary>
    void CommitOutput();
}
