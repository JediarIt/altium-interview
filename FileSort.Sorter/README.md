# FileSort.Sorter

The external sorter of [FileSort](../README.md). It sorts a file of `Number. String` lines by String and then by
Number within a given memory, whatever the size of the file: chunks of the input are sorted in memory and kept as
sorted runs in a temporary folder, then merged into the output. The line format, the order of lines and the
assumptions about them are in the [root README](../README.md#assumptions).

## Contents

- [Usage](#usage)
- [Options](#options)
- [What the output holds](#what-the-output-holds)
- [Invalid input and long lines](#invalid-input-and-long-lines)
- [The output file](#the-output-file)
- [Temporary files and free space](#temporary-files-and-free-space)
- [Memory](#memory)
- [What the sorter prints](#what-the-sorter-prints)
- [Exit codes](#exit-codes)
- [How it works](#how-it-works)
- [Performance](#performance)
- [Design decisions](#design-decisions)
- [Tests](#tests)
- [Limitations](#limitations)

## Usage

```powershell
# Sort test.txt into sorted.txt
FileSort.Sorter --input data/test.txt --output data/sorted.txt

# The same, with the short names of the options, replacing sorted.txt without a question
FileSort.Sorter -i data/test.txt -o data/sorted.txt --overwrite

# Less memory and fewer threads, the runs in another directory
FileSort.Sorter -i data/test.txt -o data/sorted.txt --overwrite --memory 512MB --threads 4 --temp data/runs

# In a script: the exit code is the answer, the summary goes to a file
FileSort.Sorter -i data/test.txt -o data/sorted.txt --overwrite > data/sort.log

# Then check the result with the checker
FileSort.Checker -i data/test.txt -s data/sorted.txt
```

`FileSort.Sorter` stands for either way of starting it: `dotnet run -c Release --project FileSort.Sorter --` or the
published build (see [Quick start](../README.md#quick-start)). Unlike the generator and the checker, the sorter is
published for the installed .NET runtime, not as a self-contained executable: QA engineers make test files and check
results, and the sorter is what they test. It runs on .NET 10, or on the next major version where .NET 10 is not
installed. The directory of `--temp` must exist.

## Options

| Option | What it does | Accepted values | Default |
|---|---|---|---|
| `--input`, `-i` | The file to sort. It is only read. | A file path | Required |
| `--output`, `-o` | The sorted file to write, see [The output file](#the-output-file). | A file path, not the same file as `--input` | Required |
| `--temp` | The directory in which the sorter creates its temporary folder of runs, see [Temporary files and free space](#temporary-files-and-free-space). | An existing directory | The directory of the output |
| `--memory` | The largest peak working set of the process, see [Memory](#memory). | A size such as `512MB` or `1.5GB`, at least `256MB`; units are binary (`1MB` is 1048576 bytes) | `1GB` |
| `--threads` | The largest number of threads that parse, sort and merge lines at the same time. It does not change the output. | A whole number from 1 to 64 | The number of logical processors, at most 64 |
| `--line-ending` | The line ending of the input and of the output, see [Line endings](#line-endings). | `lf` (`\n`), `crlf` (`\r\n`), or `auto`: taken from the first line feed of the input | `auto` |
| `--overwrite` | Replace an existing output without asking. | A flag | |
| `--help`, `-h`, `-?` | Prints the usage text. | A flag | |
| `--version` | Prints the version. | A flag | |

A test (`ReadmeOptionsTableTests`) compares the option names and the fixed defaults of this table with the command
line itself, so the table cannot silently fall behind the code.

## What the output holds

Exactly the lines of the input, each as many times as the input has it and with the same bytes, in order; every line
followed by the terminator of the line ending, the last one too; and nothing else. Nothing is trimmed, no leading
zero is removed, no character is changed: the sorter copies lines as bytes and never composes one.

The order: Strings first, compared byte by byte as unsigned bytes, which for UTF-8 is the order of the code points
and is case-sensitive; then the values of the Numbers, which may have any number of digits. Lines with equal keys
are written in the order of their bytes, so a file with

```
7. Apple
007. Apple
07. Apple
00. Apple
0. Apple
```

is sorted into

```
0. Apple
00. Apple
007. Apple
07. Apple
7. Apple
```

The Number 0 comes before the Number 7, and within each value the bytes decide: `0. Apple` comes before `00. Apple`
because its second byte is a period, which is smaller than the second `0` of `00. Apple`. This makes the output
depend only on the bytes of the input and on the line ending: not on `--threads`, `--memory` or `--temp`, nor on
where the input was cut into chunks. Keeping such lines in the order of the input would need the position of every
line in its index entry, and no checker that reads the output once could confirm it.

An empty input gives an empty output, and so does an input that holds only a byte order mark.

### Line endings

A file has one line ending for all its lines, and the output is written with the line ending of the input, so a
copied line keeps its bytes.

- `--line-ending lf` or `crlf` reads the input with that line ending, literally.
- `--line-ending auto`, the default, looks at the first line feed of the input: a carriage return right before it
  means `\r\n`, anything else means `\n`, and so does an input without a line feed.

With `\n`, a carriage return is a byte of the String, at its end too: the input `2. Banana\n1. Apple\r\n` has the
Strings `Banana` and `Apple` followed by a carriage return, and is sorted into `1. Apple\r\n2. Banana\n`. With
`\r\n`, a line feed without a carriage return before it makes the line not valid. The detection is wrong for an
`\n` file whose first String ends with a carriage return, which then reads as an `\r\n` file and stops at its
second line; `--line-ending lf` sorts it. The generator never writes such a String on the first line.

### Byte order mark

The bytes `EF BB BF` at the start of the input are skipped: they are not part of the first line. The output never
starts with them; the summary says whether the input did.

## Invalid input and long lines

Every line must be valid for the line ending of the sort: one or more digits, a period and a space, and a String
of well-formed UTF-8. The first line that is not, in the order of the file, stops the sort with exit code 3:

```
Invalid line in the input: C:\data\test.txt
  Line:           2
  Offset:         9 bytes from the start of the file
  Reason:         the line does not start with a digit
  Line ending:    lf (\n), from the input; give --line-ending if that is wrong
No output was written.
```

A line may be up to 1 MiB long, its terminator not counted. A longer line ends the sort with exit code 1 and a
message of the same form, which says that a file read with the wrong line ending can look like one long line. The
limit is the sorter's, not the format's: the merge holds the current line of every run in a buffer of its own, and
the limit keeps that buffer, and with it the memory, bounded for every input the sorter accepts. No real
`Number. String` line comes near it. When the input has both, the one nearer to the start of the file is reported.

All lines are checked while the chunks are made, before anything is written to the output, so an invalid input
never leaves a partial output, and an output that existed before is left as it was. The temporary folder is deleted.

## The output file

- **The output appears only when it is complete.** It is written as `<output>.tmp` next to the output and renamed at
  the end, so the output path never holds a partial file. The temporary file is created, empty, before the input is
  read: an output path that cannot be written fails at once, not after the runs of a large input are made. A
  `<output>.tmp` left by a sort that was killed is replaced.
- **An existing output is replaced only with consent**: `--overwrite`, or `y` or `yes` at the question
  `'C:\data\sorted.txt' already exists. Replace it? [y/N]`, which is asked before anything is read. Anything else,
  Enter included, leaves the file untouched and ends with exit code 1. With no console to ask, when standard input
  or the error stream is redirected, that counts as a refusal, and the message names `--overwrite`. A file that
  appears at the output path while the sort runs, when there was none to ask about and no `--overwrite`, is not
  replaced either: the sort ends with exit code 1 before it writes the output.
- **The old output is deleted as late as possible**: once the whole input has been read and checked, right before
  the output is written. A sort that fails earlier, on an invalid line for example, leaves it as it was.
- **The input cannot be the output.** The same file in `--input` and `--output` is an invalid argument (exit code
  2), and nothing is read or written. The old output is deleted before the new one is written, to give its space to
  the output; with the input as the old output, a failure or a Ctrl+C during the final merge would lose the input.
  The full paths are compared as the file system of the platform compares names; a second name for one file, such
  as a link, is not looked for (see [Limitations](#limitations)).

## Temporary files and free space

- **The runs live in a folder of the sort's own**, created in the directory of `--temp`, by default the directory of
  the output, and named after the output with `.sort-` and a random suffix: `sorted.txt.sort-a1b2c3d4.xyz`. It is
  deleted with everything in it however the sort ends: success, an invalid input, a failure, Ctrl+C. Only a process
  that is killed leaves it behind, and its name says what it is. Nothing else in that directory is created, changed
  or deleted, apart from the output and its temporary file when the directory is the output's. An input that fits in
  one chunk is written straight to the output and gets no folder.
- **Not the system's temporary directory**: it is often on a small system disk, and a sort of 100 GB needs about
  100 GB of temporary space.
- **Free space is checked before the input is read**: about the size of the input for the runs and as much for the
  output, the sum of the two when they are on one volume. An output that is about to be replaced counts as free
  space for the output, which is written after it is deleted, but not for the runs, which are written while it still
  exists; a stale `<output>.tmp` counts for both, since it is emptied first. Whether a sort needs more than one merge
  pass depends on the length of its lines, unknown before reading, so the space is checked again before every merge
  of a group of runs. A sort that does not fit ends with exit code 1:

  ```
  Not enough free space for 'C:\data': 2147483710 bytes are needed, 1073741824 are free.
  ```

  A volume whose free space cannot be asked, as with some network paths, is not checked; a full disk then ends the
  sort with exit code 1, and the temporary files are deleted.

## Memory

`--memory` limits the peak working set of the whole process, not only the sorter's buffers. The sorter keeps 128 MiB
of it for what it does not allocate itself (the runtime and its compiled code, the stacks of the threads, the
console) and gives the rest to the engine, which allocates two buffers, once, and nothing per line:

| Buffer | At the default 1 GB | At the smallest 256 MB |
|---|---|---|
| The window: the block buffers in flight while a chunk is written, then the output buffer of the merge | 32 MiB | 32 MiB |
| The chunk buffer: the lines and the index entries of a chunk, then the buffers of the runs in the merge | 864 MiB | 96 MiB |
| Runs one merge takes, at 2 MiB of the chunk buffer each | about 430 | 47 |

The chunk buffer is at most the largest array of .NET, about 2 GiB, so more than about 2.2 GB of `--memory` is
accepted and not used. A file of 1 GB of default content needs about 1.6 GiB for its lines and entries: at the
default it makes two runs and one merge, and by this count from about 1.8 GB of `--memory` it fits in one chunk.
The default is 1 GB all the same, half of the 2 GB of the task, because the hardware the sorter will run on is
unknown, and the other half is the margin.

## What the sorter prints

Before it reads the input, the sorter prints to the error stream the settings it works with, the options that were
not given included:

```
Sorting:          C:\data\test.txt
Output:           C:\data\sorted.txt
Temporary folder: in C:\data
Memory:           1073741824 bytes (1.00 GiB)
Threads:          16
Line ending:      auto
Overwrite:        no
```

While it sorts, it draws two progress bars on the error stream with Spectre.Console: one for the chunks, over the
bytes of the input read, with the runs written so far; and one for each merge pass, over the bytes it writes, with
its number when there are several. Each shows its throughput and the time left. When the error stream is
redirected, nothing about progress is written, and a log file gets the settings and the messages only.

After a successful sort, standard output gets the summary, as `Name: value` lines that a script can read, and
nothing else. For the file of 1 GB of [End to end](#end-to-end):

```
Output:           C:\data\sorted.txt
Lines:            43065356
Size:             1073741831 bytes (1.00 GiB)
Line ending:      lf
Byte order mark:  no
Runs:             2
Merge passes:     1
Chunks:           6.88 s
Merge:            4.99 s
Elapsed:          11.89 s
Throughput:       86 MiB/s
Peak working set: 932 MiB
```

Merge passes count every merge the sort ran: each merge of a group of runs into a longer run, and the final merge
into the output. An input that fits in one chunk is written straight to the output and shows 0 runs and 0 merge
passes.

A console that cannot be written to stops the sort with exit code 1 while the output is not complete; once the output
is in place, it stays, the exit code is 0, and the error stream says, if it can, that the summary was lost.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | The output is written |
| 1 | The sort could not be done: a file that cannot be read or written, a temporary directory that does not exist, a line longer than 1 MiB, too little free space, a declined overwrite |
| 2 | Invalid arguments, the same file as input and output among them; the message names the option |
| 3 | The input is not valid: a line is not a `Number. String` line; nothing is written |
| 130 | Cancelled with Ctrl+C or a termination signal |

The codes mean the same in every program of the solution; they are defined once, in `FileSort.Cli`, and code 3 is
the checker's "a file is not valid" too. Every non-zero exit comes with a message that names the cause, and never
with a stack trace. Ctrl+C or a termination signal stops the sort within two seconds and deletes the temporary
folder and `<output>.tmp`. An old output that was to be replaced is left as it was, unless the sort had already
deleted it to write the new one: that happens at the start of the final merge, or of the write of an input that fits
in one chunk.

## How it works

The sorter is a thin console host over the engine, `FileSort.Engine`, a class library without console code. The
engine sorts in two phases:

1. **Chunks.** It reads as much of the input as fits in one buffer, parses and checks the lines on all threads,
   sorts them, and writes them as one sorted *run* to a temporary folder. It repeats this until the input ends. If
   the whole input fits in the first chunk, that chunk is the output, and no run and no temporary folder are made.
2. **Merge.** It merges the runs into the output on one thread. When there are more runs than one merge can take, it
   first merges groups of the smallest runs into longer runs.

### The chunk buffer

The engine allocates one large array for the whole sort, once and without zeroing, plus a small window (below). A
chunk's lines fill the array from its start, as they are in the file; behind them come their *index entries*, one per
line, which are what is sorted. An entry is 16 bytes:

| Field | Bytes | What it holds |
|---|---|---|
| Prefix | 8 | The first 8 bytes of the String as one big-endian number, zeros after a shorter String |
| Offset | 4 | Where the line starts in the array |
| Shape | 4 | The length of the line (21 bits) and the count of digits of its Number (10 bits) |

Lines and entries share the array, so it adapts to the length of the lines: lines of 25 bytes take 61% of it and
their entries the rest; lines of 5 bytes (`1. A` and a line feed) 24%; lines of 200 bytes 93%. A chunk ends when the
next line and its entry would not fit. The array is at most the largest array of .NET, about 2 GiB, so a 32-bit
offset reaches every byte of it.

### Reading

The input is read with positional reads, in pieces of 16 MiB, so that Ctrl+C and the progress are looked at between
them. The engine reads as many bytes as the lines and their entries can be expected to fill, judging by the average
line of the chunk before (for the first chunk, the generator's default line of 25 bytes), and cuts the chunk after
the last line feed it read. The next chunk starts where this one ended: the bytes after the cut are read again, and
nothing is copied around. If the lines were shorter than expected and their entries do not fit, the chunk ends
earlier, at the line where lines and entries fill the array.

The lines of a chunk are cut into ranges, one per thread, at line ends; each thread first counts the line feeds of
its range with a vectorized search, which gives every range its slice of the index before a line is parsed. Every
slice starts on a cache line of 64 bytes, so that two threads never write to one cache line.

A byte order mark at the start of the input is skipped. The line ending is taken from the first line feed after it
(`\r\n` when a carriage return stands right before it, `\n` otherwise and without any line feed) unless
`--line-ending` gives it; the line feed is looked for within the first longest line, because a first line without one
there is too long with either line ending. An input that is not a regular file, a device or a pipe, is refused: the
sorter needs its length and reads parts of it twice.

### Parsing, and what is reported

Every thread parses the lines of its range into its slice: the digits of the Number, the separator `. `, the String
checked for well-formed UTF-8, and the entry. A line that is not valid stops the range; so does a line longer than
1 MiB, its terminator not counted. The chunk takes the problem with the smallest offset, and since the chunks before
it had none, that is the first problem of the file, whatever `--memory` and `--threads` are:

- **A line that is not valid** ends the sort with exit code 3 and names the input, the number of the line (counted
  from 1), its offset in bytes from the start of the file, the reason (empty line, no Number, no separator,
  ill-formed UTF-8, a line feed without a carriage return in a `\r\n` file) and the line ending.
- **A line that is too long** ends it with exit code 1, with the number, the offset and the line ending, because a
  file read with the wrong line ending can look like one line of a hundred gigabytes. A line that is both too long
  and not valid is reported as too long, unless a line feed without a carriage return in its first 1 MiB makes it not
  valid: the reader finds that line feed while it looks for the end of the line.

All lines are checked while the chunks are made, before anything is written to the output, so an invalid input never
leaves a partial output, and an output that existed before is untouched.

### Sorting a chunk

Then every slice is sorted by one thread, in *segments* of up to 2^20 entries, with a look at Ctrl+C between
them: a sort routine cannot be stopped in the middle, and with `--threads 1` a chunk is tens of millions of entries.
The comparison:

1. Different prefixes decide, compared as numbers, which is the same as comparing the first 8 bytes of the Strings.
   Most comparisons end here, without reading a line.
2. Equal prefixes: when one String has 8 bytes or less it is the beginning of the other one, so the shorter comes
   first; otherwise the bytes after the first 8 decide.
3. Equal Strings: the values of the Numbers, from their digits: without leading zeros the longer run of digits is the
   greater value, and runs of equal length compare digit by digit. No Number is too long.
4. Equal values: the bytes of the Numbers, like Strings. Lines with equal keys come out in the order of their bytes:
   `0. Apple`, `00. Apple`, `007. Apple`, `07. Apple`, `7. Apple`.

### Writing a chunk: the block merge

After the sort a chunk is a set of sorted segments, about 32 at the default settings, and the run must be one sorted
sequence. Merging the segments on one thread would cost more than everything else done to the chunk, so the output of
the chunk is cut into *blocks* that are merged at the same time:

1. From every segment the engine takes evenly spaced entries, as many as there will be blocks (one per MiB of lines),
   sorts these samples and takes every S-th of them as a *splitter*, S being the number of segments.
2. A binary search in every segment finds where each splitter falls. Block b holds, from every segment, the entries
   from splitter b - 1 up to splitter b.
3. Worker threads merge blocks with a loser tree into buffers, and one writer thread writes the buffers to the run in
   the order of the blocks.

For example, if the splitters are `Apple…` and `Banana…`, one block holds every line from `Apple…` to just before
`Banana…`, taken from every segment, and while one thread merges it, another merges the block from `Banana…`.

A key that a million lines share would put all of them into one block, so splitting uses a strict order: the order
of lines, then the number of the segment, then the index in the sorted segment. Entries with one key are then split
between blocks; such lines are identical, so their order changes no byte of the output.

The buffers of the blocks in flight are limited in bytes by a window of 32 MiB, not in number. Blocks start strictly
in order, and a block starts only when its exact size, summed from the lengths in its entries, fits in what the window
has left; the writer frees the window as it writes. Every block before a waiting one has started and will finish, so
room always comes. A block larger than a quarter of the window, for example of long lines that sort next to each
other, gets no buffer: it starts only when it is the next to be written, and its worker writes it to the run itself,
piece by piece, through a buffer of one longest line set aside in the window.

### The merge and its passes

A run reader keeps a buffer of its run, a slice of the chunk array, and the current line, parsed into an entry that
points into that slice, so that lines of different runs compare with the same comparison. Lines read from a run are
not checked again: they were checked when their chunk was made. A loser tree finds the least line among the runs with
about log2 of their number comparisons, and the line is copied, with its terminator, to the output buffer, the window
of the first phase.

Every run gets an equal share of the array, from 2 MiB (two longest lines) up to 16 MiB. One merge therefore takes at
most as many runs as there are buffers of 2 MiB in the array: about 430 at the default memory, which is about 220 GB
of input of default content. With more runs, groups of the smallest runs are merged into longer runs first, each group
just large enough that the runs left fit into one final merge: with k runs and room for F, a group of k - F + 1 runs
when that is at most F, otherwise a group of F and the rule again. For 700 runs and room for 430, that is one group of
271 runs, not 430. Every run of a group is deleted as soon as the group is merged.

A run holds the lines of the output, each followed by the output's terminator: the merge copies bytes and never
composes a line.

## Performance

The machine these numbers come from, and how far they can be trusted, are described in the
[root README](../README.md#performance). The first sections measure the published sorter on files, and every output
was checked by the checker; the sections from [The parts in memory](#the-parts-in-memory) on measure the parts of
the engine in memory, which chose them. The runs on files wrote about 3 GB in all, the one run of 100 GB about
300 GB more, and the one run of 10 GB about 30 GB.

### End to end

The published build, one run of each; the file of 100 GB was sorted by the same assembly from the build output,
started with `dotnet`, and the file of 10 GB by the build output after the change described in [Memory](#memory-1):

| Input | `--memory` | Threads | Runs | Merge passes | Chunks | Merge | Whole | Peak working set |
|---|---|---|---|---|---|---|---|---|
| 100 MB of default content | 1 GB, the default | 8 | 0 | 0 | 0.92 s | 0.00 s | 0.95 s | 226 MiB |
| 1 GB of default content | 1 GB, the default | 16, the default | 2 | 1 | 6.88 s | 4.99 s | 11.89 s | 932 MiB |
| 10 GB of default content | 1 GB, the default | 16, the default | 20 | 1 | 64.70 s | 97.45 s | 162.18 s | 935 MiB |
| 100 GB of default content | 1 GB, the default | 16, the default | 195 | 1 | 653.04 s | 1495.52 s | 2148.58 s | 1019 MiB |

- **The file of 1 GB**, with default options on the whole machine, sorts in 11.9 s, five times under the minute of
  the task, with a peak working set of 932 MiB: under its 1 GB of `--memory`, and under half of the 2 GB of the
  task. One run stands behind the figure: with a margin of five times, the noise of about 10% between runs changes
  nothing, and every run writes 2 GB.
- **The file of 100 GB**, 4.3 billion lines, sorts in 35 min 49 s, and the checker found the output correct (in
  3 min 18 s). It ran once: the sort writes about 200 GB. A control sort of the file of 1 GB just before it took
  12.28 s, so the machine was in its usual state. The input was generated ten minutes before the sort, which left
  the SSD time to empty its fast cache; at this size neither the input nor the runs fit in the file cache of the
  operating system, so they were read from the disk.
- **The file of 10 GB**, 431 million lines, sorts in 2 min 42 s, with 20 runs and one merge, and the checker found
  the output correct. It ran once, after the sort of 100 GB, to see the memory after the change described in
  [Memory](#memory-1) and to put a third point between the other two; a control sort of the file of 1 GB just before
  it took 11.19 s.
- **The merge**, on one thread, is 42% of the sort of 1 GB, 60% of the sort of 10 GB and 70% of the sort of 100 GB.
  It grows faster than the input: per GB the chunks took about the same time in the three sorts (6.9, 6.5 and
  6.5 s), the merge 5.0 s with 2 runs, 9.7 s with 20 and 15.0 s with 195, where the loser tree makes about eight
  comparisons a line instead of one. The sort of 1 GB multiplied by 100 would have promised 20 minutes; the sort
  took 36. A parallel final merge would make it faster; it was not done (see [Limitations](#limitations)).
- **A sort of 10 GB predicts the sort of 100 GB.** The merge costs a line 116 ns with 2 runs, 226 ns with 20 and
  347 ns with 195: about 80 ns and about 34 ns more for every level of the loser tree, the logarithm to base 2 of
  the number of runs. The straight line through the sorts of 1 GB and 10 GB gives 335 ns a line with 195 runs,
  1444 s for the merge of 100 GB; with the chunks at the rate of the sort of 10 GB, 647 s, that is 34 min 51 s for
  the whole sort, 3% under the 35 min 49 s measured. With the control sort of 1 GB of the same day in place of the
  one in the table, the line gives 36 min 16 s, 1% over. So a change to the merge can be measured at 10 GB, with
  30 GB written, and need not be measured at 100 GB with 300.
- **The file of 100 MB** fits in one chunk at the default memory and is written straight to the output, with no runs
  and no merge. It ran with the process limited to 8 logical processors.
- The files of 100 MB and 1 GB had been read before, so they may have come from the file cache of the operating
  system; the runs and the output were written through it.

```powershell
# The inputs, the same as for the measurements of the checker
FileSort.Generator --output data/check-default-1gb.txt --size 1GB --seed 1
FileSort.Generator --output data/check-small-100mb.txt --size 100MB --seed 1

dotnet publish FileSort.Sorter -c Release -o publish/sorter
publish/sorter/FileSort.Sorter.exe --input data/check-default-1gb.txt --output data/sorted.txt
cmd /c start /wait /affinity FF /b "" publish\sorter\FileSort.Sorter.exe --input data\check-small-100mb.txt --output data\sorted.txt --overwrite
publish/checker/FileSort.Checker.exe --input data/check-small-100mb.txt --sorted data/sorted.txt

# 10 GB, once: about 30 GB written
FileSort.Generator --output data/test-10gb.txt --size 10GB --seed 1
FileSort.Sorter/bin/Release/net10.0/FileSort.Sorter.exe --input data/test-10gb.txt --output data/sorted-10gb.txt
publish/checker/FileSort.Checker.exe --input data/test-10gb.txt --sorted data/sorted-10gb.txt

# 100 GB, once: about 300 GB written; ten minutes between the generator and the sort let the SSD's cache empty
FileSort.Generator --output data/test-100gb.txt --size 100GB --seed 1
publish/sorter/FileSort.Sorter.exe --input data/test-100gb.txt --output data/sorted-100gb.txt
publish/checker/FileSort.Checker.exe --input data/test-100gb.txt --sorted data/sorted-100gb.txt
```

### Memory

| Input | `--memory` | Threads | Logical processors | Runs | Peak working set |
|---|---|---|---|---|---|
| 1 GB of default content | 1 GB, the default | 16 | 16 | 2 | 932 MiB |
| 10 GB of default content, after the change below | 1 GB, the default | 16 | 16 | 20 | 935 MiB |
| 100 GB of default content, before the change below | 1 GB, the default | 16 | 16 | 195 | 1019 MiB |
| 100 MB of default content | 256 MB, the smallest | 8 | 8 | 2 | 160 MiB |
| 100 MB of default content | 256 MB | 64, the largest | 2 | 2 | 161 MiB |
| 100 lines of 1 MB | 256 MB | 8 | 8 | 2 | 120 MiB |

With the smallest `--memory`, the two buffers of the engine are 128 MiB (96 for the chunks and 32 for the window),
and the peak is 160 MiB: everything else, the runtime and its compiled code, the threads and the console, takes
about 32 MiB, and 64 threads on two processors add 1 MiB. At the default 1 GB the buffers are 896 MiB, and the rest
was 36 MiB with the file of 1 GB and 123 MiB with the file of 100 GB: it grew with the input, from 2 runs to 195.
The reserve of 128 MiB that the sorter keeps for that rest held in every case, by 5 MiB at 100 GB; inputs larger
than 100 GB were not tried. With lines of 1 MB the chunk buffer is not
filled: the first chunk reads as many bytes as lines of 25 bytes would fill, and the second chunk the rest.

What grew was found after the run of 100 GB, and removed. The plan of the blocks of a chunk took its samples into a
new array for every chunk: 32 segments by 527 blocks at the default memory, about 17,000 samples of 24 bytes, 400 KB.
An array of 85,000 bytes or more lives on the runtime's heap of large objects, which is freed only by a full
collection, and a sort that allocates almost nothing never causes one: 195 chunks left about 85 MB of such arrays,
which is the growth that was measured, 87 MiB. The plan now rents its two large arrays from the shared pool of
arrays and gives them back, so the next chunk gets the same ones, and a test fails if a chunk after the first
allocates enough for one array of that heap (501,528 bytes before the change). A sort in memory of 198 chunks of that
shape, a one-off check that is not part of the repository, left 117 MB on the heap of large objects before the
change and 33 MB after it, of which 32 are the window. The 100 GB were not sorted again, since that writes 300 GB:
the peak of 1019 MiB in the tables is the one measured before the change.

A sort of 10 GB after the change, 20 chunks, shows what to expect of 100 GB. Its peak is 935 MiB, so the rest is
39 MiB. Its working set, read four times a second by a small program that is not part of the repository, grew from
the second chunk to the last by 0.06 MiB a chunk, with a standard error of 0.03, and its private bytes by 0.02,
which is no growth; before the change the sorts of 1 GB and 100 GB give 0.45 MiB a chunk. At 0.06 MiB a chunk the
195 chunks of 100 GB end near 940 MiB, and the merge, whose peak was 5 MiB above the chunks, near 945 MiB, which
leaves about 80 MiB of the reserve where 5 were left. That is an estimate from 18 points, not a measurement: with
the slope two standard errors higher it is 958 MiB.

```powershell
cmd /c start /wait /affinity FF /b "" publish\sorter\FileSort.Sorter.exe --input data\check-small-100mb.txt --output data\sorted.txt --overwrite --memory 256MB
cmd /c start /wait /affinity 3 /b "" publish\sorter\FileSort.Sorter.exe --input data\check-small-100mb.txt --output data\sorted.txt --overwrite --memory 256MB --threads 64

# 100 lines of 1 MB, made by the recipe in the checker's README, Reproducing
cmd /c start /wait /affinity FF /b "" publish\sorter\FileSort.Sorter.exe --input data\long-lines.txt --output data\sorted.txt --overwrite --memory 256MB
```

### Edge cases

100 MB made by the generator with every edge case of content version 2, sorted with the smallest `--memory` on 8
logical processors: two runs and one merge, 1.71 s, a peak of 160 MiB. The checker finds the output `CORRECT` and
counts 22,504 neighbouring pairs of lines with equal keys and different bytes, which it accepts in any order; the
tests check that the sorter writes them in the order of their bytes.

```powershell
FileSort.Generator --output data/edge-v2-100mb.txt --size 100MB --seed 1 --edge-cases all
cmd /c start /wait /affinity FF /b "" publish\sorter\FileSort.Sorter.exe --input data\edge-v2-100mb.txt --output data\sorted.txt --overwrite --memory 256MB
```

### Cancellation

A script sent a real console Ctrl+C event to the published sorter while it merged the two runs of the 100 MB file
with the smallest `--memory`. The sort ended at once, well within the two seconds that the spec allows, with exit
code 130 and the message about the cancellation, and neither the output, nor its temporary file, nor the temporary
folder was left. The script is not part of the repository: it was a one-off check.

### The cost of the progress bars

The same sort in memory, 256 MiB of default content with 64 MiB of buffers (14 runs and one merge) on 8 threads,
all the logical processors the process was given, ran in turns with the bars drawn on the console and with nothing
drawn:

```powershell
dotnet build FileSort.slnx -c Release
cmd /c start /wait /affinity FF /belownormal /b "" dotnet FileSort.Benchmarks\bin\Release\net10.0\FileSort.Benchmarks.dll progress-cost --runs 5
```

| Series | Not drawn, median (min to max) | Drawn, median (min to max) | Difference | Collections of generation 0 per sort |
|---|---|---|---|---|
| 3 runs of each | 2.29 s (2.27 to 3.11) | 2.66 s (2.44 to 2.73) | +16% | not counted |
| 5 runs of each | 3.01 s (2.92 to 4.26) | 2.94 s (2.78 to 3.14) | -2% | 1 not drawn, 2 drawn |

The two series disagree by more than the effect: the machine was faster all round in the first one, and in the
second the drawn sorts were the faster ones. What the drawing costs is below the noise of the machine, and its
allocations add about one collection of generation 0 to a sort. The bars stay at ten updates a second.

### The parts in memory

The measurements below are BenchmarkDotNet's, in memory, on the default content of the generator (seed 1); none of
them writes to the disk. They ran with the process limited to 8 of the 16 logical processors, at a lower priority, so
that the machine stays usable, and with a short job:

```powershell
dotnet run -c Release --project FileSort.Benchmarks -- micro --filter *Micro.Sorter* --launchCount 1 --warmupCount 1 --iterationCount 5
```

A short job gives the mean of five measurements in one process, and on this machine the same case moves by about
10% from one run to the next, so a difference under that is no difference.

### Parsing a line

What a chunk does with every line, and what the merge does with a line it reads again from a run (one core, 2^20
lines per operation, `ParseBenchmarks`):

| Variant | Mean per line |
|---|---|
| Checked: the digits, the separator, the UTF-8 of the String, the entry | 47.4 ns |
| Already checked, in the merge: the digits and the entry | 19.2 ns |

The check of UTF-8 is most of the difference: the merge skips it, because every line of a run was checked when its
chunk was made.

### The sort routine

A chunk is sorted by every thread in its own range, in segments (D7 of the design). Three routines were measured on
the index of a chunk of 64 MiB of lines, 8 threads, segments of 2^20 entries (`ChunkSortBenchmarks`):

| Routine | Mean |
|---|---|
| The framework's sort of elements that compare themselves (`IComparable<T>`), the lines in a field of the thread (used) | 186.9 ms |
| A hand-written introsort, generic over a comparer struct | 205.9 ms |
| The framework's sort with a comparison delegate | 234.5 ms |

The rule of the design was to write the sort by hand only if it is at least 10% faster than the framework; it is
slower. The framework's sort of `IComparable<T>` elements calls the comparison directly and inlines its first step,
the comparison of the prefixes, which decides most comparisons without reading a line; only when the prefixes are
equal does the comparison read the lines, through a field of the thread that points to the chunk buffer. The
introsort stays in the benchmarks as the candidate that was measured, and every measured sort is checked for its order
after the measurement.

### The size of a segment

A sort routine cannot be stopped in the middle, so a thread sorts its range in segments and looks at Ctrl+C between
them; with `--threads 1` a whole chunk is tens of millions of entries. The size of a segment is the largest whose sort
takes at most half a second on one core (`SegmentSortBenchmarks`):

| Entries | Mean |
|---|---|
| 2^19 = 524,288 | 141.4 ms |
| 2^20 = 1,048,576 (used) | 334.4 ms |
| 2^21 = 2,097,152 | 722.3 ms |

### The index entry

The design planned an entry of 24 bytes: the first 8 bytes of the String, the value of the Number when it fits in 64
bits, the offset and the lengths, so that lines with equal Strings compare their Numbers without reading them, which
a prototype before the design had found worth its 8 bytes. The committed benchmarks do not show a gain
(`EntrySizeBenchmarks` on one segment of 2^20 entries and one core, `ChunkEntrySizeBenchmarks` on a chunk of 64 MiB
and 8 threads):

| Measurement | Entry of 16 bytes (used) | Entry of 24 bytes |
|---|---|---|
| One segment, the 16-byte case first | 315.9 ms | 277.3 ms |
| One segment, the 24-byte case first (the two methods swapped by hand) | 339.9 ms | 408.3 ms |
| A chunk, the 16-byte case first | 168.3 ms | 151.4 ms |

Whichever case runs second is faster, by 10% to 17%, in either order: the difference belongs to the machine, not to
the entry. With no gain to pay for it, the entry is 16 bytes: the prefix, the offset and the lengths, and a Number is
compared by its digits, which also removes any limit on its length from the comparison. A chunk then holds about a
fifth more lines: a line of default content takes 25 bytes and its entry 16 instead of 24.

### The size of a block

The write of one sorted chunk of 64 MiB of default content by the block merge, in memory, to a stream that keeps
nothing (M2 of the design; three runs a row, the median):

```powershell
dotnet run -c Release --project FileSort.Benchmarks -- block-merge
```

| Threads | Block of 256 KiB | Block of 1 MiB (used) | Block of 4 MiB |
|---|---|---|---|
| 1 | 480.9 ms | 472.8 ms | 481.9 ms |
| 2 | 275.3 ms | 269.5 ms | 280.6 ms |
| 4 | 143.2 ms | 177.5 ms | 180.7 ms |
| 8 | 125.1 ms | 134.0 ms | 136.8 ms |

The gap at 4 threads did not come back in a second series (`block-merge --threads 4,8 --blocks 64,128,256,1024`):
4 threads took 174.5, 169.1, 165.6 and 175.7 ms with blocks of 64, 128, 256 and 1024 KiB, and 8 threads 196.6, 128.6,
126.1 and 119.0 ms. No size from 128 KiB to 4 MiB is better than another beyond the noise of the machine, and blocks of
64 KiB start to cost. A block aims at 1 MiB of lines: the plan of the blocks stays small (one sample per segment per
MiB of lines), and a block stays far below a quarter of the window, the size from which it is written without a
buffer. One thread writes a chunk at about 135 MiB/s, eight at about 500 MiB/s, four times as fast on the four cores
that the eight threads had.

### The phases

The whole sort in memory: 128 MiB of default content, 64 MiB for the engine's buffers, which makes 7 runs merged in
one pass; the runs stay in memory, and the output is only counted (M3 of the design; three runs a row, the median):

```powershell
dotnet run -c Release --project FileSort.Benchmarks -- sort-phases
```

| Threads | Chunks | Merge | Whole | Whole, MiB/s |
|---|---|---|---|---|
| 1 | 2.46 s | 0.78 s | 3.15 s | 41 |
| 2 | 1.35 s | 0.73 s | 2.09 s | 61 |
| 4 | 0.90 s | 0.78 s | 1.67 s | 77 |
| 8 | 0.67 s | 0.73 s | 1.39 s | 92 |

The chunks phase grows faster with every thread; the merge runs on one thread in this version and takes the same
time whatever the threads, about 140 ns a line with 7 runs. With 8 threads the merge is already half of the sort.

## Design decisions

Decisions about the whole solution are in the [root README](../README.md#design-decisions); these are the ones of
the sorter.

- **The engine is a library, the program a thin host.** `FileSort.Engine` sorts a file and has no console code:
  settings in, a result out, and progress as counters that the host reads from its own thread. A test checks that
  its assembly references nothing of the console (`EngineIndependenceTests`). `FileSort.Sorter` parses the command
  line, asks before an output is replaced, draws the progress, prints the summary and turns the engine's exceptions
  into messages and exit codes. The engine writes its runs through a small interface, so the tests and the
  experiments run thousands of runs and several merge passes in memory, without a disk.
- **The line parser lives in the engine.** It is written for the index entry: it reads the digits, finds the
  separator, checks the UTF-8 of the String and writes the entry in one pass over the line, and the merge reuses it
  without the checks. `FileSort.Common` keeps the definition of the format; the checker has its own simple parser on
  purpose, so that a bug in this one cannot confirm itself, and the generator writes lines and never reads one.
- **One buffer for the lines and the entries of a chunk**, allocated once without zeroing and reused for the buffers
  of the runs in the merge: the share of each adapts to the length of the lines, and nothing is allocated per line.
- **An entry of 16 bytes.** The design planned 24, with the value of the Number for lines with equal Strings; the
  benchmarks showed no gain for it (see [The index entry](#the-index-entry)), and the author chose 16. A Number is
  compared by its digits, which also lifts any limit on its length.
- **The framework's sort**, of elements that compare themselves; a sort written by hand was slower (see
  [The sort routine](#the-sort-routine)).
- **The block merge of a chunk.** Without it, one thread would merge the sorted segments of every chunk while the
  others wait: one thread writes a chunk at about 135 MiB/s, eight at about 500 MiB/s (see
  [The size of a block](#the-size-of-a-block)).
- **A loser tree for the merge**: it finds the least of k lines with about log2 k comparisons, one per level on the
  way from the leaf of the last winner to the root. A binary heap, such as `PriorityQueue`, compares two children at
  every level on the way down, about twice as many.
- **Equal keys in the order of their bytes**, so that the output depends only on the input (see
  [What the output holds](#what-the-output-holds)).
- **`--memory` limits the whole process**, because the memory of a process is what anyone watching it sees; the
  sorter keeps a reserve for what it does not allocate itself (see [Memory](#memory)).
- **A line limit of 1 MiB**, against the checker's 64 MiB: the merge keeps a buffer of two longest lines for every
  run, and the limit keeps the memory promise for every input that the sorter accepts.
- **Free space is checked before the input is read**, so that a sort that cannot fit fails in a second and not after
  an hour of runs.
- **Spectre.Console draws the progress, in the sorter only.** The author's decision: a sort of a large file runs for
  minutes, in phases and passes, which the one rewritten line of text that the generator and the checker use shows
  poorly. RazorConsole, which the author looked at first, was dropped before any prototype: the sorter takes no input
  from the keyboard while it sorts, so a framework of components on a hosting stack buys nothing over the progress
  display of Spectre.Console. The package, with its one dependency, is referenced by `FileSort.Sorter` alone: the
  engine never sees it, and the generator and the checker, which are trimmed into single files, do not get it. When
  the error stream is redirected, Spectre.Console is not used at all.
- **Reads and writes go through the file cache of the operating system**, and reading, sorting and writing take
  turns. Overlapping them, unbuffered I/O, a parallel final merge, prefetching and the choice of the garbage
  collector's mode were left out: the sorter meets the figures of the task without them, and each would need
  measurements of its own.

## Tests

- Every scenario of the specs `external-sorter` and `sort-order` has a test, except those that are measurements
  (time and peak memory; see [Performance](#performance)), and the scenarios of `line-format` that are
  about reading are run against the engine's parser.
- **Two oracles that share no code with the engine** live in the test project: a naive parser, which carries the
  Number as a `BigInteger`, with a naive sort built on it and given the tie-break by bytes; and the checker, run
  in-process on a few outputs.
- **Lines**: the engine's parser against the naive parser on generated content with every edge case; the packing of
  an entry against worked examples, Numbers of 1022, 1023 and 1024 digits among them; the comparison against the naive
  order, on every pair of hand-written lines and on generated lines.
- **Chunks**: tiny chunk buffers and 1, 2, 7 and 16 threads put a chunk boundary and a range boundary at every
  position of a small input; the byte order mark, both line endings, a last line without a terminator, the line
  limit, and an invalid line whose number and offset must be right across both boundaries. The first problem by
  offset is reported for every buffer size.
- **Segments and cancellation**: every segment is in the naive order and starts on a cache line; a chunk sorted by one
  thread stops within the time of "Cancellation".
- **Blocks**: a line that repeats thousands of times, long lines that sort next to each other, a large block right
  after a window full of small ones, a chunk of one segment. The run must be the naive sort, and the buffers in flight
  must never pass the window. A chunk after the first, of the shape of a chunk at the default memory, must allocate
  less than one array of the heap of large objects (see [Memory](#memory-1)).
- **Merge**: many runs with tiny buffers; exactly as many runs as one merge takes, and one more; several passes; after
  every group only the runs that the plan keeps are left.
- **The whole sort in memory**: generated input with every edge case, 1, 2, 7 and 16 threads, chunk buffers from the
  smallest to one that holds the whole input, both line endings: the output must be the naive sort byte for byte.
- **Files**: the temporary folder appears with the first run and disappears with the sort; files of the user next to
  it survive; the output gets its name only when it is complete; an old output is kept on a refusal and on an invalid
  input; the free space check, with the free space given by the test.
- **The command line**, in-process with the console of the tests: the options, the exit codes and their messages, the
  question before an output is replaced, a console that cannot be written to, cancellation, the settings and the
  summary. The files are kilobytes; the largest is the line of 1 MiB of the line limit.
- **Disk**: the cases with thousands of runs and several passes run on a run storage in memory, so the tests write
  megabytes.
- The code coverage of the solution is in the [root README](../README.md#code-coverage).

### Mutations

Before the independent review, twelve faults were put into the engine one at a time, the tests were run against each,
and the fault was taken out again. Every fault made tests fail:

| Fault | Where | Tests that failed |
|---|---|---|
| The prefixes compared as signed numbers | the comparison | 20, among them `EntryComparerTests.Every_pair_of_hand_written_lines_compares_as_the_naive_order_says`, `WholeSortTests.The_checker_finds_the_outputs_correct` |
| The tie-break by bytes reversed | the comparison | 24, among them `SortScenarioTests.Equal_keys_different_bytes`, `ChunkWriterTests.A_line_that_repeats_thousands_of_times` |
| A String of 8 bytes or less put after a longer one that begins with it | the comparison | 20, among them `SegmentSorterTests.Every_segment_is_in_the_naive_order`, `FileSorterTests.A_file_sorts_into_its_naive_sort` |
| Leading zeros counted in the value of a Number | the comparison | 33, among them `EntryComparerTests.A_line_compares_equal_to_itself_and_the_order_is_antisymmetric` |
| More digits taken for a smaller value | the comparison | 54, among them `ChunkWriterTests.A_chunk_of_one_segment` |
| A bound of a block off by one entry | the plan of the blocks | 28, among them `BlockPlanTests.One_key_in_most_lines_is_still_split_into_blocks_of_about_equal_size`, `WholeSortTests.Generated_lines_sort_like_the_naive_sort_whatever_the_threads_and_the_memory` |
| A line of a block written without its terminator | the block merge | 82, among them `ChunkWriterTests.Lines_with_equal_keys_are_split_between_blocks_without_changing_a_byte` |
| The last line of a run dropped | the run reader | 24, among them `MergePhaseTests.The_runs_merge_into_the_naive_sort_in_the_merges_of_the_plan` |
| The runs of a group kept after it is merged | the merge passes | 24, among them `FileSorterTests.Nothing_left_behind_in_another_directory` |
| The number of an invalid line one too small | the parsing of a range | 42, among them `ChunkBoundaryTests.An_invalid_line_anywhere_is_reported_with_its_number_and_offset` |
| The next chunk started one byte late | the reading of chunks | 36, among them `ChunkBoundaryTests.A_chunk_ends_after_every_line_for_some_size_of_the_buffer` |
| The loser tree keeping the wrong winner | the loser tree | 71, among them `ReadingTests.Carriage_return_and_line_feed` |

## Limitations

- **The merge runs on one thread.** It is 70% of the sort of 100 GB, and with eight threads already half of a sort
  in memory (see [The phases](#the-phases)). A parallel final merge, by ranges of keys as in the block merge, would
  make it faster; it was not done.
- **Reading, sorting and writing take turns**: while a chunk is sorted, the disk waits, and while it is read or
  written, the processor mostly does.
- **A line is at most 1 MiB**, its terminator not counted.
- **A process that is killed leaves its temporary folder**, `<output>.sort-` and a random suffix, and maybe
  `<output>.tmp`; they can be deleted by hand. So does a sort whose folder another program holds open when it ends,
  a virus scanner for example: the sort still succeeds, since the output is in place.
- **Free space cannot be asked on some network paths**; there a full disk ends the sort with exit code 1.
- **Another path to the input is not always recognized** as the input. A link given as the output is harmless: the
  sort replaces the link, and the input keeps its bytes (tried with a hard link on Windows). A path that reaches the
  input's own name in a way the comparison of full paths misses, through a network share for example, makes the
  sort write in place, and a failure or a Ctrl+C during the final merge would then lose the input.
- **100 GB was sorted once, and nothing larger.** Its peak working set came within 5 MiB of `--memory`; the cause
  was removed afterwards (see [Memory](#memory-1)), and the 100 GB were not sorted again: a sort of 10 GB puts
  their peak near 945 MiB, as an estimate. Whether a much larger input stays under `--memory` was not measured.
- **Windows-only measurements.** The code is cross-platform, but it was developed and measured on one Windows
  machine.