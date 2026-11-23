# Design

## Context

See `proposal.md` for the motivation and the scope. What shapes the design:

- `FileSort.Sorter` is a placeholder that references `FileSort.Common`. `FileSort.Common` holds the constants of the
  line format and the line ending; `FileSort.Cli` holds the console plumbing (exit codes, the console abstraction,
  the progress line, Ctrl+C, command-line errors). The generator and the checker are built on them.
- The checker is the independent oracle: it shares no line code with anything, and the sorter does not reuse its
  code either. The in-memory sort `sorted-copy` in `FileSort.Benchmarks` is built on the checker's code and is not
  a starting point for the engine.
- The spec `sort-order` defines the order; the specs `line-format` and `output-checker` fix, ahead of the sorter,
  that the output has the input's lines byte for byte and the input's line ending.
- A 1 GB file made by the generator with default options has 43 million lines of 24.9 bytes on average, the
  terminator included. More than half of its lines share their String with another line (in the sorted file, 56% of
  neighbouring lines have equal Strings); a String of the pool occurs about 12,000 times per GB.
- The disk of the reference machine, measured for the generator: one writer takes about 2.5 GiB/s with unbuffered
  16 MiB writes and about 1.2 GiB/s with buffered ones; four to eight writers into separate files take 3.2 GiB/s;
  several writers into one file are slow.
- The author split the sorter into two changes (`proposal.md`). This one may not rely on anything that the second
  one adds.
- Sizes use binary units, as in the specs: 1 GB is 1 GiB, and MB and MiB mean the same.
- The specs state the contract. Details below that level (accepted ranges, buffer sizes, message texts, names of
  types) are decided here and written only here; once the code exists, the code, its tests and `--help` are their
  single home, and this document is archived with the change. The one deliberate second copy is the table of
  options in the sorter's README (D15). Type names below are for orientation and may change.

### Planning measurements

Before this design was written, two throwaway prototypes measured the steps of an in-memory sort on
`data/check-default-1gb.txt`, read into memory from the file cache. Nothing was written to disk. The prototypes are
not part of the repository, so the README will not quote these numbers; the measurements M1 to M9 below reproduce
what the README needs with committed code. Medians of three runs unless one run is given; the machine moved by 10%
to 20% between series, so a few rows give two values from two series.

| Id | Step, for the whole 1 GB file | 1 thread | 16 threads |
|---|---|---|---|
| P1 | Split, parse and validate the lines, UTF-8 included | 2.08 s (48 ns a line) | 0.40 s |
| P1 | The same without the UTF-8 check | 1.39 s | |
| P2 | Sort an index of 8-byte entries (offset and lengths), no key in the entry, the comparison in the entry (`IComparable<T>`) | 86 s (1 run) | 8.1 s |
| P2 | Sort 16-byte entries: an 8-byte prefix of the String, Numbers compared as digits | 33 s (1 run) | 3.18 to 3.90 s; 2.95 s with a tuned comparison of digits |
| P2 | Sort 24-byte entries: the prefix and the Number as a 64-bit integer | 28 s (1 run) | 2.41 to 3.38 s |
| P2 | Sort 24-byte entries: a 16-byte prefix, Numbers compared as digits | | 2.33 s |
| P2 | Sort 8-byte entries with a comparison delegate, as `sorted-copy` does | 101 s (1 run) | |
| P3 | Copy the lines out in the order of one sorted index | 3.09 s; 1.81 s with prefetch | 0.55 s; 0.44 s with prefetch |
| P4 | Merge 16 sorted parts of an index and copy the lines out, loser tree | 15.5 to 19.7 s; 10.8 s with prefetch | 1.66 s by key ranges; 1.31 s with prefetch |
| P4 | The same with `PriorityQueue` | 25.9 s (1 run) | |
| P5 | Merge 16 runs whose lines lie one after another, each line parsed again | 7.7 s (180 ns a line) | |

In P2 the 16 threads sort 16 parts of the index, one each. "Prefetch" is a hint to the processor to load the
memory of a line a few lines ahead of its use. P4 by key ranges is the block merge of D8; its output was compared
byte for byte with the sorted copy of the file. What follows from the table:

- **The prefix makes sorting 2.6 times faster**: most comparisons are decided by eight bytes inside the entry
  without reading the line. Not all: in the sorted default file 99% of neighbouring lines have the same first eight
  bytes of String, and 56% the same String. The last steps of a sort compare such neighbours and read the text.
- **The Number in the entry takes 13% to 24% off the time of the sort**: 3.90 to 3.38 s and 3.18 to 2.41 s in the
  two series, 33 to 28 s on one thread, for eight bytes more per line.
- **Comparing is cheap; collecting lines is not.** With one sorted index the addresses of the next lines are known
  in advance, and the processor fetches many of them at once (P3). When parts are merged, the next line is known
  only after a comparison, and every step waits for memory: merging the parts on one thread (P4) costs several times
  more than sorting them on 16 threads (P2). Sixteen threads that merge different ranges of keys wait at the same
  time.
- **The merge of runs costs 180 ns a line on one thread** (P5), most of it the comparisons along the tree.

## Goals / Non-Goals

**Goals:**

- Output that is right for every valid input, byte for byte, and the same for every setting.
- Memory that never passes `--memory`, whatever input is accepted.
- A 1 GB file under the task's minute on the reference machine with a wide margin, as the architecture allows
  without the tuning of the second change: the estimate from P1 to P5 is about 12 s (D3).
- Inputs of ~100 GB on the same code path: about 230 runs at the default memory, one merge pass. Shown on small
  inputs with tiny budgets and on one input of 5 GB.
- An engine that tests and measurements run in-process without a disk.

**Non-Goals:**

- Everything the second change does (`proposal.md`, Out of scope). In particular the final merge stays on one
  thread here: at 100 GB it is most of the time, about 23 minutes of an estimated half hour.
- Repairing or skipping malformed input.
- Sharing line code with the checker.
- Speed on hard disks: the merge reads many runs at once, which suits an SSD; nothing here is measured on a hard
  disk.

## Decisions

### D1. Where this change ends

This change has the whole algorithm, built so that both targets of the task are met on the reference machine:
large chunks, parsed and sorted on all cores, written as one run each by the parallel block merge of D8, merged on
one thread in one or more passes. The block merge is here and not in the second change because without it, writing
a chunk costs more than everything else in creating it (P4).

The second change gets what makes the sorter fast and safe on weaker machines and at 100 GB: reading the next chunk
and writing the previous run while a chunk is sorted; a parallel final merge, which applies the block merge of D8
to runs on disk; unbuffered reads and writes; software prefetch (P3 and P4 show 20% to 40%, but it needs unsafe
code and works on x86 only); the mode of the garbage collector; .NET 11; tiered compilation; the 100 GB run and a
time requirement for it.

### D2. Projects and what moves where

| Project | Kind | References |
|---|---|---|
| `FileSort.Engine` (new) | Class library: the sort | `FileSort.Common` only |
| `FileSort.Sorter` | Console app on the regular runtime: the command line over the engine | `FileSort.Engine`, `FileSort.Cli`, Spectre.Console |
| `FileSort.Cli` | Class library, as today, plus the pieces below | `System.CommandLine` only |

- **The engine has no console dependencies**: no `System.CommandLine`, no `FileSort.Cli`, no Spectre.Console. A
  test fails if its assembly references any of them, as the checker's test does for `FileSort.Common`.
- **The name** is `FileSort.Engine`: it differs from `FileSort.Sorter` at a glance, and `README.md` and
  `CLAUDE.md` already call it the sorting engine. `FileSort.Sorting` was the alternative; it is one syllable away
  from `FileSort.Sorter`.
- **The engine's line parser lives in the engine.** It is the sorter's hottest code and its only consumer; the
  generator writes lines and never parses them, and the checker must not share it. `FileSort.Common` keeps the
  constants of the format and the line ending; its 64-bit range (`LineFormat.MaxNumber`) is the range of the fast
  path of D6.
- **Into `FileSort.Cli`**: the parser of sizes (from the generator, for `--memory`), one parser of whole numbers
  (from the generator; the checker's own parsing of `--threads` is replaced by it) and the question before an
  existing output is replaced, with its result type (from the generator). The generator and the checker behave as
  before; their tests pass with no change other than namespaces, unless a message changes its wording, which the
  task that moves the parsers checks.
- **Spectre.Console** goes into `FileSort.Sorter` only. `FileSort.Cli` keeps `System.CommandLine` as its only
  package, so the trim check of the generator and the checker does not have to cover Spectre.
- **The sorter is not a standalone executable.** It runs on the regular runtime, so it has no option in
  `publish.yml` and no trim check. The README publishes it as a framework-dependent build.
- **Folders.** The engine: `Lines` (parsing, the index entry, the comparison), `Chunks` (the buffer of a chunk,
  reading, the sort of segments), `Blocks` (the block merge of D8 and its writer), `Runs` (the run storage),
  `Merging` (the loser tree and the passes), and the public types at the root. The sorter: `Cli`, `Cli/Options`,
  `Cli/Reports`, like the checker. A folder that grows past about seven files is split, as `CLAUDE.md` says.

### D3. The algorithm

Two phases:

1. **Chunks.** Read as much of the input as fits in the chunk buffer (D4); parse and validate its lines on all
   threads, filling an index (D5, D6); sort the index, each thread its own range, in segments (D7);
   write the chunk as one sorted
   run with the block merge (D8). Repeat until the input ends. If the input ended inside the first chunk, that
   chunk is written straight to the output, and the sort ends without a temporary folder.
2. **Merge.** Merge the runs into the output with a loser tree on one thread (D10). When there are more runs than
   one pass can hold, merge groups of runs into longer runs first.

An example with the default memory: a 1 GB file becomes two chunks of about 440 MiB of lines and a third of about
145 MiB, so three runs, merged in one pass. Estimated from P1 to P5 for the reference machine:
reading 1 GB about 0.5 s, parsing 0.4 s,
sorting 2.5 to 3.5 s, writing the runs 1.5 s plus the disk, the merge of three runs about 5 s plus reading and
writing 1 GB: about 12 s, under the minute of the task with a margin of five. A 100 GB file at the default memory
becomes about 230 runs, merged in one pass: about 10 minutes for the chunks and 23 for the merge.

### D4. The chunk buffer and reading

**One buffer for a chunk's lines and its index.** The engine allocates one large byte array for the whole sort
(D13). A chunk's lines fill it from the start; the index entries of those lines, 24 bytes each (D6), follow them.
The chunk ends when the next line and its entry would not fit. One buffer adapts to the length of the lines: lines
of 25 bytes take 51% of it and their entries the rest; lines of 5 bytes (`1. A` and a line feed) 17%; lines of 200
bytes 89%. Two separate buffers for lines and entries, each full in turn, would leave most of one of them empty
whenever the lines are not of the length they were sized for.

**Reading.** The input is opened for positional reads (`RandomAccess.Read`) with sequential access as a hint to the
system, and read in pieces of 16 MiB, so that cancellation and progress are checked between them. The engine reads
as many bytes as the lines and their entries can be expected to fill, from the average length of a line in the
previous chunk (for the first chunk, the default content's), and cuts the chunk after the last line feed it read.
The next chunk starts at the file offset where this one ended, so nothing is copied around; the bytes after the cut
are read again.

**Counting before parsing.** The lines of a chunk are parsed by all threads, each over its own range of bytes cut
at line ends. Each range first counts its line feeds (a vectorized search). The counts give every range its slice of
the index, and show whether all the entries fit. If they do not, because the lines were shorter than expected, the
chunk is cut earlier, at the line where lines and entries fill the buffer, and the rest goes to the next chunk.
Then every thread parses its range into its slice.

**Ends of the file.** A byte order mark at the start is skipped (D11). The last line may lack a terminator; it is a
line all the same. An input that is not a regular file of a known length (a device, a pipe) is refused with exit
code 1, as the checker does.

**Limits.** A chunk is at most the largest array of .NET, about 2 GiB, so a 32-bit offset in an entry reaches every
byte of it.
A range that finds no line end within the limit of D12 has found a line that is too long. It does not stop the sort
at once: the lines of the chunk before that line are parsed first, and the first problem of the file, by offset, is
the one reported (D5). The chunks before were parsed completely, so whatever `--memory` and `--threads` are, the
same input gives the same report and the same exit code.

### D5. Parsing and validating a line

The engine's parser takes a line without its terminator and checks it against `line-format`: one or more ASCII
digits, the separator `. `, and a String of well-formed UTF-8 (`Utf8.IsValid`). A line that is not valid gets a
reason: empty line, no Number, no separator, ill-formed UTF-8, or a line feed without a carriage return in a file
read with `\r\n`. While it reads the digits, the parser computes the value of the Number as a 64-bit integer and
notes whether it fits: up to 19 significant digits always fit, 20 fit unless the value passes `LineFormat.MaxNumber`,
more never do.

**The first problem of the file is the one reported.** Every thread notes the first invalid line of its range, and
the line that is too long if its range has one (D4). The chunk takes the problem with the smallest offset; its line
number, counted from 1, is one more than the lines of the earlier chunks, of the earlier ranges and before it in its
range, all counted in 64 bits. The report names the input file and gives the line number, the offset of the line
from the start of the file, the reason and the line ending in use.

All lines are checked while the chunks are made, before anything is written to the output. So an invalid line
never leaves a partial output, and an output that existed before is untouched (D14). A file of 100 GB with an
invalid line near its end fails after all its chunks were read; checking the whole file first would read it twice.

The merge of D10 parses lines that were checked when their chunk was made, so it skips the UTF-8 check: P1 shows
that the check is a third of the cost of parsing.

### D6. The index entry and the comparison

An entry is 24 bytes:

| Field | Bytes | What it holds |
|---|---|---|
| Prefix | 8 | The first 8 bytes of the String as a big-endian 64-bit integer, with zeros after a shorter String |
| Number | 8 | The value of the Number, when it fits in 64 bits |
| Offset | 4 | Where the line starts in the chunk buffer |
| Shape | 4 | The length of the line (21 bits), the length of the Number (10 bits) and whether the Number fits (1 bit) |

The prefix is read with `BinaryPrimitives.ReadUInt64BigEndian`; a String shorter than 8 bytes is padded with zeros
by a named method. The fields of the shape are written and read by small named methods with comments in plain
words, following the rule for bit operations in this project. A Number of 1023 digits or more stores 1023, which
means "count the digits again": such a Number is valid, and absurd enough that its cost does not matter.

The comparison, the order of `sort-order` with the tie-break of D9:

1. **Prefixes.** Different prefixes decide, compared as unsigned integers, which is the same as comparing the bytes.
2. **Strings.** Equal prefixes and one String of 8 bytes or less: the shorter String comes first, because it is the
   beginning of the other one, and two such Strings of the same length are equal. For example `AB` (2 bytes) and
   `AB` followed by a zero byte (3 bytes; U+0000 is valid UTF-8) have equal prefixes, and `AB` comes first.
   Otherwise the bytes after the first 8 are compared.
3. **Numbers**, when the Strings are equal: two values that fit compare as integers; otherwise the digits compare
   (leading zeros skipped, more digits is greater, then digit by digit). This is the decision of the author: a fast
   path for Numbers that fit and digits for the rest. P2 measured it against digits alone: 13% to 24% less sorting
   time for 8 bytes more per entry. The 16-byte prefix without a Number (P2) was as fast on this file and is not used.
4. **Bytes of the Numbers**, when the values are equal too: the Number with the smaller bytes first, compared like
   Strings, as D9 requires. If the Numbers have the same length too, the lines are identical.

The comparison is the most important code of the sorter. It is tested against the naive comparison of the tests on
generated lines with every edge case, and with a set of deliberately broken versions, each of which the tests must
catch (D19).

### D7. Sorting a chunk

The entries of a range of D4 lie together in the index, and the thread that parsed a range sorts its entries. That
needs no coordination and no second index. A parallel sort that produces one sorted index (a parallel merge sort, a
sample sort) would need a second index array of the same size and leave 30% less room for lines; the block merge of
D8 gets the same result from sorted pieces without it.

**Segments of bounded size, for cancellation.** A thread does not sort its whole range as one piece: it sorts it in
segments of at most a fixed number of entries, and checks the cancellation token between segments. A sort routine
cannot be interrupted in the middle, and with `--threads 1` one chunk of default content is 18 million entries,
about 11 seconds of sorting on one thread (P2), against the 2 seconds that "Cancellation" allows. The segments of
all threads are what the block merge of D8 merges. More segments make its tree deeper: at the default settings a
chunk has about 32 segments instead of 16, one more comparison a line. The size of a segment is set by M1, so that
one segment sorts in at most half a second on one thread of the reference machine.

The state of a thread is created by that thread. The segments are slices of one array, and their starts are aligned
to 64 bytes, so that two threads never write to one cache line. The checker lost a fifth of its time to that.

**Which sort routine** is decided by M1. The framework's `MemoryExtensions.Sort` cannot inline a comparison that
needs the chunk buffer: with a delegate every comparison is a call (P2: 101 s with a delegate against 86 s with the
comparison in the entry, both without a prefix), and with an `IComparable<T>` entry the buffer would have to come
from a thread-static field. The candidates
are the framework with a delegate, the framework with `IComparable<T>` and a thread-static buffer, and a hand-written
introsort that is generic over a comparer struct, which the JIT specializes. The hand-written sort is used only if
M1 shows it at least 10% faster for the whole sort of a chunk, and a comment next to it says so, as `CLAUDE.md`
requires.

### D8. Writing a chunk: the block merge

After D7 a chunk is a set of sorted segments, and the run must be one sorted sequence. Merging the segments on one
thread costs more than the rest of the chunk's work (P4). So the output of the chunk is cut into blocks that can be
merged at the same time, and each block is cut by keys:

1. **Splitters.** From every segment the engine takes evenly spaced entries, as many as there will be blocks, sorts
   these samples and takes every S-th of them as a splitter, S being the number of segments, so that each block
   gets about the same number of lines. The number of blocks aims at about 1 MiB of lines a block (M2 decides).
2. **Ranges.** For each splitter, a binary search in each segment finds the first entry that is not before it.
   Block b holds, from every segment, the entries from splitter b - 1 (included) up to splitter b (excluded); the
   first block starts at the start of each segment and the last one ends at its end.
3. **Merge.** Worker threads take blocks in order and merge the ranges of a block with a loser tree into a buffer.
   One writer thread writes the buffers to the run in the order of the blocks.

An example: if the splitters are `Apple…` and `Banana…`, one block holds every line from `Apple…` to just before
`Banana…`, taken from every segment, and while one thread merges it, another merges the block from `Banana…`.

**Repeated lines.** A key that a million lines share would put all of them into one block. To prevent that,
splitting uses a strict order: the comparison of D6, then the number of the segment, then the index of the entry in
the sorted segment (a sort routine need not keep the order of equal entries, so the index after sorting is what
counts). Entries that the comparison of D6 finds equal can then be split between blocks. Such lines are identical,
because D6 compares the bytes of the Numbers last, so their order between blocks changes no byte of the output.
With this order a block holds at most about twice the average number of lines.

**Bounded memory, no waiting in a circle.** Blocks start strictly in order: block b starts only after block b - 1
has started. The block buffers in flight are limited in bytes by the window of D13, not in number:
a block starts only when its
exact size, summed from the lengths in its entries, fits in what the window has left, and until then no later block
starts either. The writer frees room as it writes, in order, and every block before the waiting one has started and
will finish, so room always comes. A block larger than a quarter of the window, for example many lines of 1 MB that
sort next to each other, is not buffered: it starts only when it is the next block to be written, and its worker
writes it to the run itself, piece by piece, through one piece buffer kept for that purpose. No later block starts
until it has started, so nothing can fill the window behind it.

P4 measured this on the whole 1 GB file: 1.66 s with 16 threads against 15.5 to 19.7 s for one thread merging the
16 segments. The prototype started blocks in any order and had no window; M2 measures the block merge as it is
built here, to compare with P4.

### D9. Lines with equal keys

Lines with equal keys are written in the order of their bytes, as the spec requires. They can only differ in the
leading zeros of their Numbers, and since their Strings are equal, comparing the bytes of the lines is the same as
comparing the bytes of the Numbers. More leading zeros usually come first, but not always: `007. Apple` comes before
`07. Apple` and `7. Apple`, while `0. Apple` comes before `00. Apple`, because after the first `0` one line has a
period and the other a `0`, and the period is the smaller byte.

The cost is nil: the bytes are compared only when the values are equal. The other choice, the order of the input,
would need the position of a line in every entry and, between runs, the number of its run; and the checker could
not verify it while reading the output once. The naive sort of the tests gets the same tie-break, so that the tests
compare outputs byte for byte.

### D10. The merge

**A loser tree** merges k runs with log2(k) comparisons a line; a binary heap needs about twice as many. P4 measured
the tree at 24% less time than `PriorityQueue` in the same series (19.7 against 25.9 s).

**A run reader** keeps a buffer of its run and the current line, parsed into the fields of an entry that point into
that buffer; the comparison is the one of D6. When the current line reaches the end of the buffer, the rest of the
buffer moves to its start and the next bytes of the run are read after it. The buffer of a run is the memory of the
merge divided by the number of runs, between 2 MiB and 16 MiB. 2 MiB holds the longest line of D12 with its
terminator and leaves room for a read of sensible size. The cap of 16 MiB comes from the generator's measurements,
where writes of 16 MiB were the best size; reads are assumed to behave alike, and M5, whose three runs get buffers
of the cap, shows whether the merge keeps up with the disk.

**Passes.** One pass merges at most as many runs as there are buffers of 2 MiB in the merge's memory: about 430 at
the default memory, which is about 185 GB of input with default content. With more runs (a huge input or a tiny
`--memory`), the engine first merges groups of the smallest runs into longer runs, each group just large enough
that the runs left fit into one final pass: with k runs and room for F, a group of k - F + 1 runs when that is at
most F, otherwise a group of F and the rule again. Merging no more than that rewrites the fewest bytes: for 700 runs
and room for 430, one group of 271 runs, not 430. Each run of a group is deleted as soon as the group is merged.

**One thread.** The final merge runs on one thread in this change: P5 is 180 ns a line with 16 runs. At 1 GB that is
a few seconds; at 100 GB it is most of the time, and the second change makes it parallel.

**What a run holds.** A run is a file of lines in sorted order, each followed by the output's terminator: the bytes
that will be in the output. The merge copies bytes and does not rebuild lines, and a run can be checked by the
checker if a test needs to.

### D11. Line endings and the byte order mark

- **`auto`** takes the line ending from the first line feed of the input after a byte order mark: `\r\n` when a
  carriage return stands right before it, `\n` otherwise and when the input has no line feed. This is the rule of
  the checker, so the checker reads the output the same way as the sorter read the input.
- **The output** is written with that line ending, every line followed by its terminator. A carriage return that is
  not part of a terminator is a byte of a String and stays one.
- **A byte order mark** at the start of the input is skipped and never written: it is not part of a line, and the
  output is the sorted lines. The checker accepts a mark in either file, so this costs no compatibility.

### D12. The length of a line

A line may have up to 1 MiB, not counting its terminator. A longer line ends the sort with exit code 1. The message
names the line, by number and offset, and mentions the line ending, because a file read with the wrong line ending
can look like one line of a hundred gigabytes.

Why 1 MiB and not the 64 MiB of the checker: the merge holds the current line of every run in that run's buffer, so
the longest line sets the smallest buffer, and the smallest buffer sets how many runs one pass can merge. With 1 MiB
the memory promise of the spec holds for every file the sorter accepts. A line of `Number. String` has never come
near that limit: the generator writes lines of about 25 bytes by default, and about 13 KB with the largest
`--words`. The checker can afford more, because it holds a line or two at a time. The limit is a setting of the
engine, so that tests use small lines with small buffers.

A line that is both too long and not valid is reported as too long, unless what makes it invalid lies within its
first 1 MiB: a range finds a bare line feed of a file read with `\r\n` while it looks for line ends, so such a line
is reported as not valid, as the checker does.

### D13. Memory

`--memory` is the limit of the whole process. The host subtracts a reserve for everything the engine does not
allocate and passes the rest to the engine, which divides it:

| Item | At the default 1 GB and 16 threads | Scales with |
|---|---|---|
| Reserve: runtime, compiled code, thread stacks, the console, small objects | 128 MiB, to be confirmed by M6 | threads, a little |
| The window of D8: the block buffers in flight, and 1 MiB of it set aside as the piece buffer of a large block; reused as the output buffer of the merge | 32 MiB | fixed; tests with tiny budgets set it smaller |
| The chunk buffer (D4), reused as the run buffers of the merge (D10) | the rest, 864 MiB | `--memory` |

- **Phase 1**: lines and entries of a chunk fill the chunk buffer; with default content about 440 MiB of lines and
  424 MiB of entries.
- **Phase 2**: the chunk buffer becomes the run buffers of the merge. Nothing new is allocated.
- **Allocation**: the chunk buffer once, with `GC.AllocateUninitializedArray`, and the block buffers once, as a pool
  of the engine. Nothing is allocated per line, and next to nothing per block, so the garbage collector has nothing
  to do and stays in its default mode; choosing a mode is the second change's.
- **Accepted values**: `--memory` takes a size as the generator's `--size` does, from 256 MB, where the chunk buffer
  is 96 MiB, with no upper limit. Above about 2.2 GB the chunk buffer stops growing (D4): more memory is accepted
  and not used, which the README says.
- **Threads**: `--threads` accepts 1 to 64, by default the number of logical processors or 64, whichever is smaller.
  A thread adds its stack, of which only the used part is committed, and a few small arrays.
- **Expected peak**: just under the value of `--memory`, because the chunk buffer is written through in every chunk
  and the reserve is chosen to hold everything else. M6 measures the default and the worst cases of the spec.

Why the default is 1 GB and not 2: a 1 GB file does not fit in one chunk even with 2 GB, since its lines and entries
need 2 GiB. More memory therefore only turns three runs into two, a second or two, while half of the task's budget
left unused is the margin for an unknown machine. Inputs of short lines need more runs: 100 GB of 5-byte lines makes
about 700 runs and two merge passes at the default memory (D10).

### D14. Temporary files, free space and the output file

- **The temporary folder** is created inside the directory that `--temp` names, by default the directory of the
  output, under a new name: the output's file name, `.sort-` and a random suffix (`Path.GetRandomFileName`). The
  engine deletes it, with everything in it, on every way out: success, invalid input, failure, cancellation. In
  that directory it deletes nothing else, apart from `<output>.tmp` and an old output being replaced, when it is the
  output's directory.
  A process that is killed leaves the folder behind, which the README says. An input that fits in one chunk needs no
  folder and gets none.
- **Why not the system's temporary directory**: it is often on a small system disk, and a sort of 100 GB needs
  about 100 GB of temporary space.
- **Free space** is checked before the input is read, on the volume of the temporary folder and on the volume of
  the output: the runs need about the size of the input, and so does the output; on one volume the two add up. That
  is the peak of a sort in one pass, reached during the final merge, when all runs and the output exist at once. The
  check asks for room for runs even when the input will turn out to fit in one chunk, which is known only after
  reading; for such small inputs the room is small. An output that is about to be replaced counts as free space
  for the output, since it is deleted before the output is written; a stale `<output>.tmp` counts as free space
  too. Whether a sort needs several passes depends on the length of its lines, unknown before reading; so before
  each group of D10 the space is checked again for the run that the group writes, and a sort that would not fit
  ends with exit code 1 and the same message. A volume whose free space cannot be asked (some network paths) is not
  checked, and a full disk then ends the sort with exit code 1.
- **The output** is written to `<output>.tmp` next to it and renamed when complete, as the generator does. That file
  is created, empty, before the input is read, so an output path that cannot be written fails at once and not after
  the runs of a large input are made. An
  existing output is replaced only with `--overwrite` or a `y` or `yes` at the prompt, which is the generator's
  question, moved to `FileSort.Cli`. It is asked before anything is read. The old file is deleted right before the
  output is written, that is at the start of the final merge or of the write of a single chunk, so a sort that fails
  earlier, for example on an invalid line, leaves it untouched.
- **The same file twice**: the full paths of `--input` and `--output` are compared as the file system of the
  platform compares names, as the checker does; a second name for one file, such as a link, is not looked for. A
  sort in place, which `sort -o F F` of GNU allows, is refused because the old output is deleted before the new one
  is written: with the input as the old output, a failure or a Ctrl+C during the final merge would lose the input.
  The README says why.

### D15. Command line, settings, progress and summary

| Option | Accepted values | Default |
|---|---|---|
| `--input`, `-i` | the path of the input file | required |
| `--output`, `-o` | the path of the output file | required |
| `--temp` | a directory | the directory of the output |
| `--memory` | a size such as `512MB` or `1.5GB`, at least 256 MB | 1 GB |
| `--threads` | a whole number from 1 to 64 | the number of logical processors, at most 64 |
| `--line-ending` | `auto`, `lf` or `crlf` | `auto` |
| `--overwrite` | a flag | off |

- **Settings** go to standard error before anything is read: both paths in full, the temporary directory, the
  memory, the threads, the line ending option and whether `--overwrite` was given.
- **Progress** is drawn with Spectre.Console on standard error when it is a console: one bar for the chunks, over the
  bytes of the input read, and one for the merge, over the bytes written, with the pass when there are several, the
  throughput and the time left. The host reads the counters of the engine (D17) every 100 ms. When standard error is
  redirected, Spectre.Console is not used at all and nothing about progress is written, as in the other programs.
  M8 measures what the drawing costs while every core sorts.
- **Why Spectre.Console**, the author's decision: a sort of a large file runs for minutes, in phases and passes, and
  one rewritten line of text, the `ProgressLine` of `FileSort.Cli` that the generator and the checker use, shows
  that poorly. RazorConsole, which the author looked at first, was dropped before any prototype: the sorter takes
  no input from the keyboard while it sorts, so a framework of components on a hosting stack buys nothing over
  Spectre.Console's
  own progress display. The alternative without a package is `ProgressLine` with the phase in its text. `CLAUDE.md`
  keeps third-party packages in production code to a minimum, and this one is contained: one package with one
  dependency of its own (Spectre.Console.Ansi), referenced by `FileSort.Sorter` only, never seen by the engine, the
  generator or the checker. The tests run the program with a console of their own whose streams count as
  redirected, so they never start the drawing; the class that draws is small, and the README names it if its
  coverage falls below the rule.
- **The summary** goes to standard output as `Name: value` lines, the style of the other programs, which scripts
  can read: output path, lines, bytes, the line ending used, whether the input had a byte order mark, runs, merge
  passes, the time of the chunks and of the merge, elapsed time, throughput and peak working set. Merge passes
  count every merge the sort ran: each group of D10 and the final merge, one each. An input that fit in one chunk
  shows 0 runs and 0 merge passes.
- **README**: the sorter's README lists the options in a table typed by hand, and a test compares the option names
  and the fixed defaults with the command, with the helper the generator and the checker already use.

### D16. Errors, exit codes and cancellation

| Situation | Exit code | Where the message goes |
|---|---|---|
| The output is written | 0 | the summary on standard output |
| A line of the input is not valid | 3 | standard error: the input file, line number, offset, reason, line ending |
| Invalid arguments, the same file as input and output among them | 2 | standard error, naming the option |
| Cancelled | 130 | standard error |
| Anything else: a file that cannot be read or written, a temporary directory that does not exist, a line too long, too little space, a declined overwrite | 1 | standard error |

- The engine reports an invalid line and a line that is too long as exceptions of its own with the line number, the
  offset and the reason; the host turns them into messages and exit codes. Errors of the file system are named with
  their path. Anything unexpected is caught by `ProgramHost`, which already ends every program with a message and
  no stack trace.
- **Cancellation** comes from `FileSort.Cli` as a token. The engine checks it between pieces of reading, between
  segments of a sort (D7), between blocks and between refills of the merge, so it reacts well within a second,
  then deletes the temporary folder and
  `<output>.tmp` and lets the host report it. The spec allows 2 seconds.
- A console that cannot be written to is treated as the generator treats it: before the output is complete it stops
  the sort with exit code 1; after the output is in place the output stays, and the exit code is 0.

### D17. The engine's interface

- **Settings in, result out**: the input and output paths, the temporary directory, the bytes of memory the engine
  may use and the size of the window of D8, the threads, the line ending (`auto`, `lf` or `crlf`), the line limit,
  whether an existing output may be replaced (the host has asked), and where free space is asked (the volumes, or a
  function the tests give). `Sort` takes a cancellation token and returns a result: lines, bytes, the line ending
  used, whether the input had a byte order mark, runs, merge passes and the time of each phase.
- **Progress as counters**: the phase, the input bytes read, the runs written, the pass and the output bytes
  written. They are updated once per piece or block, never per line, and the host reads them from its own thread.
- **The run storage** is a small interface (create a run, open it, delete it, and create the output and make it
  final) with a file implementation: runs in the temporary folder, the output as `<output>.tmp` renamed at the end.
  Tests and the experiments of `FileSort.Benchmarks` use a version in memory, which lives with them and not in the
  engine: thousands of runs and several passes without a disk, and measurements of the processor alone, where the
  input is read from a file and nothing is written. This is the same kind of seam as the block sink of the
  generator.
- **Writes and reads** go through the file cache of the operating system: a run and the output are written in whole
  block buffers, and the merge reads with positional reads into its buffers. Unbuffered I/O is the second change's.

### D18. The generator: new edge cases and content version 2

- **`big-numbers`** also writes 18446744073709551616, the first Number above 64 bits, and Numbers of 21 to 30
  digits. They are formatted from `UInt128`, which holds 39 digits and writes itself as UTF-8 like the other integer
  types.
- **`leading-zeros`** keeps adding 1 to 3 zeros; together with `big-numbers` a Number can now have up to 33 digits,
  which the spec allows. The generator caps its zeros by `LineFormat.MaxNumberDigits` today; it gets a limit of its
  own for the longest Number it writes, and its longest line grows by those digits and by the carriage return of the
  new category. `LineFormat.MaxNumberDigits` stays the count of digits of the 64-bit range, which the fast path of D6
  uses, and its comment stops saying that the generator writes no longer Number.
- **`carriage-return`**, a new category, appends a carriage return to the String of a line, with the probability of
  the other categories. It is applied when the line is written, also to a String of the pool, and never to the first
  line of the file, so that `auto` reads every generated file right. In a file with `\r\n` the line ends with
  `\r\r\n`, which `line-format` reads as a String with a carriage return at its end. Applied to an empty String it
  makes the String a single carriage return.
- **Content version 2.** Files made with `big-numbers` change for certain, and files made with `carriage-return`
  did not exist; whether files with only other categories keep their bytes depends on where the bits of the new
  category go in the roll of edge cases, which the code decides. A file made without edge cases draws no roll, and
  its bytes stay the same: the reference hash of the plain reference file is recorded again for version 2 with the
  same value, and a new hash is recorded for the file with edge cases. Hashes recorded for version 1 are never
  edited.
- `data/check-edge-1gb.txt`, made with version 1, stays a valid input, but the same command now makes a different
  file. The measurements of this change use it as it is, plus a small file of version 2 for the new categories.

### D19. Tests

- **Lines**: the engine's parser against the naive parser of the tests on generated content with every edge case,
  and the scenarios of `line-format`; the packing of the entry against worked examples, Numbers of 1022, 1023 and
  1024 digits among them (D6); the comparison against the
  naive comparison, with the tie-break of D9, on generated lines and on lines with Numbers above 64 bits, Numbers of
  zeros only, carriage returns and zero bytes in Strings; every scenario of `sort-order`.
- **Chunks**: tiny chunk buffers and thread counts that put a chunk boundary and a range boundary at every position
  of a small input; the byte order mark, both line endings, a last line without a terminator, the line limit, and an
  invalid line whose number and offset must be right across both boundaries; an invalid line before a line that is
  too long, and the other way round, in one chunk and in two, which must give the report of D5 for every buffer
  size. The thread counts are 1, 2, 7 and 16 wherever a test varies them.
- **Segments**: the bound on a segment of D7; a sort with one thread and a large chunk, cancelled while it sorts,
  which must end within the time of "Cancellation".
- **Blocks**: inputs where one line repeats thousands of times, where long lines sort next to each other, where a
  large block comes right after a window full of small ones, and where a chunk has one segment; the run must equal
  the naive sort, and the bytes in flight must never pass the window of D13.
- **Merge**: many runs with tiny buffers; exactly as many runs as one pass holds, and one more; several passes.
- **The whole sort in memory**: generated inputs with every edge case, sorted with 1, 2, 7 and 16 threads and with
  chunk buffers from tiny to large, must give the same bytes as the naive sort with the tie-break of D9. A few
  outputs are also checked by the checker, in-process, as a second oracle.
- **Files**: the temporary folder appears and disappears; files of the user next to it survive; the output appears
  only when complete; an existing output and the question; the free space check, with the free space given by the
  test.
- **The command line** in-process with the console of the tests: every scenario of `external-sorter`, and the
  options table of the README.
- **Mutations**: before the independent review, at least ten deliberate faults in the comparison, the splitting of
  blocks and the merge, one at a time (a prefix compared as signed, the byte tie-break reversed, a splitter range
  off by one, a run's last line dropped, and others); the tests must fail for each.
- **Disk**: tests write megabytes, not hundreds of megabytes; the in-memory run storage carries the big cases.
- **Coverage**: at least 80% of the lines of the engine and the sorter, and of every class; the entry point and the
  drawing of the progress, which need a real console, are named in the README if they fall below.

### D20. Continuous integration

- **`sorter.yml`**, of the same shape as the other two, calls `test-program.yml` with the test namespaces of the
  sorter, the engine, the naive oracle and `FileSort.Cli`, and with the assemblies `FileSort.Sorter`,
  `FileSort.Engine` and `FileSort.Cli`. It starts on changes of the sorter, the engine, `FileSort.Cli`,
  `FileSort.Common`, the generator and the checker (the tests make inputs with the one and check outputs with the
  other), the tests except the generator's and the checker's own test folders, and the build files.
- **`generator.yml` and `checker.yml`** stop listing `FileSort.Sorter`, which they listed only because the sorter
  had no workflow, do not list the engine, and exclude the sorter's and the engine's test folders, as the
  generator's workflow already excludes the checker's, so that a commit that touches only the sorter's tests does
  not start them.
- **`CLAUDE.md`** gets the sorter's commands, in the same change, because CI runs what the Commands section runs,
  and its Workflow paragraph names `sorter.yml`.

### D21. Measurements and what they write

The planning measurements P1 to P5 wrote nothing. The measurements of the implementation, with their volume of disk
writes: a sort writes about twice its input, the runs and the output.

| Id | What | How | Written |
|---|---|---|---|
| M1 | The parsing of lines (D5), the sort routine and the size of a segment of D7, and the entry of 24 bytes against 16 | BenchmarkDotNet, segments of default content in memory | 0 |
| M2 | The size of a block in D8: 256 KiB, 1 MiB, 4 MiB; 1 to 16 threads | Experiment, in memory | 0 |
| M3 | The phases with 1, 2, 4, 8 and 16 threads | Experiment, the whole sort in memory | 0 |
| M4 | A 100 MB file, once: one chunk, written straight to the output, so the sort of a small file | Published build, default options | 0.1 GB |
| M5 | The 1 GB file, three times: the scenario "A file of 1 GB" | Published build, default options | 6 GB |
| M6 | Memory: the reserve, and the scenarios of "Memory budget": the smallest `--memory` on the 1 GB file, 64 threads on two logical processors on the 1 GB file, and 300 lines of 1 MB with the smallest `--memory`, made once by the PowerShell recipe of the checker's README | Published build | 4.9 GB |
| M7 | The 1 GB file with every edge case of version 1, once, and 100 MB of version 2 | Published build | 2.2 GB |
| M8 | The cost of drawing the progress while every core sorts | Experiment, in memory | 0 |
| M9 | The 5 GB file: the scenario "A file of 5 GB" | Generator, then published build, then checker | 15 GB |

The check of Ctrl+C on the published sorter (task 9.4) cancels a sort of the 1 GB file during its merge and writes
about 1 GB more. Together about 14 GB, plus 15 GB for M9. During planning the author approved about 11 GB and the
run of a 5 GB file, whose 15 GB of writes were stated then; this plan is about 3 GB above the first figure, so the
total is stated to the author again before M4 starts. Disk measurements leave pauses between runs, as `CLAUDE.md`
says, and the README quotes only numbers that committed code reproduces.

Decision rules: M1 decides the sort routine and the size of a segment (D7) and confirms the entry; M2 decides the
size of a block; if the median of M5 is above 20 s, well above the estimate of D3, the author is told with the times
of the phases before anything is tuned; if M6 finds the reserve too small for any case of the spec, the reserve
grows and the README says why.

## Risks / Trade-offs

- [The final merge on one thread dominates at 100 GB: about 23 minutes of an estimated half hour] → the second
  change makes it parallel with the block merge of D8, applied to runs; this change measures 5 GB to see the trend.
- [Planning numbers come from throwaway prototypes] → they decided the architecture only; every number in the README
  comes from M1 to M9, which committed code reproduces.
- [Many equal keys put lines into one block] → the strict order of D8 splits them; a test with one line repeated
  thousands of times guards it.
- [Long lines that sort together make one block large] → such a block is written by its worker in pieces, through
  the piece buffer that the window sets aside; a test with lines of 1 MB guards it, and M6 measures its memory.
- [Short lines make many runs] → 100 GB of 5-byte lines takes two merge passes at the default memory; correct, and
  slower; the README says so.
- [The reserve of D13 is an estimate] → M6 measures the peak in the worst cases of the spec, and the reserve is one
  constant of the host.
- [Counts and offsets overflow 32 bits at 100 GB] → all counts of lines and bytes and all file offsets are 64-bit;
  M9 crosses 4 GB of offsets; the count of lines passes 2^31 only at about 50 GB, which the second change's 100 GB
  run checks.
- [A killed process leaves its temporary folder] → its name says what it is; the README tells where to look.
- [A full disk in the middle of a sort] → exit code 1, with the temporary folder and `<output>.tmp` deleted.
- [A hard disk reads hundreds of runs slowly] → not measured; the README names it as a limitation.
- [The new edge cases change the bytes of edge-case files] → content version 2, new reference hashes, and the plain
  content keeps its bytes, which the plain reference hash shows.
- [Spectre.Console on a redirected stream] → not used then; the settings and the summary are plain lines either way.
- [The sorter's comparison and the checker's agree on a wrong order] → they share no code, and both are tested
  against the naive comparison of the tests.

## Open Questions

None that change this design. The reserve of D13, the block size of D8, and the sort routine and the size of a
segment of D7 are numbers and choices that M1, M2 and M6 settle.
