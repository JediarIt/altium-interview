# FileSort.Generator

The test file generator of [FileSort](../README.md). It writes a file of a requested size in the `Number. String`
format: reproducible from a seed, with repeated Strings and, on request, with every unusual but valid kind of line.
The format itself and the assumptions about it are in the [root README](../README.md#assumptions).

## Contents

- [Usage](#usage)
- [Options](#options)
- [What the lines contain](#what-the-lines-contain)
- [Edge cases](#edge-cases)
- [Reproducible files](#reproducible-files)
- [Writing the file](#writing-the-file)
- [Performance](#performance)
- [Design decisions](#design-decisions)
- [Tests](#tests)
- [Limitations](#limitations)

## Usage

It writes a file of the requested size, made of valid `Number. String` lines:

```powershell
# 1 GB of plain lines; the seed is chosen at random and printed
FileSort.Generator --output data/test.txt --size 1GB

# The same file again, on any machine
FileSort.Generator --output data/test.txt --size 1GB --seed 42

# Mostly unique long Strings: nothing from the pool, 4 or 5 words each
FileSort.Generator -o data/unique.txt -s 10GB --duplicates 0 --words 4-5

# Every unusual but valid kind of line, with Windows line endings
FileSort.Generator -o data/edge.txt -s 100MB --edge-cases all --line-ending crlf
```

`FileSort.Generator` stands for either way of starting it: `dotnet run -c Release --project FileSort.Generator --`
or the published executable (see [Quick start](../README.md#quick-start)).

A file made with default options looks like this:

```
975143. Degree forward wife
1718. Be dead
158692. Pump crop
29842359. Bone agency kick
14. Heat joy jaguar expect time
217. Sit address
```

## Options

| Option | What it does | Accepted values | Default |
|---|---|---|---|
| `--output`, `-o` | The file to create. Missing directories are created. | A file path | Required |
| `--size`, `-s` | The size of the file. The file ends with the first line that reaches the size, so it is at least this large and exceeds it by less than one line. | A number greater than zero with an optional unit: `B`, `KB`, `MB`, `GB`, `TB`. Units are binary (`1KB` is 1024 bytes); `KiB`, `MiB`, `GiB`, `TiB` mean the same. A fraction is allowed (`1.5GB`) and is rounded up to a whole byte. | Required |
| `--seed` | The seed of the content. | A whole number from 0 to 18446744073709551615 | Random; the summary prints the seed used |
| `--duplicates` | The probability, in percent, that a line takes its String from a pool of 1024 repeated Strings. | A whole number from 0 to 100 | `30` |
| `--words` | The number of words in a String. | A number (`4`) or a range (`2-7`), from 1 to 1024 | `1-5` |
| `--edge-cases` | Adds valid but unusual lines, see [Edge cases](#edge-cases). | Category names separated by commas: `unicode`, `separator`, `case`, `big-numbers`, `leading-zeros`, `empty`, `carriage-return`; or `all` | None: every line is plain |
| `--line-ending` | The line ending of the file, the same on every operating system. | `lf` (`\n`) or `crlf` (`\r\n`) | `lf` |
| `--threads` | The largest number of threads that generate content at the same time. It does not change the content. | A whole number from 1 to 256 | The number of logical processors |
| `--overwrite` | Replaces an existing output file without asking. | A flag | Off: the generator asks |
| `--buffered-io` | Writes through the file cache of the operating system, see [Writing the file](#writing-the-file). | A flag | Off |
| `--help`, `-h`, `-?` | Prints the usage text. | A flag | |
| `--version` | Prints the version. | A flag | |

A test (`ReadmeOptionsTableTests`) compares the option names and the fixed defaults of this table with the command
line itself, so the table cannot silently fall behind the code.

## What the lines contain

- **Strings** are 1 to 5 words (`--words`) from a built-in dictionary of 1024 English words, separated by single
  spaces, the first word capitalized. The number of words is chosen at random from the range, every count about
  equally often.
- **Numbers** come in every length: first a bit length from 1 to 32 is chosen, each with equal probability, then a
  value of that many bits. So every decimal length from 1 to 10 digits is common, between 4% and 13% of the lines
  each (see [Design decisions](#design-decisions) for why).
- **Repeated Strings.** `--duplicates` is the probability that a line takes its String from a pool of 1024 Strings
  made from the seed. At 1 GB and the default 30%, each pool String appears about 12,000 times, so the sorter meets
  long groups of equal Strings that only the Number orders. The option sets the share of pool lines, not the share
  of repeated Strings: freshly composed Strings also coincide by chance, short ones most of all (a one-word String
  is one of 1024 words). With default options about 45% of the lines of a small file share their String with
  another line; `--duplicates 0 --words 4-5` gives mostly unique Strings.
- A default file therefore contains everything the task asks for: equal Strings with different Numbers, fully
  identical lines, and Numbers from 1 to 10 digits. Without `--edge-cases` every String starts with an upper-case
  ASCII letter followed by lower-case letters and spaces, which ordinal and culture-aware comparers order the same
  way, so results can be compared with other tools.

## Edge cases

`--edge-cases` adds lines that are valid but that a naive sorter gets wrong. Each enabled category applies to a line
with probability 1/16, independently of the others.

| Category | What an affected line gets | What it catches |
|---|---|---|
| `unicode` | Words without ASCII characters: Cyrillic, Greek, CJK, fullwidth forms, emoji, and one word spelled with a precomposed and with a decomposed letter | Comparing UTF-16 code units or culture rules instead of bytes; cutting a multi-byte character |
| `separator` | `. ` inside the String, a String that begins with digits and `. `, or a String that begins with a space | Splitting at the last `. ` instead of the first; trimming |
| `case` | Dictionary words in lower case, capitalized and in upper case, in any position (the non-ASCII words of `unicode` keep their own spelling) | Case-insensitive comparison |
| `big-numbers` | Numbers above 4294967295, among them 4294967296, 9223372036854775807, 9223372036854775808, 18446744073709551615 and 18446744073709551616, and Numbers of 21 to 30 digits, which do not fit in 64 bits | Parsing the Number as a 32-bit or 64-bit integer, signed or not |
| `leading-zeros` | 1 to 3 zeros in front of the Number; together with `big-numbers` a Number has up to 33 digits | Comparing Numbers as text or by length |
| `empty` | An empty String | Assuming at least one character after the separator |
| `carriage-return` | A carriage return at the end of the String; never on the first line of the file | Trimming the String, or taking a lone `\r` as the end of a line; in a `\r\n` file the line ends with `\r\r\n` |

Edge cases never change the number of words, except `empty`. Pool Strings carry them too, so unusual Strings also
repeat. The carriage return is decided for the line, not for the String: one pool String appears with it on some
lines and without it on others, and an empty String with it is a single carriage return. The first line never gets
one, so a reader that takes the line ending from the first line of a file, as the sorter and the checker do by
default, reads every generated file with the line ending it was written with.

## Reproducible files

The bytes of a file depend only on the seed, on `--size`, `--duplicates`, `--words`, `--edge-cases` and
`--line-ending`, and on the *content version* of the generator. They do not depend on the number of threads, the
write mode, the output path, the operating system or the .NET version. Every run prints the command line that
writes the same bytes again:

```
Output:           C:\data\test.txt
Size:             1073741831 bytes (1.00 GiB)
Lines:            43065356
Average line:     24.9 bytes
Seed:             1
Content version:  2
Write mode:       unbuffered, 16 threads
Elapsed:          0.80 s
Throughput:       1276 MiB/s
Peak working set: 81 MiB
Reproduce:        FileSort.Generator --output C:\data\test.txt --size 1073741824 --seed 1 --duplicates 30 --words 1-5 --line-ending lf
```

How it is achieved:

- The file is a sequence of blocks of 1 MiB. A block is filled with whole lines, and its content is a function of
  the seed, the content options and the block's index: each block has its own random stream. Any thread can compute
  any block, so the thread count only decides who computes it. Splitting the file "by thread" would make 4 and 16
  threads produce different files.
- A smaller file is a prefix of a larger one made with the same seed and options.
- The generator uses its own random number generator and only integer arithmetic: no `System.Random`, no floating
  point, no culture, no `Environment.NewLine`.
- Everything the bytes depend on (dictionary, block size, pool size, edge case rates, the way random bits are used)
  is frozen under the content version, which the summary prints. Tests pin the SHA-256 of two reference files for
  every version; a change that alters the bytes fails them until the version is raised.

The content versions so far:

| Version | What changed |
|---|---|
| 1 | The first version. |
| 2 | `big-numbers` also writes 18446744073709551616 and Numbers of 21 to 30 digits, and the category `carriage-return` is new. Files made without `--edge-cases` keep their bytes: the reference file with default options has the same hash in both versions. Files made with edge cases can differ, as the reference file with `--edge-cases all` does. |

## Writing the file

- The content goes to `<output>.tmp` next to the output and is renamed when it is complete, so the output path
  never holds a partial file. Ctrl+C, a termination signal or an error removes the temporary file.
- An existing output file is replaced only with consent: `--overwrite`, or `y` at the prompt. When the input or the
  error stream is redirected there is nobody to ask, and the generator refuses. After consent the old file is
  deleted *before* the new one is written, so the volume needs no room for two 100 GB files; the price is that a
  cancelled run leaves no file. That is acceptable for a file that its seed can make again.
- Free space is checked before anything is deleted, and the temporary file is preallocated, so a volume that is too
  small fails before any content is written.
- Blocks are generated in parallel and written by one thread in order. At most `--threads` blocks are in flight, so
  memory does not depend on the file size, and a slow disk slows generation down instead of filling memory.
- On Windows the file is written without the file cache of the operating system (`FILE_FLAG_NO_BUFFERING`), in
  chunks of 16 MiB: see [Performance](#performance) for the measurements behind that. `--buffered-io` switches to
  regular writes, for a drive that rejects unbuffered ones (some network and virtual drives). Other operating
  systems always use regular writes.
- Before it starts, the generator prints the settings it works with to the error stream: every option with its
  value, whether it was given or not, and the seed, also when the generator chose it. A run that is cancelled or
  fails has no summary, so this is where its settings can be read:

  ```
  Generating:       C:\data\test.txt
  Requested size:   1073741824 bytes (1.00 GiB)
  Seed:             8171887560342867551 (chosen at random)
  Duplicates:       30%
  Words:            1-5
  Edge cases:       none
  Line ending:      lf
  Threads:          16
  Write mode:       unbuffered
  Overwrite:        no
  Content version:  2
  ```
- Progress (percentage, throughput, time left) is shown on the error stream unless it is redirected. The summary
  goes to standard output and nothing else does, so it can be redirected to a file or read by a script. If standard
  output fails once the file is in place (redirected to a file on a full disk, say), the file stays, the exit code
  is 0, and the error stream says that the summary was lost. A pipe whose reader has stopped is not a failure: the
  .NET runtime drops what is written to it, and the run ends with 0 as usual.

Exit codes:

| Code | Meaning |
|---|---|
| 0 | The file was written |
| 1 | No file was written: a runtime failure (not enough space, unwritable path) or a declined overwrite |
| 2 | Invalid arguments; the message names the option |
| 130 | Cancelled with Ctrl+C or a termination signal |

## Performance

The machine these numbers come from, and how far they can be trusted, are described in the
[root README](../README.md#performance): the processor is limited by power and heat, the SSD has a fast cache, and
a disk figure comes from one run unless its table shows several.

### End to end

The published executable (`publish/generator/FileSort.Generator.exe`), default options, 16 threads. Wall time,
throughput and peak working set are the ones the generator prints in its summary.

| Size | Write mode | Threads | Wall time | Throughput | Peak working set |
|---|---|---|---|---|---|
| 1 GB | Unbuffered (default) | 16 | 0.80 s | 1276 MiB/s | 81 MiB |
| 10 GB | Unbuffered (default) | 16 | 6.02 s | 1701 MiB/s | 84 MiB |
| 100 GB | Unbuffered (default) | 16 | 60.95 s | 1680 MiB/s | 92 MiB |
| 10 GB | Buffered (`--buffered-io`) | 16 | 9.87 s | 1038 MiB/s | 56 MiB |
| 100 GB | Buffered (`--buffered-io`) | 16 | 93.39 s | 1097 MiB/s | 62 MiB |
| 10 GB | Unbuffered (default) | 64 (`--threads 64`) | 5.73 s | 1788 MiB/s | 132 MiB |

The requirements are 100 GB in under 5 minutes and a peak working set under 512 MB that does not grow with the
file. A 100 GB file takes about a minute and has 4.3 billion lines; memory stays under 100 MiB from 1 GB to 100 GB,
and under 140 MiB with 64 threads. The 1 GB run is short enough for start-up (loading the runtime, compiling the
code) to show in its throughput. Sizes are binary: 100 GB is 107,374,182,400 bytes.

### Where the limit is

Generation alone against the whole run, by thread count. Generation alone is the pipeline writing into a sink that
throws the output away, measured by BenchmarkDotNet on 256 MiB (`micro --filter *Pipeline*`). End to end is one run
per thread count into a file of 10 GB
(`pipeline --sink file --file data/pipeline.bin --size 10GB --threads 8`, with a pause between runs).

| Threads | Generation alone: mean time | Generation alone, MiB/s | Allocated per run | End to end, MiB/s (one run) |
|---|---|---|---|---|
| 1 | 543.0 ms | 471 | 2.08 MB | 471 |
| 2 | 343.9 ms | 744 | 3.08 MB | 715 |
| 4 | 214.8 ms | 1192 | 5.08 MB | 1264 |
| 8 | 155.8 ms | 1643 | 9.08 MB | 1735 |
| 16 | 107.2 ms | 2388 | 17.07 MB | 1811 |

- **Up to 8 threads generation is the limit**: the whole run is as fast as generation alone, within the noise of
  this machine (the two columns were taken at different times and by different methods).
- **At 16 threads the write path is the limit.** Generation alone keeps growing, to 2.3 GiB/s, and the whole run
  stays at about 1.8 GiB/s. For large files that is also what the disk sustains (see below).
- **Below 100 GB that limit is not the disk itself.** At 10 GB the whole run, about 1.7 to 1.8 GiB/s, stays below
  what the raw disk takes from one writer, about 2.5 GiB/s at 8 GiB ([The write path](#the-write-path)). Part of the
  difference is the file sink, which measured 67% to 99% of the raw write ([The file sink](#the-file-sink)); the rest
  was not measured further. The design aimed at a run limited by the disk: that holds at 100 GB, where the drive
  itself slows down to about 1.8 GiB/s, and not for smaller files.
- Generation scales to 5.1 times one thread on 16 logical processors, not to 16 times: the power limit of the processor
  and hyper-threading.
- The allocated memory is one megabyte per thread plus one: the block buffers, `threads + 1` of 1 MiB, and next
  to nothing else. It does not depend on the size.

An earlier version of this section compared the same end-to-end runs with single stopwatch runs of generation alone
(600 MiB/s for one thread) and concluded that the whole run is slower than generation even with one thread, with a
guess about the cause. The benchmark does not show that difference: the stopwatch figure was the noise.

### The write path

What the disk takes without the generator: raw sequential writes of 8 GiB of incompressible data into preallocated
files, final flush included. All values are MiB/s. Reproduce with
`dotnet run -c Release --project FileSort.Benchmarks -- write-path --file data/write-path.bin --size 8GiB --pause 30`,
and add `--one-file` for the last four rows. Mind the volume: this command writes 192 GiB; the experiments default
to 1 GiB per run for that reason.

| Write mode | Writers | Chunk 1 MiB | Chunk 4 MiB | Chunk 8 MiB | Chunk 16 MiB |
|---|---|---|---|---|---|
| Unbuffered | 1 | 1676 | 2214 | 2301 | 2600 |
| Unbuffered | 4, a file each | 3186 | 3171 | 3136 | 3185 |
| Unbuffered | 8, a file each | 3189 | 3130 | 3136 | 3165 |
| Buffered | 1 | 1155 | 1133 | 1204 | 1265 |
| Buffered | 4, a file each | 2499 | 2654 | 2422 | 2741 |
| Buffered | 8, a file each | 2148 | 2294 | 2118 | 2037 |
| Unbuffered | 4, regions of one file | 1023 | 1129 | 1287 | 1284 |
| Unbuffered | 8, regions of one file | 972 | 1109 | 1185 | 1157 |
| Buffered | 4, regions of one file | 870 | 870 | 866 | 852 |
| Buffered | 8, regions of one file | 822 | 808 | 838 | 800 |

"Unbuffered" is `FILE_FLAG_NO_BUFFERING`: the data goes from the program's memory straight to the disk, without a
copy into the file cache of the operating system. What follows from the table:

- **Unbuffered writes** are about twice as fast as buffered ones for one writer. A side effect helps later
  measurements: the new file does not fill the file cache, so the sorter that reads it next starts from a cold
  cache and from free RAM.
- **One writer, because the output is one file.** The disk itself takes more from several writers: about 3.1 GiB/s
  when each writes its own file, against 2.2 to 2.5 from one. But several writers inside one file are slower than
  one. NTFS keeps a "valid data length" for a file and fills everything between it and a write with zeros first, so
  a writer that starts in the middle of a fresh file makes the system write those zeros before its data. The
  generator's output is one file that grows from its start, so it has one writer.

  This row of the table was first measured wrong. The plan for this change compared writers by giving each a
  region of one file, saw the bottom four rows and concluded that the disk prefers one writer. An independent
  review of the code pointed at the zero-filling; a file per writer gave the opposite result. The conclusion for
  the generator stands, the reason for it changed.
- **Large chunks.** Three more runs per size, one writer, unbuffered
  (`write-path --file data/write-path.bin --size 8GiB --modes unbuffered --writers 1 --chunks 4,8,16,32 --runs 3 --pause 20`):

  | Chunk, MiB | Run 1 | Run 2 | Run 3 |
  |---|---|---|---|
  | 4 | 2285 | 2320 | 2267 |
  | 8 | 2383 | 2397 | 2354 |
  | 16 | 2511 | 2603 | 2620 |
  | 32 | 2728 | 2729 | 2686 |

  The generator writes **16 MiB** chunks: 8% faster than 8 MiB. 32 MiB adds 5% more and is not used: the generator
  does not produce data that fast on this machine, and the two chunk buffers are the largest fixed part of its
  memory. The buffered mode shows no gain from larger chunks and keeps 1 MiB.
- **100 GB is slower than 8 GB**, because the drive leaves its fast cache on the way. One writer, 100 GiB
  (`--size 100GB --writers 1`): 1776 MiB/s unbuffered with 8 MiB chunks, 1835 MiB/s unbuffered with 16 MiB chunks,
  964 MiB/s buffered. (A run with 16 MiB chunks started two minutes after another 100 GB run gave 1474 MiB/s: the
  cache had not recovered.)

### The file sink

Ready blocks written to a file in three ways (`file-sink --file data/file-sink.bin --size 8GiB --modes unbuffered
--runs 2 --pause 45`; unbuffered, 16 MiB chunks): the raw write of one buffer over and over, which copies nothing;
a sink with one chunk buffer that copies blocks into it and then writes it, on one thread; and the generator's file
sink, which copies into one buffer while its own writer thread writes the other.

| Run | Raw write, MiB/s | One buffer, MiB/s | One buffer / raw | File sink, MiB/s | File sink / raw |
|---|---|---|---|---|---|
| 1 | 2663 | 2122 | 80% | 2625 | 99% |
| 2 | 2691 | 2096 | 78% | 1816 | 67% |

The design set a threshold: below 90% of the raw throughput, the write moves off the thread that copies. One buffer
is below it, so the sink has a second buffer and a writer thread. The writer is a thread of its own and not a
thread-pool task, so that a write never waits in the pool's queue behind the blocks being generated.

The second run of the file sink is not a typo. In the earlier runs of this comparison the file sink gave 2260 to
2430 MiB/s (86% to 96% of the raw write) most of the time and about 1850 now and then, with the raw write steady in
the same series. The cause of the slow runs was not found. In the buffered mode the file sink matched the raw write
(1237 against 1238 MiB/s, 1230 against 1211); the buffered runs of the series above hit the full cache of the SSD
and are left out.

### Block size

The file is made of blocks, and the block capacity is part of the content version, so it was chosen before the
reference hashes were recorded. 10 GB, 16 threads
(`pipeline --sink file --file data/pipeline.bin --size 10GB --threads 16 --block-capacity 256` and so on):

| Block capacity | End to end, MiB/s | Generation alone, MiB/s |
|---|---|---|
| 256 KiB | 1679 | 1996 |
| 1 MiB | 1762 | 2537 |
| 4 MiB | 1740 | 2876 |

End to end the three are equal within the noise of this machine. A second round of the same three runs hit the
full cache of the SSD (1061, 1085 and 799 MiB/s) and says nothing about block size. **1 MiB** is kept: larger
blocks only help generation alone, which is not the limit, and they cost memory (one buffer per thread).

### The hot path

BenchmarkDotNet, one core (`micro --filter *`):

| Benchmark | Variant | Mean | Ratio |
|---|---|---|---|
| One 64-bit random value | xoshiro256\*\* (used) | 1.43 ns | 1.0 |
| | SplitMix64 | 1.66 ns | 1.2 |
| | `System.Random(seed).Next()` (31 bits) | 11.07 ns | 7.8 |
| | `System.Random(seed).NextInt64()` | 31.54 ns | 22 |
| A new generator and its first value | xoshiro256\*\* seeded through SplitMix64 (used) | 1.81 ns | 1.0 |
| | `new System.Random(seed)` | 1920 ns | 1060 |
| Writing one Number | Loop-free writer (used) | 8.59 ns | 1.0 |
| | `Utf8Formatter.TryFormat` | 15.93 ns | 1.9 |
| Copying one word | A fixed 16-byte slot (used) | 2.10 ns | 1.0 |
| | By its exact length | 6.76 ns | 3.2 |
| Copying a word and capitalizing it | One exclusive or with 0x20 (used) | 2.25 ns | 1.0 |
| | `Ascii.ToUpperInPlace` on its first byte | 5.30 ns | 2.4 |
| One block of 1 MiB, default options | | 2.48 ms: 404 MiB/s | |
| One block of 1 MiB, `--words 1` | | 2.84 ms: 353 MiB/s | |
| One block of 1 MiB, `--edge-cases all` | | 3.36 ms: 298 MiB/s | |

- **Zero bytes are allocated per block** in all three block benchmarks.
- The row of `--edge-cases all` is of content version 2, measured again on another day with
  `micro --filter *BlockBenchmarks*`: 3.36 and 3.40 ms in two runs, in which the default row gave 2.20 and 2.65 ms.
  Version 1 measured 2.70 ms, 9% above the default row of the table; version 2 is 35% above that row, and 53% and
  28% above the default row of the same two runs. Version 2 makes one line in 128 a Number of 21 to 30 digits, with
  128-bit arithmetic and formatting; that this is the cause is likely and was not measured on its own.
- A new generator per block is why the generator cannot use `System.Random`: each block has its own random stream,
  and creating a seeded `System.Random` costs about 1.9 µs.
- The Number and the word benchmarks run over a million values, not over a short series repeated in a loop. With a
  series of 1024 values the Number writers looked equal: the processor's branch predictor learns a short series by
  heart, and the cost that the loop-free writer removes, a digit loop whose length is random, disappears from the
  measurement. In the generator no series repeats.
- The loop-free writer handles the Numbers of plain lines, which fit in 32 bits. The rare Numbers that it cannot
  write (above 32 bits, or with leading zeros) go through the framework's `Utf8Formatter`, and those above 64 bits
  through the formatting of `UInt128`. A hand-written writer with a digit loop was measured too, at 19.4 ns, slower
  than the framework's; it was removed.
- The block benchmarks give about 400 MiB/s per core, the pipeline benchmark with one thread 471 MiB/s (the table
  in [Where the limit is](#where-the-limit-is)). The same code runs in both; they were measured in different
  sessions on a machine that slows down as it heats. Compare numbers within one table, not across tables.

### Memory

Nothing in the generator grows with the size of the file:

| Item | Size at 16 threads | Scales with |
|---|---|---|
| Block buffers, `threads + 1` of 1 MiB | 17 MiB | `--threads` |
| Chunk buffers, 2 of 16 MiB (2 of 1 MiB with `--buffered-io`) | 32 MiB | constant |
| Pool of repeated Strings | under 0.1 MiB by default; for `--words 1024` about 6 MiB, at most 22 MiB with `unicode` and `separator` | `--words` |
| Runtime, compiled code, thread stacks | the rest of the peak working set | `--threads` |

The measured peak working set is in the end-to-end table above. The garbage collector stays in its default mode:
generating a block allocates nothing (0 bytes in the benchmark above), and the pipeline allocates its block buffers
once and one small task per block. The pipeline benchmark confirms the first row: 17.07 MB allocated per run with 16
threads, 2.08 MB with one.

## Design decisions

Decisions about the whole solution are in the [root README](../README.md#design-decisions); these are the ones of
the generator.

- **The generator has its own random number generator.** xoshiro256\*\*, seeded through SplitMix64: about 20 lines,
  pinned by known-answer tests against the published reference values. `System.Random` with a seed gives a stable
  sequence today, but its algorithm is not promised across .NET versions, and the bytes of a test file must depend
  on the seed alone. It is also slower where it matters here: a 64-bit value takes 1.4 ns against 31.5 ns, and a
  new generator, which every block needs for its own stream, takes 1.8 ns against 1.9 µs (see
  [The hot path](#the-hot-path)).

  Why not a ready implementation: the framework has xoshiro256\*\* too, behind `Random.Shared` and an unseeded
  `new Random()`, but it is internal and cannot be given a seed. The packages that offer it are either a large
  numerics library or small ones with few users, and any of them would be a second dependency of a tool that has to
  stay trim-safe, for twenty lines. The algorithm is published by its authors as reference code in the public
  domain; these lines follow it, and the tests compare them with its published values.

  Each source of randomness is chosen for what it has to guarantee:

  | Where | Generator | Why |
  |---|---|---|
  | The content of a file | Own xoshiro256\*\* | The same bytes for a seed on any machine and any .NET version |
  | The seed of a run that was given none | `RandomNumberGenerator` of the operating system | It only has to differ from run to run; the run prints it |
  | The noise that the write-path experiment writes | `System.Random` | Nobody reads those bytes back; they only have to be incompressible |
- **Strings are made of dictionary words, not of random letters or real text.** Random letters differ in their first
  bytes almost always, which flatters a sorter that compares a short prefix. Real text has too few distinct
  fragments: almost every String would repeat in a large file, `--duplicates` would control nothing, and the text
  would be a multi-megabyte asset. Words from a dictionary of 1024 give long common prefixes (`Apple pie` against
  `Apple tree`) and keep the share of repeats under control. The list was written for this project, so it has no
  license of its own, and it has no apostrophes or hyphens, which culture-aware comparers treat specially.
- **Numbers of every length are common.** A Number drawn uniformly from 0 to 2^32 has 9 or 10 digits 98% of the
  time, and two lines almost never share a Number, so small test files would contain no fully identical lines.
  Dropping or merging identical lines is a classic bug of a sorter, and tests run on small files. Choosing the bit
  length first, each from 1 to 32 with equal probability, makes short Numbers as common as long ones, like the
  Numbers of the task's example (`1`, `415`, `30432`), and puts thousands of identical lines into a 10 MB file. It
  needs no division and no floating point.
- **Lines are written straight into the output block.** Digits, separator, words and line ending go into the block
  buffer as bytes: no `string`, no intermediate buffer, no heap allocation per line. Two details are chosen by
  benchmark (see [The hot path](#the-hot-path)): a word is copied as a fixed 16-byte slot instead of by its length,
  and the digits of a Number are computed without a loop. Both remove a branch that the processor cannot predict,
  because the length of a word or of a Number is random.
- **Bit operations sit behind named methods.** One random value pays for several choices, each from its own group
  of bits, and a bare `(r * 3) >> 8` tells a reader nothing. `RandomBits` names the ways a value is read: `Bits`
  (a group of bits), `IsSet` (one bit), `Pick` (one of n choices), `Highest` and `WithBitLength` (a Number of a
  given bit length). The loop-free Number writer is split the same way, into `DropFirstCharacters`,
  `TwoAsciiDigits`, `EightAsciiDigits` and `ToCharacters`. Every doc comment has a worked example with real values.
  The methods ask to be inlined, and the machine code shows that they are: printed before and after the change
  (`DOTNET_TieredCompilation=0` and `DOTNET_JitDisasm="WriteLine Compose Apply WriteUnusualNumber"` on a run of the
  generator), the methods of the hot path have the same size, `WriteLine` 1029 bytes, `Compose` 601, `Apply` 50,
  and the same calls. Two first versions did cost time and were changed back: `Ascii.ToUpperInPlace` for the
  capital letter of every String is not inlined and adds about 3 ns per String (see
  [The hot path](#the-hot-path)), and a table of the edge case flags, six at the time, doubled the code of
  `EdgeRoll.Apply`. A whole word in upper case is rare and does go through the framework. xoshiro256\*\* and
  SplitMix64 keep the authors' reference code as it is.
- **`System.CommandLine` for the command line.** It is Microsoft's parser, the one behind the `dotnet` CLI: typed
  options, validation, `--help` and `--version`. Without it each of the three programs would carry 100 to 150 lines
  of hand-written parsing and help text. Version 2.0.15 is the latest patch of the line released with .NET 10;
  3.0.0 was a few days old when this was decided. It is the only NuGet package of the generator, and a check
  publish with trimming confirms that it does not break the self-contained executable. What the programs do the
  same way around the parser (errors that name their option, the exit codes, the console, Ctrl+C, the parsing of
  sizes and whole numbers, the question before an existing output is replaced) lives in the shared library
  `FileSort.Cli`, see the [root README](../README.md#design-decisions). The benchmark project uses the same parser
  and the same size parser, so options are declared, checked and documented in one way everywhere.
- **No console UI framework.** Libraries such as Spectre.Console or RazorConsole draw tables, progress bars and
  whole screens. The generator prints eleven lines of settings, one progress line and eleven lines of summary, and
  that does not pay for a second dependency in a tool that has to pass the trim check and to start fast as a single
  file. Its output is also meant to be read by scripts: plain `Name: value` lines on standard output, which a
  framework that redraws the screen would get in the way of. The sorter, which has several phases to show and no
  trimming requirement, draws its progress with Spectre.Console (see
  [its README](../FileSort.Sorter/README.md#design-decisions)).

## Tests

- Every scenario of the two specs has a test, except those that are measurements or manual checks (throughput,
  memory, publishing; see [Performance](#performance)).
- What the generator writes is validated by a naive parser that lives in the test project and shares no code with
  the writer.
- The SHA-256 of two reference files is pinned per content version, so a change of the generated bytes cannot go
  unnoticed.
- The code coverage of the solution is in the [root README](../README.md#code-coverage).

## Limitations

- **No malformed files.** The generator writes valid lines only. The sorter and the checker stop at the first
  invalid line, so a file with one invalid line shows how they report it; such files are small and made by hand,
  and a flag for them is not written.
- **No custom dictionary.** Strings come from the built-in list of 1024 words.
- **Windows-only measurements.** The code is cross-platform, but it was developed and measured on one Windows
  machine. Unbuffered writes are used on Windows only; Linux and macOS use the buffered mode of the same code, and
  direct I/O there (`O_DIRECT`, `F_NOCACHE`) is not implemented.
- **Byte-identical output on other platforms is by construction, not by measurement.** The generator avoids every
  platform-dependent operation, and the reference-hash tests fail on a platform that produces different bytes, but
  they have only been run on Windows x64, on the .NET 10 and .NET 11 runtimes.
- **Trim analysis covers trimming only.** A check publish with `PublishTrimmed` confirms that the generator and its
  one package, `System.CommandLine`, have no trim warnings. AOT warnings inside the package would need the AOT
  compiler, which needs a C++ toolchain; that half rests on the package's own claim.
