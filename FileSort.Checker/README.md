# FileSort.Checker

The checker of [FileSort](../README.md). It confirms that a sorted file is the correctly sorted version of an input
file: both files are valid, the sorted file is in order, and it has exactly the lines of the input. It is an oracle
for the sorter, written before the sorter and without its code. The line format, the order of lines and the
assumptions about them are in the [root README](../README.md#assumptions).

## Contents

- [Usage](#usage)
- [Options](#options)
- [Why the input file is needed](#why-the-input-file-is-needed)
- [What is checked](#what-is-checked)
- [The report](#the-report)
- [Exit codes](#exit-codes)
- [How it works](#how-it-works)
- [Performance](#performance)
- [Design decisions](#design-decisions)
- [Tests](#tests)
- [Limitations](#limitations)

## Usage

```powershell
# Is sorted.txt the sorted version of test.txt?
FileSort.Checker --input data/test.txt --sorted data/sorted.txt

# The same, with the short names of the options
FileSort.Checker -i data/test.txt -s data/sorted.txt

# Both files are read with \r\n, whatever the first line of the input looks like
FileSort.Checker -i data/test.txt -s data/sorted.txt --line-ending crlf

# In a script: the exit code is the answer, the report goes to a file
FileSort.Checker -i data/test.txt -s data/sorted.txt > data/check.log
```

`FileSort.Checker` stands for either way of starting it: `dotnet run -c Release --project FileSort.Checker --` or
the published executable (see [Quick start](../README.md#quick-start)). The checker only reads the two files: it
creates, changes and deletes nothing.

## Options

| Option | What it does | Accepted values | Default |
|---|---|---|---|
| `--input`, `-i` | The input file of the sort: the file that the sorted file was made from. It is only read. | A file path | Required |
| `--sorted`, `-s` | The sorted file, the output of the sorter. It is only read. | A file path, not the same file as `--input` | Required |
| `--line-ending` | The line ending that both files are read with, see [Line ending](#line-ending). | `lf` (`\n`), `crlf` (`\r\n`), or `auto`: taken from the first line feed of the input | `auto` |
| `--threads` | The largest number of threads that check lines at the same time; reading the two files takes two more. It does not change the result. | A whole number from 1 to 64 | The number of logical processors, at most 64 |
| `--help`, `-h`, `-?` | Prints the usage text. | A flag | |
| `--version` | Prints the version. | A flag | |

A test (`ReadmeOptionsTableTests`) compares the option names and the fixed defaults of this table with the command
line itself, so the table cannot silently fall behind the code.

The sorted file is given in `--sorted`, not in `--output`: in the generator and the sorter `--output` is a file that
the program writes, and the checker writes none. The same file in both options is rejected as an invalid argument:
a file compared with itself always "has the same lines", and a slip of the hand would get `CORRECT` for a check
that compared nothing. The two full paths are compared as the file system compares names; a second name for one
file, such as a link, is not looked for.

## Why the input file is needed

Because the order alone proves little. Take this input:

```
415. Apple
30432. Something something something
1. Apple
32. Cherry is the best
2. Banana is yellow
```

Each of these outputs is perfectly in order, and each is wrong:

```
1. Apple                      1. Apple                                  (an empty file)
415. Apple                    1. Apple
32. Cherry is the best        2. Banana is yellow
                              32. Cherry is the best
                              30432. Something something something
```

The first lost two lines, the second has `1. Apple` twice in place of `415. Apple`, and the third lost everything.
These are the typical faults of an external sort: the tail of a buffer that was never written, a line repeated
where two temporary files meet, a line cut at the edge of a block. They leave the order intact. Only the
comparison with the input shows them, so the checker always takes both files.

## What is checked

Three facts, with one result. The rows are in order of precedence:

| What the files are like | Result | Exit code |
|---|---|---|
| A line of either file is not a valid `Number. String` line | `INVALID FILE` | 3 |
| Both files are valid; the order is violated, or the lines differ, or both | `NOT CORRECT` | 4 |
| Both files are valid, the output is in order and has the lines of the input | `CORRECT` | 0 |

- **Valid files.** Every line of both files must be valid for the line ending of the check: one or more digits,
  a period and a space, then well-formed UTF-8. The check stops at the first line that is not, and names the file,
  the number of the line, its offset in bytes and the reason. There is no reading on: a line without a key cannot
  take part in the order check, and skipping it would hide the bugs that show up exactly this way, such as a file
  cut inside a Number or a byte order mark that travelled with a line.
- **Order.** Every line of the output is compared with the line before it: Strings byte by byte, then Numbers by
  value. Numbers are compared as digits, without converting them, so a Number may have any length. An order
  violation does not stop the check: all of them are counted, and the first ten are printed.
- **Same lines.** The output must have exactly the lines of the input, each as many times, byte for byte:
  `007. Apple` and `7. Apple` have equal keys and are different lines, so a sorter that rewrites Numbers is caught.
  Each file is reduced to three numbers: the count of its lines, the count of their bytes, and the sum, modulo
  2^128, of the 128-bit hashes (`XxHash128`) of the lines. The files have the same lines when the three numbers are
  equal.
- **Equal keys in any order.** `007. Apple` and `7. Apple` are in order whichever comes first. The sorter decides
  their order; the summary counts such neighbouring pairs so that they are visible.

The order check and the comparison of the lines are independent, and both are always reported. "The lines are the
same, the order is wrong in a million places" points at the comparison code of a sorter; "the order is right, one
line is missing" points at its reading or writing.

### How a miss can happen

The comparison of the lines is probabilistic. If the two sets of lines differ, the two sums are equal only when
128-bit hashes happen to add up to the same value: with a probability of about 2^-128, and below 2^-93 even for a
bug that loses lines in equal groups of the largest size a 100 GB file allows. That is a sum and not an exclusive
or on purpose: under exclusive or two identical lines cancel each other, so an output that lost a pair of
duplicates and gained another pair would pass. The hash is not cryptographic and has no key: the checker guards
against mistakes of a sorter, not against an output forged on purpose to pass.

When the lines differ, the checker says so and gives the number of lines of each file. It does not say which line
is missing or changed: for small files, both can be sorted and compared line by line with other tools.

### Line ending

A file has one line ending for all its lines, and both files are read with the same one: a sorter is expected to
write with the line ending of its input.

- `--line-ending lf` or `crlf` reads both files with that line ending, literally.
- `--line-ending auto`, the default, looks at the first line feed of the **input**: a carriage return right before
  it means `\r\n`, anything else means `\n`, and so does an input without a line feed. The output is not looked
  at for this: in a sorted file the first line can be one whose String ends with a carriage return, and it would
  look like `\r\n`.

An output written with another line ending than its input does not pass, and the checker says why:

| Input | Output | What happens |
|---|---|---|
| `\r\n` | `\n` | `INVALID FILE` at line 1 of the output, without reading the whole file as one line: read with `\r\n`, a line feed without a carriage return does not end a line |
| `\n` | `\r\n` | `NOT CORRECT`: every line is valid, with a carriage return at the end of its String, so only the comparison of the lines fails. The summary notes that every line of the output ends with a carriage return and not every line of the input does |

The detection is wrong in two cases, and `--line-ending` fixes both: a `\r\n` file of one line without a
terminator is taken for an `\n` file (and read correctly all the same), and an `\n` file whose first String ends
with a carriage return is taken for an `\r\n` file. The generator writes Strings that end with a carriage return
(`--edge-cases carriage-return`), but never on the first line, so its files are always detected right. The report
of an invalid line names the line ending and, when it was detected, the option.

### Byte order mark

The bytes `EF BB BF` at the start of either file are skipped: they are not part of the first line and do not
affect the result. The summary says which of the files start with them. Anywhere else they are ordinary bytes,
which make a line invalid when they stand where a digit should.

## The report

Order violations are printed while the files are being read, the first ten of them, each with the line and the
line before it; a line is shortened to its first 200 bytes, with control bytes and ill-formed UTF-8 shown as
escapes:

```
Order violation at output line 1234568: equal Strings, Numbers out of order
  1234567: 415. Apple
  1234568: 32. Apple
```

When both files are read to the end, the summary follows:

```
Input:            C:\data\test.txt
  Size:           1073741831 bytes (1.00 GiB)
  Lines:          43065356
  Byte order mark: no
Output:           C:\data\sorted.txt
  Size:           1073741822 bytes (1.00 GiB)
  Lines:          43065355
  Byte order mark: no
Line ending:      lf (from the input)
Order:            FAILED, 3 violations, the first at line 1234568
Same lines:       FAILED, the output has 1 line fewer
Equal keys, different bytes: 0 pairs
Elapsed:          1.86 s
Throughput:       1102 MiB/s
Peak working set: 67 MiB
Result:           NOT CORRECT
```

After a line that is not valid there is no summary:

```
Invalid line in the output: C:\data\sorted.txt
  Line:           2
  Offset:         9 bytes from the start of the file
  Reason:         the line does not start with a digit
  Content:        Apple
  Line ending:    lf (from the input); give --line-ending if that is wrong
Result:           INVALID FILE
```

Where things go:

- **Standard output** gets what the checker says about the content of the files, and nothing else: the
  violations, the report of an invalid line, the summary. It can be redirected to a file or read by a script.
- **The error stream** gets the settings before the check (both paths in full, the line ending option, the number
  of threads), the progress line (percentage, throughput, time left; not when the stream is redirected), and
  errors: a file that cannot be read, a line longer than supported, a cancellation.
- If standard output fails (redirected to a file on a full disk, say), the check goes on and the exit code still
  carries the verdict; the error stream says that the report was lost. If the settings or the progress cannot be
  written, the check stops with exit code 1 and a message that names the console.
- Ctrl+C or a termination signal stops the check within milliseconds, with exit code 130 and no result.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | The output is correct |
| 1 | The check could not be done: a file that cannot be read, a line longer than 64 MiB |
| 2 | Invalid arguments; the message names the option |
| 3 | A file is not valid: a line is not a `Number. String` line |
| 4 | Both files are valid, and the output is not the sorted input |
| 130 | Cancelled with Ctrl+C or a termination signal |

The codes mean the same in every program of the solution; they are defined once, in `FileSort.Cli`. Code 4 is
separate from code 1 because the most useful thing a script can learn from a checker is the difference between
"checked, and it is wrong" and "could not check". Every non-zero exit comes with a message that names the cause,
and never with a stack trace.

## How it works

- **Each file is read once**, by a thread of its own, and the two are read at the same time. The reading thread
  cuts the file into chunks of 1 MiB, each ending at the end of a line, and hands every chunk to a worker thread.
  For the output it puts a copy of the last line of a chunk in front of the next chunk, so that every pair of
  neighbouring lines lies in one chunk.
- **A worker checks one chunk** and knows nothing of the others: it splits the chunk into lines, checks every
  line to be valid, adds it to the digest of the chunk and, for the output, compares it with the line before it,
  the first line with the copy in front. At most `--threads` chunks, of both files together, are handed out at the
  same time.
- **The results are taken in the order of the chunks.** That order gives every line its number, prints violations
  in the order of their lines, and makes the line that is reported as invalid the first invalid line of the file,
  whichever chunk was checked first. The digest is a sum, so the chunks need no order for it.
- **Nothing is kept but counters** and the first ten violations: the result of a chunk holds nothing as long as a
  line, so memory depends on the chunks in flight and not on the size of the files (see [Memory](#memory)).
- **One file that is wrong stops the reading of the other**, so a file that is invalid at its start does not cost
  the full reading of 100 GB. A file that fits in one chunk is always read to its end, so for small files the
  outcome does not depend on which thread is faster. For large files one dependence on timing remains and is
  accepted: if each file has a fault and the two lie far apart, the one that is reached first is the one
  reported. Each run names a real fault, and no run passes.
- **A line longer than 64 MiB ends the check** with exit code 1. It is a limit of the tool, not of the format: it
  protects memory when a file is read with the wrong line ending, in which case a "line" can be the whole file.

## Performance

The machine these numbers come from, and how far they can be trusted, are described in the
[root README](../README.md#performance): 8 cores with 16 logical processors, limited by power and heat, so the
same measurement varies by about 10% from run to run. The checker only reads, and every file in the tables below was
read from the file cache of the operating system: these are measurements of the processor cost of a check. The reading
itself is not the limit in any of them: the same two reading threads feed one checking thread at 353 MiB/s and
sixteen at 1267 MiB/s (see [Threads](#threads)).

All the tables below except the hash come from one series, taken after the last change of the code, and the
machine was slower in it than earlier the same day, with other programs keeping 7% to 36% of the processor busy:
the check with one thread, the control, took 5.8 s in this series and 4.4 s two hours before, although the code
has since done less work per line, not more. Compare numbers within this series; the requirements hold in it all
the same.

The pairs are a file made by the generator and its sorted copy, made by an in-memory sort that exists for these
measurements (`sorted-copy`, see [Reproducing](#reproducing)).

### End to end

The published executable (`publish/checker/FileSort.Checker.exe`), default options: 16 threads. Elapsed time,
throughput and peak working set are the ones the checker prints in its summary; the elapsed time runs from the
start of the reading to its end, so it leaves out the start of the process and the opening of the files.

| Pair | Runs | Elapsed | Throughput | Peak working set |
|---|---|---|---|---|
| 1 GB + 1 GB, default options | 5 | 1.86 s median (1.67 to 2.10) | 1102 MiB/s | 66 to 69 MiB |
| 1 GB + 1 GB, `--edge-cases all --line-ending crlf` | 1 | 2.29 s | 893 MiB/s | 69 MiB |
| 100 MB + 100 MB, default options | 1 | 0.30 s | 668 MiB/s | 65 MiB |
| 1 GB + 1 GB, default options, `--threads 1` | 1 | 6.61 s | 310 MiB/s | 35 MiB |

The requirement is a pair of 1 GB in under 3 seconds, the rate at which a pair of 100 GB takes 5 minutes; at the
measured rate it takes about 3 minutes, if the disk keeps up. Memory must stay under 256 MB and must not follow
the size of the files: it is under 70 MiB with the default threads, and the same at 100 MB and at 1 GB (see
[Memory](#memory) for the worst case). The pair of 100 GB itself, the input and the output of the sorter's one sort of
that size, was checked once, read from the disk by the regular build started with `dotnet`: 198.46 s (1032 MiB/s),
a peak working set of 80 MiB, and the result `CORRECT`.

### Threads

The whole check of the default pair for several thread counts, without the command line: the experiment `check`,
five runs per row, chunks of 1 MiB.

| `--threads` | Median, s | Median, MiB/s | Slowest | Fastest |
|---|---|---|---|---|
| 1 | 5.81 | 353 | 325 | 445 |
| 2 | 3.62 | 566 | 522 | 717 |
| 4 | 2.34 | 877 | 800 | 1029 |
| 8 | 1.80 | 1138 | 1098 | 1292 |
| 16 | 1.62 | 1267 | 1067 | 1427 |

- Sixteen threads check 3.6 times faster than one, on eight cores with hyper-threading that share their power
  budget, and with other programs busy on them in this series. The generator's pipeline reached 5.1 times on the
  same machine; neither comes near 16.
- The experiment runs its checks one after another in one process, so from the second run on the code of the
  check is compiled. The published executable starts anew every time and compiles that code during its first
  moments (with full optimization, see [Compilation](#compilation)), which counts in its elapsed time; this is part
  of why its 1.86 s is above the 1.62 s here. The start of the runtime itself, 0.24 s for `--help`, is not in the
  elapsed time.
- One thread that checks lines handles both files at 353 MiB/s, which is about one core's rate
  ([One thread](#one-thread) has 342 to 536 MiB/s, with one thread that reads, cuts and checks). In the check the
  two files are read and cut into chunks by two more threads, which `--threads` does not count.

### Why the check is parallel

The first version was simpler: one thread per file read, split, checked and hashed, as the plan preferred, and
the plan set a rule: if that does not check a pair of 1 GB in 3 seconds, a file is checked in parallel. It did
not. Five runs for each of three sizes of the read buffer gave 4.8 to 5.1 s. Reading the two files without
checking them took a fraction of a second, so the processor was the limit: about 100 ns per line, for 43 million
lines per file. Part of it was an accident of memory layout: the objects of the two scans were created one after
another by one thread and lay side by side, so two threads kept writing to the same cache lines. Creating each
scan on its own thread gave 4.2 s, still above the requirement.

That version is not in the repository, and its numbers were taken earlier the same day, when the machine was
faster; the rows of the table above with one and two threads are its closest reproducible relatives. The parallel
version follows the design that the plan had ready: chunks cut at line ends, checked independently, merged in
order.

### Chunk size

The default pair, 16 threads, five runs per row (`check --chunks 1,4,16`):

| Chunk | Median, s | Median, MiB/s | Slowest | Fastest |
|---|---|---|---|---|
| 1 MiB | 1.48 | 1384 | 788 | 1532 |
| 4 MiB | 1.61 | 1271 | 1065 | 1482 |
| 16 MiB | 1.70 | 1204 | 918 | 1379 |

1 MiB is the fastest in this series and was equal to the others within the noise in an earlier one (the slowest
run of the first row is the first run of the process, which compiles the code). **1 MiB** is used: the chunks in
flight are the largest part of the checker's memory, one per thread, and a smaller chunk is no slower.

### One thread

BenchmarkDotNet, one core, no disk (`micro --filter *ScanBenchmarks*`): 64 MiB of sorted generated lines are read
from memory as chunks, and every chunk is checked on the calling thread.

| Benchmark | Content | Mean | Throughput | Allocated |
|---|---|---|---|---|
| Chunks of the input: split, parse, hash | Default | 119.4 ms | 536 MiB/s | 15.7 KB |
| Chunks of the output: the same and the order | Default | 176.0 ms | 364 MiB/s | 25.8 KB |
| Chunks of the input | `--edge-cases all` | 127.7 ms | 501 MiB/s | 15.6 KB |
| Chunks of the output | `--edge-cases all` | 187.1 ms | 342 MiB/s | 26.1 KB |

- **Nothing is allocated per line.** The 64 MiB are 2.7 million lines in 64 chunks: the allocated bytes are a few
  hundred per chunk (the result of the chunk, and for the output its order check).
- A line of default content is about 25 bytes with its terminator, so one thread spends about 44 ns per line of
  the input and 65 ns per line of the output. The hash takes 16 ns of that (see below), a quarter of the output's
  time: well under the half at which the plan would have reopened the choice of the hash.
- One core checks the output at 342 to 364 MiB/s, at the edge of the 341 MiB/s per file that the requirement
  needs, and that is with the file already in memory: one thread per file would also have to read it.
- The standard deviation of these four rows is 13% to 18% of the mean: the benchmarks were run on a machine that
  slows down as it heats and was busy with other programs. Compare numbers within one table, not across tables.

### The hash of a line

BenchmarkDotNet, one core (`micro --filter *LineHash*`), in an earlier series: each variant hashes a million
generated lines, about 24 bytes each, and adds the hashes up. The code it measures did not change since.

| Hash | Mean per line | Ratio |
|---|---|---|
| `XxHash128` (used) | 16.2 ns | 1.0 |
| `XxHash3`, 64 bits | 12.9 ns | 0.8 |
| SHA-256 | 298 ns | 18.6 |

- The 128-bit hash costs 3 ns per line more than the 64-bit one, and buys an argument that needs no fine print
  (see [How a miss can happen](#how-a-miss-can-happen)).
- SHA-256 needs no package and is 18 times slower: a cryptographic strength that a checker of mistakes does not
  need.
- The million lines are not a short series repeated in a loop: the lengths of the lines are random, and a short
  series would let the processor learn them by heart.

### Compilation

The checker turns tiered compilation off: every method is compiled with full optimization the first time it runs.
A check is one hot loop that lasts seconds, and with tiered compilation the first second of it ran in unoptimized
code. The published executable on the default pair, three runs each:

| Compilation | Elapsed |
|---|---|
| Full optimization at once (used) | 1.91 to 2.20 s |
| Tiered, the default of .NET (`DOTNET_TieredCompilation=1`) | 2.60 to 3.89 s |
| Tiered, without the profile-guided step (`DOTNET_TieredCompilation=1`, `DOTNET_TieredPGO=0`) | 2.51 to 3.02 s |

The price is the start: `--help` takes 0.24 s. The setting is one line of the project file.

### Memory

Nothing in the checker grows with the size of the files:

| Item | Size at 16 threads | Scales with |
|---|---|---|
| Chunks handed out to the workers, one buffer each, and the chunk being read in each file | 18 MiB | `--threads`; a line longer than a chunk makes the buffer of its chunk as long as two such lines (the line and the copy of the line before it) |
| The first ten violations of each chunk, two shortened lines each | under 10 KB per chunk | constant |
| Runtime, compiled code, thread stacks | the rest of the peak working set | `--threads` |

The buffers of the chunks come from the framework's pool of arrays and go back to it as soon as a chunk is
checked; a line is never copied but into the buffer of the next chunk, and the result of a chunk holds counts and
nothing as long as a line. A check therefore allocates a few small objects per chunk and nothing per line (see
[One thread](#one-thread)), and the garbage collector stays in its default mode: there is next to nothing for it to
collect. Files read through the file cache occupy the cache of the operating system, not the working set of the
process.

The requirement is under 256 MB whenever no line is longer than 1 MB, for every accepted `--threads`. The peak
working set reported by the published executable, one run per cell unless several are given; "two cores" is the
same run limited to two logical processors (`start /affinity 3`), where the chunks wait longest for a worker:

| Files | `--threads` | Two cores | 16 logical processors |
|---|---|---|---|
| The default pair of 1 GB | 16 | 51 MiB | 66 MiB |
| The default pair of 1 GB | 64 | 100 MiB | 79 MiB |
| 100 lines of 1 MB each, 100 MB a file | 16 | 89 to 112 MiB (9 runs) | 73 to 77 MiB (9 runs) |
| 100 lines of 1 MB each, 100 MB a file | 64 | 97 to 160 MiB (10 runs) | 70 to 79 MiB (10 runs) |

The first version did not meet the requirement, and the review of this change found it. Every chunk handed out
holds its buffer, and `--threads` accepted up to 256: on two cores, where the chunks pile up in front of the
workers, 256 threads took 323 MiB. And every chunk copied lines into arrays of their own: its first line and its
last line, for the comparison with the chunks around it, and each line to compare the next one with. With lines
of 1 MB that is megabytes of garbage per chunk, which the collector reclaims late, and 16
threads on 16 processors took up to 251 MiB, 64 threads on two cores 342 MiB. Now `--threads` accepts up to 64,
which is far more than any disk can feed, and the lines stay in the buffers: the last line of a chunk travels to
the next chunk in front of its bytes.

### Reproducing

```powershell
# The pairs: about 4.2 GB written in all, once. Every measurement after that only reads.
FileSort.Generator --output data/check-default-1gb.txt --size 1GB --seed 1
FileSort.Generator --output data/check-edge-1gb.txt --size 1GB --seed 1 --edge-cases all --line-ending crlf
FileSort.Generator --output data/check-small-100mb.txt --size 100MB --seed 1
dotnet run -c Release --project FileSort.Benchmarks -- sorted-copy --input data/check-default-1gb.txt --output data/check-default-1gb.sorted.txt
dotnet run -c Release --project FileSort.Benchmarks -- sorted-copy --input data/check-edge-1gb.txt --output data/check-edge-1gb.sorted.txt
dotnet run -c Release --project FileSort.Benchmarks -- sorted-copy --input data/check-small-100mb.txt --output data/check-small-100mb.sorted.txt

# End to end
publish/checker/FileSort.Checker.exe --input data/check-default-1gb.txt --sorted data/check-default-1gb.sorted.txt

# Threads and chunk size
dotnet run -c Release --project FileSort.Benchmarks -- check --input data/check-default-1gb.txt --sorted data/check-default-1gb.sorted.txt --chunks 1 --threads 1,2,4,8,16
dotnet run -c Release --project FileSort.Benchmarks -- check --input data/check-default-1gb.txt --sorted data/check-default-1gb.sorted.txt --chunks 1,4,16

# One thread and the hash
dotnet run -c Release --project FileSort.Benchmarks -- micro --filter *ScanBenchmarks* *LineHash*

# Memory with lines of 1 MB: 100 lines of 1048003 or 1048004 bytes, shuffled and sorted, 0.2 GB in all
$text = 'a' * 1048000; $random = [Random]::new(1)
(0..99 | Sort-Object { $random.Next() } | ForEach-Object { "$_. $text`n" }) -join '' | Set-Content data/long-lines.txt -NoNewline -Encoding ascii
(0..99 | ForEach-Object { "$_. $text`n" }) -join '' | Set-Content data/long-lines.sorted.txt -NoNewline -Encoding ascii
publish/checker/FileSort.Checker.exe --input data/long-lines.txt --sorted data/long-lines.sorted.txt --threads 64

# The same check limited to two logical processors
cmd /c start /wait /affinity 3 /b "" publish\checker\FileSort.Checker.exe --input data\long-lines.txt --sorted data\long-lines.sorted.txt --threads 64
```

`sorted-copy` reads a file of up to 2 GB into memory, orders an index of its lines with the checker's own
comparison and writes the lines out; a 1 GB file takes about a minute. The copy serves only to measure time: it
proves nothing about the checker, and the tests do not use it.

## Design decisions

Decisions about the whole solution are in the [root README](../README.md#design-decisions); these are the ones of
the checker.

- **The checker shares no line code with the other programs.** It has its own splitting, parsing and comparison,
  in the plainest form that is correct, so that a bug in the optimized code of the sorter cannot be confirmed by
  the same code. The project has no reference to `FileSort.Common`, and a test fails if its assembly ever gets
  one. What it shares, through `FileSort.Cli`, is console plumbing: exit codes, the console, the progress line,
  Ctrl+C.
- **Numbers are compared as digits.** Leading zeros are dropped, the longer run of digits is the greater value,
  and runs of equal length compare digit by digit. This works for a Number of any length, with one code path: a
  64-bit fast path would save a few nanoseconds where Strings are equal and would give the oracle a second way to
  be wrong.
- **A sum of 128-bit hashes for "the same lines".** It needs no memory, does not depend on the order, counts a
  line as many times as it occurs, and its chance of a miss needs no fine print. Sorting the input a second time
  and comparing byte by byte would be a second sorter. The package is `System.IO.Hashing`, Microsoft's, from the
  repository of the runtime, with no dependencies; with `System.CommandLine` it is one of the checker's two
  packages, and a check publish with trimming covers both.
- **One line ending for the pair, taken from the input.** Detecting the line ending of each file separately would
  let a sorter that converts line endings pass, and would detect the output wrongly whenever its first String
  ends with a carriage return.
- **An invalid line stops the check; an order violation does not.** The first leaves nothing to go on with; the
  second is exactly what the user wants counted.
- **Parallel within a file, because one thread per file was measured too slow** (see
  [Why the check is parallel](#why-the-check-is-parallel)). The parallel code is kept as plain as it can be: a
  chunk is checked by code that knows nothing of threads, and everything that depends on order happens on one
  thread per file. Tests check that the result does not depend on the size of a chunk or on the number of
  threads, against values computed by a naive comparison.
- **Few switches.** There is no option for the size of a chunk or the limit on a line: the defaults are chosen by
  measurement, and an oracle with fewer switches has fewer ways to be run wrong. `--threads` exists because the
  degree of parallelism must be configurable on a machine that is not known in advance.
- **No console UI framework**, for the reasons the generator gives: the output is plain `Name: value` lines that a
  script can read, and the tool has to pass the trim check and to start fast as a single file.

## Tests

- Every scenario of the specs `output-checker` and `sort-order` has a test, except those that are measurements
  (throughput, memory, publishing; see [Performance](#performance)), and every scenario of `line-format` that is
  about reading is run against the checker's own splitting and parsing.
- The checker is tested against two things that share no code with it and live in the test project: a naive
  parser, which carries the Number as a `BigInteger`, and a naive comparison and sort built on it.
- **Correct pairs**: files made by the generator in memory, plain and with every edge case (Numbers above 64 bits
  and Strings that end with a carriage return among them), each with a few lines added by hand that the generator
  writes rarely or never (a Number of 39 digits, a String that ends with a space), sorted by the naive sort, must be
  `CORRECT`.
- **The catalogue of breakages**: a correct pair is broken in one way at a time, each a kind of wrong output that
  a sorter can plausibly produce, and each must give its result: a line removed, written twice or replaced by a
  copy of another; a pair of identical lines replaced by another pair; neighbours swapped; Numbers ordered as
  text; Strings ordered without regard to case or by UTF-16 code units; one byte changed; a leading zero removed;
  two lines joined; the file cut inside a String, a character or a Number; an empty line; a byte order mark in
  the middle; the other line ending.
- **Chunks and threads**: the counts, the line numbers, the first violations and the first invalid line are the
  same for chunks of 64 bytes and of 4 MiB, on 1 thread and on 32.
- The application is run in-process through its command line, with a console of the tests and files of a few
  bytes: the reports, the exit codes, a console that cannot be written to, cancellation.
- The code coverage of the solution is in the [root README](../README.md#code-coverage).

## Limitations

- **A line longer than 64 MiB ends the check** with exit code 1. No real `Number. String` file comes near it.
- **The check does not say which lines differ**, only that they do, and by how many lines.
- **A miss is possible in principle**, with a probability below 2^-93, and the hash is no defence against an
  output forged on purpose (see [How a miss can happen](#how-a-miss-can-happen)).
- **For large files with a fault in each**, which of the two faults is reported can depend on timing (see
  [How it works](#how-it-works)).
- **Both files are required.** There is no mode that checks only the order or only the validity of one file.
- **Lines with equal keys are accepted in any order**, although the sorter writes them in the order of their bytes:
  the checker answers whether a file is sorted, and for the task both orders are sorted; it counts such pairs in the
  summary. The sorter's other decisions agree with the checker's: an invalid line stops either program at the first
  such line, and the sorter writes no byte order mark, which the result of a check does not depend on anyway.
- **100 GB was checked once**, on the input and the output of the sorter's sort of that size; the other figures are
  for files of up to 1 GB read from the file cache.
- **Windows-only measurements.** The code is cross-platform, but it was developed and measured on one Windows
  machine.
- **Trim analysis covers trimming only**, as for the generator: a check publish with `PublishTrimmed` reports no
  warnings for the checker and its two packages; AOT warnings inside the packages would need the AOT compiler.
