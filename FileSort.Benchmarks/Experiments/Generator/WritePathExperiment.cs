using System.CommandLine;

using Microsoft.Win32.SafeHandles;

using FileSort.Generator.Output;

namespace FileSort.Benchmarks.Experiments.Generator;

/// <summary>
/// Raw sequential writes of preallocated files, final flush included: what the disk takes with each write mode,
/// chunk size and number of writers, without the generator. It decides how the generator writes.
/// </summary>
internal static class WritePathExperiment
{
    private const FileOptions NoBuffering = (FileOptions)0x20000000;

    public static Command CreateCommand()
    {
        Option<string> file = ExperimentOptions.File();
        Option<long> size = ExperimentOptions.Size();
        Option<string[]> modes = ExperimentOptions.Modes();
        Option<int[]> chunks = ExperimentOptions.Numbers("--chunks", "Chunk sizes in MiB, separated by commas.", 1, 4, 8, 16);
        Option<int[]> writers = ExperimentOptions.Numbers("--writers", "Numbers of writers, separated by commas.", 1, 4, 8);
        Option<bool> oneFile = new("--one-file")
        {
            Description = "The writers share one file and each writes a region of it, instead of a file each. On "
                + "NTFS this measures the zero-filling before a write that starts in the middle of a new file.",
        };
        Option<bool> noPreallocation = new("--no-preallocation")
        {
            Description = "Do not reserve the space of a file before writing it.",
        };
        Option<int> runs = ExperimentOptions.Runs(1);
        Option<int> pause = ExperimentOptions.Pause();

        Command command = new(
            "write-path",
            "Raw sequential writes, final flush included: what the disk takes with each write mode, chunk size and "
            + "number of writers, without the generator. With the defaults the series writes 24 GiB.")
        {
            file, size, modes, chunks, writers, oneFile, noPreallocation, runs, pause,
        };
        command.SetAction(result => Run(
            Path.GetFullPath(result.GetValue(file)!),
            result.GetValue(size),
            result.GetValue(modes)!,
            result.GetValue(chunks)!,
            result.GetValue(writers)!,
            preallocate: !result.GetValue(noPreallocation),
            result.GetValue(runs),
            TimeSpan.FromSeconds(result.GetValue(pause)),
            result.GetValue(oneFile)));
        return command;
    }

    private static void Run(
        string file, long size, string[] modes, int[] chunks, int[] writers, bool preallocate, int runs, TimeSpan pause, bool oneFile)
    {
        long totalBytes = size * modes.Length * chunks.Length * writers.Length * runs;
        Console.WriteLine(
            $"File: {file}, size: {Measure.Size(size)}, preallocation: {(preallocate ? "yes" : "no")}, "
            + $"{(oneFile ? "writers share one file" : "one file per writer")}. This series writes {Measure.Size(totalBytes)} in total.");
        Measure.Header("Write mode", "Writers", "Chunk, MiB", "Median, MiB/s", "Min", "Max", "Runs");

        foreach (string mode in modes)
        {
            bool unbuffered = mode switch
            {
                "unbuffered" => true,
                "buffered" => false,
                _ => throw new ArgumentException($"Unknown write mode '{mode}'."),
            };

            foreach (int writerCount in writers)
            {
                foreach (int chunk in chunks)
                {
                    List<double> throughputs = [];
                    for (int run = 0; run < runs; run++)
                    {
                        throughputs.Add(Write(file, size, unbuffered, checked(chunk * (int)Measure.Mebibyte), writerCount, preallocate, oneFile));

                        // An SSD writes into a fast cache first and needs idle time to empty it.
                        Thread.Sleep(pause);
                    }

                    RunStatistics result = RunStatistics.From(throughputs);
                    Measure.Row(unbuffered ? "Unbuffered" : "Buffered", writerCount, chunk, result.Median, result.Min, result.Max, result.Runs);
                }
            }
        }
    }

    /// <summary>
    /// Writes the given number of bytes in chunks and returns the throughput in MiB/s. By default every writer has a
    /// file of its own and writes it from the start. With <paramref name="oneFile"/> the writers share one
    /// preallocated file and each writes a region of it. That looks like the same experiment and is not: a file
    /// system that keeps a valid data length (NTFS) fills everything before a write with zeros first, so a writer
    /// that starts in the middle of the file makes the system write the zeros before it.
    /// </summary>
    public static double Write(
        string file, long size, bool unbuffered, int chunkSize, int writers, bool preallocate, bool oneFile = false)
    {
        long chunksPerWriter = size / chunkSize / writers;
        if (chunksPerWriter == 0)
        {
            throw new ArgumentException(
                $"The size is too small: {writers} writers with chunks of {chunkSize / Measure.Mebibyte} MiB need at least "
                + $"{Measure.Size((long)writers * chunkSize)}.");
        }

        long sizePerWriter = chunksPerWriter * chunkSize;
        long total = sizePerWriter * writers;
        string[] files = oneFile
            ? [file]
            : [.. Enumerable.Range(0, writers).Select(i => i == 0 ? file : $"{file}.{i}")];
        AlignedBuffer[] buffers = new AlignedBuffer[writers];
        for (int i = 0; i < writers; i++)
        {
            buffers[i] = new AlignedBuffer(chunkSize, FileSink.Alignment);
            FillWithNoise(buffers[i].Span, (ulong)i);
        }

        try
        {
            return Measure.Throughput(total, () =>
            {
                using SafeFileHandle? shared = oneFile ? Open(file, unbuffered, preallocate ? total : 0) : null;
                Thread[] threads = new Thread[writers];
                for (int i = 0; i < writers; i++)
                {
                    int writer = i;
                    threads[i] = new Thread(() =>
                    {
                        SafeFileHandle handle = shared ?? Open(files[writer], unbuffered, preallocate ? sizePerWriter : 0);
                        long start = shared is null ? 0 : writer * sizePerWriter;
                        for (long offset = 0; offset < sizePerWriter; offset += chunkSize)
                        {
                            RandomAccess.Write(handle, buffers[writer].Span, start + offset);
                        }

                        if (shared is null)
                        {
                            RandomAccess.FlushToDisk(handle);
                            handle.Dispose();
                        }
                    });
                    threads[i].Start();
                }

                foreach (Thread thread in threads)
                {
                    thread.Join();
                }

                if (shared is not null)
                {
                    RandomAccess.FlushToDisk(shared);
                }
            });
        }
        finally
        {
            foreach (string path in files)
            {
                File.Delete(path);
            }

            foreach (AlignedBuffer buffer in buffers)
            {
                buffer.Dispose();
            }
        }
    }

    private static SafeFileHandle Open(string path, bool unbuffered, long preallocationSize)
    {
        return File.OpenHandle(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            unbuffered && OperatingSystem.IsWindows() ? NoBuffering : FileOptions.None,
            preallocationSize);
    }

    // Incompressible content, so a disk that compresses or deduplicates gets no advantage. Nobody reads these bytes
    // back or compares them between machines, so System.Random does the job. The generator's own Xoshiro256 is for
    // file content, which has to be the same bytes for a seed on every .NET version.
    private static void FillWithNoise(Span<byte> buffer, ulong stream)
    {
        new Random((int)stream).NextBytes(buffer);
    }
}
