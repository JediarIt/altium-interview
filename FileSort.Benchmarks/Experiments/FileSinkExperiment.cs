using System.CommandLine;
using System.Globalization;

using Microsoft.Win32.SafeHandles;

using FileSort.Generator.Content;
using FileSort.Generator.Content.Blocks;
using FileSort.Generator.Output;

namespace FileSort.Benchmarks.Experiments;

/// <summary>
/// Ready blocks written to a file in three ways: the raw write of one buffer over and over, which copies nothing;
/// a simple sink that copies blocks into one chunk buffer and writes it on the same thread; and the generator's file
/// sink, which copies into one buffer while its writer thread writes the other. The simple sink is what the file
/// sink would be without its second buffer and thread: the comparison is the reason they exist.
/// </summary>
internal static class FileSinkExperiment
{
    private const int ReadyBlocks = 64;
    private const FileOptions NoBuffering = (FileOptions)0x20000000;

    public static Command CreateCommand()
    {
        Option<string> file = ExperimentOptions.File();
        Option<long> size = ExperimentOptions.Size();
        Option<string[]> modes = ExperimentOptions.Modes();
        Option<int> runs = ExperimentOptions.Runs(3);
        Option<int> pause = ExperimentOptions.Pause();

        Command command = new(
            "file-sink",
            "Ready blocks written three ways: the raw write, a sink with one buffer, the generator's file sink. "
            + "One run writes the size three times.")
        {
            file, size, modes, runs, pause,
        };
        command.SetAction(result => Run(
            Path.GetFullPath(result.GetValue(file)!),
            result.GetValue(size),
            result.GetValue(modes)!,
            result.GetValue(runs),
            TimeSpan.FromSeconds(result.GetValue(pause))));
        return command;
    }

    private static void Run(string file, long size, string[] modes, int runs, TimeSpan pause)
    {
        // Blocks are generated in advance: only the writing is measured.
        BlockGenerator generator = new(new ContentOptions { Seed = 1 });
        byte[][] blocks = new byte[ReadyBlocks][];
        for (int i = 0; i < ReadyBlocks; i++)
        {
            byte[] buffer = new byte[ContentFormat.BlockCapacity];
            blocks[i] = buffer[..generator.Generate(i, buffer).Bytes];
        }

        Console.WriteLine($"File: {file}, size: {Measure.Size(size)}");
        Measure.Header("Write mode", "Chunk, MiB", "Way of writing", "Median, MiB/s", "Min", "Max", "Median / raw", "Runs");

        foreach (string mode in modes)
        {
            bool unbuffered = mode == "unbuffered";
            int chunkSize = unbuffered ? FileSink.UnbufferedChunkSize : FileSink.BufferedChunkSize;
            List<double> raw = [];
            List<double> oneBuffer = [];
            List<double> sink = [];

            // The three ways alternate inside every run, so a drift of the disk affects them alike.
            for (int run = 0; run < runs; run++)
            {
                raw.Add(WritePathExperiment.Write(file, size, unbuffered, chunkSize, writers: 1, preallocate: true));
                Thread.Sleep(pause);
                oneBuffer.Add(WriteWithOneBuffer(file, size, unbuffered, chunkSize, blocks));
                Thread.Sleep(pause);
                sink.Add(WriteThroughSink(file, size, unbuffered, blocks));
                Thread.Sleep(pause);
            }

            double rawMedian = RunStatistics.From(raw).Median;
            foreach ((string way, List<double> values) in new[] { ("Raw write", raw), ("One buffer", oneBuffer), ("File sink", sink) })
            {
                RunStatistics result = RunStatistics.From(values);
                Measure.Row(
                    unbuffered ? "Unbuffered" : "Buffered",
                    chunkSize / Measure.Mebibyte,
                    way,
                    result.Median,
                    result.Min,
                    result.Max,
                    (result.Median / rawMedian).ToString("P0", CultureInfo.InvariantCulture),
                    result.Runs);
            }
        }
    }

    private static double WriteThroughSink(string file, long size, bool unbuffered, byte[][] blocks)
    {
        try
        {
            return Measure.Throughput(size, () =>
            {
                using FileSink sink = new(file, size + ContentFormat.BlockCapacity, unbuffered);
                long written = 0;
                for (int block = 0; written < size; block = (block + 1) % blocks.Length)
                {
                    sink.Write(blocks[block]);
                    written += blocks[block].Length;
                }

                sink.Commit();
            });
        }
        finally
        {
            File.Delete(file);
        }
    }

    // Copy, then write, on one thread: whole chunks only, which is enough to compare throughput.
    private static double WriteWithOneBuffer(string file, long size, bool unbuffered, int chunkSize, byte[][] blocks)
    {
        using AlignedBuffer chunk = new(chunkSize, FileSink.Alignment);
        try
        {
            return Measure.Throughput(size, () =>
            {
                using SafeFileHandle handle = File.OpenHandle(
                    file,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    unbuffered && OperatingSystem.IsWindows() ? NoBuffering : FileOptions.None,
                    size + ContentFormat.BlockCapacity);

                long offset = 0;
                int fill = 0;
                for (int block = 0; offset < size; block = (block + 1) % blocks.Length)
                {
                    ReadOnlySpan<byte> data = blocks[block];
                    while (!data.IsEmpty)
                    {
                        int count = Math.Min(data.Length, chunkSize - fill);
                        data[..count].CopyTo(chunk.Span[fill..]);
                        fill += count;
                        data = data[count..];

                        if (fill == chunkSize)
                        {
                            RandomAccess.Write(handle, chunk.Span, offset);
                            offset += chunkSize;
                            fill = 0;
                        }
                    }
                }

                RandomAccess.FlushToDisk(handle);
            });
        }
        finally
        {
            File.Delete(file);
        }
    }
}
