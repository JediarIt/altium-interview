# Design

## Context

See `proposal.md` for the motivation and the scope. What shapes the design:

- `FileSort.Common`, `FileSort.Generator` and `FileSort.Tests` exist but are empty; there are no specs yet.
- The generator publishes as a self-contained single file and must stay trim- and AOT-safe (`IsAotCompatible` is on
  for `FileSort.Generator` and `FileSort.Common`). Warnings are errors.
- The reference machine is the only one available: Windows 11, 16 logical cores, 32 GB RAM, an NVMe SSD,
  .NET runtime 10.0.15. Nothing can be measured on Linux or macOS in this change.
- The numbers quoted below come from throwaway `Stopwatch` probes run during planning. They justify the direction of
  each decision. The measurements of D16 repeat the ones that performance depends on, and only numbers measured
  there are added to the README.
- The specs state the contract: what users and the other programs rely on. Details below that level (accepted
  ranges, the console protocol, the rules of the free-space check) are decided here and are written only here.
  Once the code exists, the code, its tests and `--help` are their single home; this document is archived with the
  change and is not kept in step with them. The one deliberate second copy is the table of options in the
  README (D13).

## Goals / Non-Goals

**Goals:**

- A byte-identical file for the same seed and content options, on any OS, thread count and runtime version.
- Throughput limited by the disk, not by the generator, on the reference machine.
- Memory that does not depend on the output size, with every buffer accounted for.
- Default output that any reasonable comparer orders the same way; unusual but valid lines only on request.
- A small set of single-purpose types that can be tested without a disk and benchmarked without a console.

**Non-Goals:**

- A line parser in `FileSort.Common` (the sorter change designs it together with the sorter).
- Malformed output, a user-supplied dictionary, direct I/O on Linux and macOS.
- Statistical quality of the random numbers beyond what test data needs. The PRNG is not used for security.

## Decisions

### D1. `FileSort.Common` gets constants and writing, not parsing

`FileSort.Common` receives the format constants (separator, terminators, the 20-digit and `UInt64` limits), the
`LineEnding` choice, and a line writer that puts a Number, the separator and a terminator into a `Span<byte>` as UTF-8.
The generator composes lines from these primitives, so the bytes of the format are defined in one place.

Parsing is left out on purpose. The sorter is its only consumer, it is the sorter's hottest code (about 4 billion
calls for 100 GB), and its shape depends on decisions not made yet: how a line split by a buffer boundary is handled,
and whether parsing returns a number and a position or a ready sort key. Tests in this change validate generated
lines with a separate naive parser written in the test project, which is given the line ending of the file (D2).
That is also the stronger check: a writer and a
parser that share one misunderstanding of the format would agree with each other.

*Alternative:* write the parser now for a round-trip test on shared code. Rejected: code without a consumer, likely
to be rewritten.

### D2. Format limits

- **Number: 1 to 20 digits, value up to `UInt64.MaxValue`.** A 64-bit value compares in one instruction. Twenty digits
  is the length of the maximum, so leading zeros are allowed inside a bounded field and a reader can reject a longer
  digit run without scanning it. *Alternative:* unlimited leading zeros; no practical producer needs them and the
  field becomes unbounded.
- **A file has one line ending, `\n` or `\r\n`, and its reader knows which.** An earlier draft accepted both endings
  line by line and took a carriage return right before a line feed as part of the terminator. Under that rule the
  bytes `Apple\r\n` are two things at once: the String `Apple\r` with the terminator `\n`, and the String `Apple`
  with the terminator `\r\n`. A sorter that reads `1. Apple\r\r\n` holds the String `Apple\r`; written out with
  `\n` it becomes `1. Apple\r\n`, which a checker reads back as `Apple`, so a byte of the input is missing from the
  output. With one line ending per file the same bytes have one meaning. In an `\n` file a reader searches for a
  single byte and every carriage return is String content; in an `\r\n` file it also looks at the byte before the
  line feed. A line that is copied keeps its bytes. The price: a file that mixes both endings is read literally.
  Read as an `\n` file, its `\r\n` lines have Strings that end with a carriage return; read as an `\r\n` file, a
  line feed without a carriage return does not end a line, so the lines on both sides of it become one invalid
  line. How the sorter and the checker learn the line ending of a file (an option and its default) is decided in
  their changes. The generator never writes a carriage return into a String. *Alternative:* keep both endings in one file and make a String that ends with a carriage return invalid;
  it adds a kind of invalid line only to protect a guess that one line ending per file makes unnecessary.
- **A UTF-8 byte order mark at the start of a file is allowed and is not part of the first line.** Such files are
  common on Windows: .NET writes the mark whenever a file is created with an explicit `Encoding.UTF8`
  (`new StreamWriter(path, false, Encoding.UTF8)`, `File.WriteAllLines(path, lines, Encoding.UTF8)`), and so does
  `Out-File -Encoding utf8` in Windows PowerShell 5.1. A test file produced by someone else's tool can therefore
  carry one, and without this rule its first line would begin with `EF BB BF` instead of a digit and be invalid.
  The check is three bytes once per file. The generator never writes the mark. *Alternative:* treat such a file as
  invalid; it fails a common input on its first line and gains nothing.
- **Leading zeros have a consequence for the sorter**: `007. Apple` and `7. Apple` have equal keys and different
  bytes. The README sentence "lines with equal keys are identical" becomes wrong and is corrected in this change; the
  order of such lines is decided in `add-external-sorter`.

### D3. Own PRNG: xoshiro256** seeded through SplitMix64

| Operation | ns |
|---|---|
| `System.Random(seed).Next()` | 4.43 |
| `System.Random(seed).NextInt64()` | 33.78 |
| xoshiro256**, 64 bits | 1.52 |
| SplitMix64, 64 bits | 1.88 |
| `new System.Random(seed)` plus the first call | 1698 |
| new xoshiro256** (seeded through SplitMix64) plus the first call | 16 |

A naive single-thread line generator without I/O produced 368 MiB/s with xoshiro256** and 160 MiB/s with
`System.Random`.

A seeded `System.Random` gave the same sequence on runtimes 8.0.34, 9.0.23, 10.0.15 and 11.0.0 (one fingerprint of
600,000 calls), so it would work today. It is still rejected: the documentation does not promise the algorithm
across major versions, it is 2.3 times slower end to end, and creating one per block costs 1.7 µs against 16 ns. The
own generator is about 20 lines and its output is pinned by known-answer tests.

Each block and the repeat pool get their own stream. The xoshiro256** state is four consecutive SplitMix64 outputs
taken from the initial state `SplitMix64(seed) XOR stream`, where `stream` is the block index, or 2^64 − 1 for the
pool.

Known-answer values for the tests, computed during planning and matching the published reference values:

- xoshiro256** from the raw state {1, 2, 3, 4}: 11520, 0, 1509978240, 1215971899390074240.
- SplitMix64 from the seed 1234567: 6457827717110365317, 3203168211198807973, 9817491932198370423,
  4593380528125082431, 16408922859458223821.

*Alternatives:* SplitMix64 alone (simpler, 64-bit state, slightly slower here); PCG (no advantage for this use).

### D4. The file is a sequence of independent blocks

A block is a buffer of fixed capacity (1 MiB, see M2) filled with whole lines until the next line might not fit. Its
content is a pure function of the seed, the content options and the block index. The file is the concatenation of
blocks 0, 1, 2 and so on, cut at the first line boundary at or after the requested size.

Consequences:

- Any thread can compute any block, and the thread count only decides who computes it. Splitting the file "by
  thread" would make 4 and 16 threads produce different files.
- A smaller file is a prefix of a larger one for the same seed and options (a spec scenario and a cheap test).
- The exact number of blocks is not known in advance, because blocks end on line boundaries and have slightly
  different lengths. The writer decides when the size is reached.

Rules that keep the bytes identical everywhere:

1. Integer arithmetic only. No floating point, no `Math.Pow` or `Math.Log`, no `System.Random`, no
   `string.GetHashCode` or `HashCode` (randomized per process).
2. No culture and no `Environment.NewLine`; numbers are formatted by culture-invariant UTF-8 code.
3. Bytes are written one field at a time; integers are never reinterpreted as bytes, so byte order of the CPU does
   not matter.
4. Everything the bytes depend on is frozen under the **content version**: block capacity, dictionary, pool size,
   edge case rates, the layout of random bits. The version starts at 1, is printed in the summary and is raised by
   any change that alters the output for an existing seed.
5. Guard: the known-answer tests of D3 and the SHA-256 of two reference files, recorded in the tests per content
   version: one with default options, one with every content option changed (`--edge-cases all`,
   `--line-ending crlf`, `--words 2-7`, `--duplicates 50`). A change of the output fails these tests; raising the
   version and recording the new hashes makes them pass. A test cannot tell an edited hash from a new version, so
   one part stays a review rule: a hash recorded for an existing version is never edited.

*Alternative:* blocks of an exact byte size, obtained by fitting the last line of each block. Offsets would be known
in advance and writes could skip the copy of D11. Rejected: the fitting logic has to respect `--words` and every edge
case, and the copy it saves is cheap (M4 measures it).

### D5. Strings from a built-in dictionary of 1024 words

Five million Strings of 1 to 5 words were generated with each model, sorted, and each String compared with its
neighbour. "Real text" was 11 MB of documentation, 1.5 million words.

| Model | Lines whose String repeats | Neighbours share the first 8 bytes | Mean common prefix, bytes |
|---|---|---|---|
| Random letters | 4.7% | 3.8% | 4.1 |
| Dictionary of 1024 words, uniform | 32.3% | 92.6% | 12.0 |
| Dictionary of 1024 words, frequent words more often | 49.9% | 93.9% | 11.4 |
| Dictionary of 8192 words, frequent words more often | 38.3% | 87.7% | 10.4 |
| Windows of real text | 77.3% | 95.5% | 14.2 |

Random letters are rejected: comparisons end within the first 8 bytes, which flatters any sorter that compares a
short prefix. Real text is rejected: it has few distinct fragments, so repeats reach 77% at 5 million lines and
approach 100% at 1 GB, `--duplicates` stops controlling anything, and it needs a multi-megabyte asset. The uniform
dictionary gives nearly the same comparison difficulty as real text and keeps repeats under control.

The dictionary has exactly 1024 distinct lower-case words of the letters a to z, at most 12 letters each, so a word
index is 10 random bits with no modulo. It is written for this project (no third-party word list, so no license
question) and includes the words of the task's examples. Words with apostrophes or hyphens are excluded because
culture-aware comparers treat them specially, which would break the promise of D8 that a default file sorts the same
way under every comparer. It is stored as static UTF-8 data.

A String is `min..max` words (`--words`, default 1 to 5) joined by single spaces, the first word capitalized. The
length is set in words, not bytes: a byte limit would cut words, and would cut non-ASCII characters in half.

### D6. Numbers: uniform bit length, then a uniform value of that length

A Number is drawn in two steps: a bit length from 1 to 32 with equal probability, then a uniform value with that
many bits (a shift of a 64-bit random value). Exact shares of lines by decimal length:

| Digits | Uniform over 0..2^32 − 1 | Uniform bit length |
|---|---|---|
| 1 | 0.0000002% | 13.3% |
| 2 to 8 | 2.3% in total | 10.2% to 10.6% each |
| 9 | 21.0% | 9.9% |
| 10 | 76.7% | 4.3% |

Two independent Numbers are equal with probability 2.3 × 10^-10 in the first model and 0.29% in the second. Fully
identical lines, counted in a simulation with Strings of 1 to 5 dictionary words and no repeat pool:

| Lines | Uniform value | Uniform bit length |
|---|---|---|
| 400,000 (about 10 MB) | 0 | 8,915 |
| 40,000,000 (about 1 GB) | 18 | 2,733,620 |

A sorter that drops identical lines is a classic bug, and with uniform values the small files used in tests would
never contain any. The second model also looks like the task's example (`1.`, `415.`, `30432.`). It needs no division
and no floating point. The average Number has 5.1 digits instead of 9.7.

### D7. Repeated Strings come from a pool derived from the seed

The pool holds 1024 Strings composed by the same rules as fresh Strings (same `--words` range, same edge cases),
from the pool's own PRNG stream. For each line, `--duplicates` percent of the time the String is a pool entry chosen
uniformly; the decision is an integer comparison on random bits.

At 1 GB and 30% each pool String appears about 12,000 times, at 100 GB about 1.2 million times, so the sorter meets
long groups of equal Strings ordered only by Number. Fresh Strings also coincide by chance (all one-word Strings and,
in large files, two-word Strings), so the option sets the share of pool lines, not the share of repeated Strings.
With default options about 45% of the lines of a 10 MB file repeat, by calculation: 30% from the pool, plus the
one-word Strings, which are a fifth of the rest and always repeat. The spec therefore tests the option with
`--words 5`, where fresh Strings do not coincide. `--duplicates 100` leaves at most 1024 distinct Strings;
`--duplicates 0` turns the pool off.

*Alternative:* a pool whose size grows with the file. Rejected: the content would depend on the size and the prefix
property of D4 would be lost.

### D8. Plain by default, edge cases on request

Default lines are sentence-case ASCII words separated by single spaces. Position 0 of every String is an upper-case
letter and every later position is a lower-case letter or a space, so an upper-case letter never meets a lower-case
one in a comparison. Measured: 300,000 such Strings sort into the same order by UTF-8 bytes, by UTF-16 code units and
by the invariant, en-US, ru-RU and de-DE cultures. A default file therefore sorts the same way under any reasonable
reading of the task, which makes results comparable with other tools. The same probe showed that this breaks as soon
as case varies (`apple` against `Banana`) or non-ASCII appears (U+FF21 against U+1F600 orders differently by UTF-8
bytes and by UTF-16 code units). Those cases are exactly what `--edge-cases` adds.

Each enabled category applies to a line with probability 1/16, independently of the others, decided by bits of one
extra random value that is drawn only when at least one category is enabled (the default path pays nothing).

| Category | What an affected line gets |
|---|---|
| `unicode` | Each word is replaced with probability 1/2, and at least one word always, by a word from a second list of about 32 words without ASCII characters: Cyrillic and Greek (2 bytes), CJK and fullwidth forms including U+FF21 (3 bytes), emoji including U+1F600 (4 bytes), and one word in two spellings, with U+0439 precomposed and decomposed (U+0438 U+0306) |
| `separator` | One of three forms: `. ` in place of the space between two words, a String that begins with digits and `. `, a String that begins with a space. A one-word String takes one of the last two |
| `case` | Each word in lower case, capitalized or upper case, chosen at random |
| `big-numbers` | A bit length from 33 to 64; one line in eight of these takes a boundary value instead: 4294967296, 9223372036854775807, 9223372036854775808 or 18446744073709551615 |
| `leading-zeros` | 1 to 3 zeros in front of the Number, never more than 20 digits in total |
| `empty` | An empty String |

Categories that change the String apply when a String is composed, so pool entries carry them too and repeated
unusual Strings exist. Categories that change the Number apply per line.

No category changes the number of words except `empty`, so `--words` keeps its meaning with edge cases. The `. `
that `separator` puts in place of a space still stands between two words, and the prefix of digits with `. ` and the
leading space are added in front of the words and are not words themselves. When `empty` applies to a String, the
other String categories do not. The frequencies in the spec hold for the default `--words` and `--duplicates`: with
`--words 1024` one megabyte has about 150 lines, and with `--duplicates 100` the String categories are decided once
per pool entry, not once per line.

### D9. One hot path, no allocations per line

Lines are written straight into the block buffer: digits of the Number, the separator, the bytes of each word, the
terminator. There is no `string`, no intermediate buffer for the String and no heap allocation per line; a pool
String is one copy of stored bytes. The naive version of this loop measured 72 ns per line (368 MiB/s per core).

The unbuffered disk takes about 2,500 MiB/s (D11), which needs roughly 7 cores at the naive speed. That is within
reach on 16 logical cores but leaves little margin, so two choices are decided by benchmark (M1) instead of by
guess: formatting the Number with `Utf8Formatter` or with a two-digits-at-a-time table, and copying a word by its
exact length or as a fixed 16-byte slot.

### D10. A bounded window of block tasks, consumed in order

The coordinator keeps at most `threads` block tasks in flight. It starts tasks for the next block indices, waits for
the oldest one, hands its block to the sink, recycles the buffer and starts the next task. Order comes from the order
of waiting, parallelism from the tasks, and the bound on memory from the size of the window. When the sink is slower
than generation, finished blocks wait and no new work starts, which is the backpressure.

The sink is an interface. The program has one implementation, the file sink (D11). The other two are test doubles and
live with their users, outside the program: an in-memory hashing sink in the tests (they check determinism without a
disk), and a discarding sink in the benchmarks (they measure generation alone) and in the pipeline tests.

Block buffers are allocated once, `threads + 1` of them, and recycled. One `Task` per block (per megabyte) is the only
allocation in steady state. Tasks run on the thread pool, whose minimum size is the number of logical processors, so
the default thread count starts at full parallelism. `--threads` is therefore an upper limit on parallelism, not a
number of dedicated threads: a value above the number of logical processors adds block buffers and no speed.

*Alternatives:* TPL Dataflow with ordered output (the same behavior, more machinery); a thread per worker with a
reorder buffer (more code for the same result); PLINQ `AsOrdered` (partitions the range in advance, so it does not
stream).

### D11. One writer, unbuffered on Windows, large aligned chunks

Raw sequential writes of an 8 GiB preallocated file to the target disk, final flush included:

| Write mode | Writers | Chunk | MiB/s |
|---|---|---|---|
| Buffered | 1 | 1 MiB | 1171 to 1325 |
| Buffered | 1 | 8 MiB | 867 |
| Buffered | 4 | 1 MiB | 1325 |
| Unbuffered | 1 | 1 MiB | 1799 |
| Unbuffered | 1 | 8 MiB | 2493 |
| Unbuffered, no preallocation | 1 | 8 MiB | 2432 |
| Unbuffered | 4 | 8 MiB | 2006 |
| Unbuffered | 8 | 8 MiB | 2224 |

Decisions that follow:

- **Unbuffered writes** (`FILE_FLAG_NO_BUFFERING`): twice the buffered throughput. A side effect that helps later
  measurements: the new file does not fill the OS file cache, so the sorter that reads it next starts from a cold
  cache and from free RAM.
- **One writer**: several writers were slower than one in the unbuffered mode and no faster in the buffered mode.
- **8 MiB chunks** in the unbuffered mode: faster than 1 MiB there. The buffered mode was faster with 1 MiB, so the
  chunk size is a constant per write mode, and M3 sweeps it for both.
- **Preallocation** made no difference beyond the spread between runs (2493 against 2432 MiB/s). It is kept for
  another reason: on a volume that is too small, the failure comes before any content is written.

Mechanics: the file sink copies blocks into a chunk buffer aligned to 4096 bytes (allocated with
`NativeMemory.AlignedAlloc`, the only unsafe code of the change, kept in one small type) and writes full chunks at
aligned offsets. The last chunk is padded to a multiple of 4096, written, and the file is cut to its exact length.
The buffered mode is the same code without the flag and with its own chunk size, so there is one writer to test.
The flag is passed only on Windows; other systems always use the buffered mode.

`--buffered-io` exists for two reasons: a file system that rejects unbuffered writes (some network and virtual
drives, or a sector size above 4096), and the end-to-end comparison of the two modes recorded in the README.

The copy into the chunk buffer runs on the writer thread, between writes. If M4 shows that it costs more than 10% of
the raw write throughput, the write moves to a second thread with two alternating chunk buffers, inside the file
sink. Asynchronous writes would not help: NTFS completes writes that extend a file synchronously, and preallocation
reserves the space without moving the end of the file, so every write still extends it.

### D12. The output file is complete or absent

- Content goes to `<output>.tmp` in the output's directory and is renamed at the end, so the rename stays on one
  volume. The name matches `*.tmp` in `.gitignore`. A stale temporary file, left by a run that was killed, is
  overwritten without asking. Missing parent directories of the output are created.
- If the output exists, consent is required: `--overwrite`, or `y` or `yes` in any letter case at a prompt shown only
  when neither standard input nor standard error is redirected. The prompt goes to standard error and ends with
  `[y/N]`: every other answer, an empty one included, declines. After consent the old file is deleted first:
  keeping it until the rename would need room for two 100 GB files. The price is that an interrupted run leaves no
  file, which is acceptable for a file that can be regenerated from its seed.
- Free space is checked before anything is deleted, where the volume can be queried. The required amount has three
  parts: the requested size; one maximum line, because the output can exceed the requested size by up to one line;
  and the padding of that sum to a multiple of 4096 bytes, because the last chunk is written padded (D11). The
  amount is capped at 2^63 − 1 bytes, the largest size that `--size` accepts, instead of overflowing. The sizes of
  the files that are about to be replaced, the old output and a stale temporary file, count as available. A failed
  check is reported as "not enough free space" with the required and available amounts and leaves both files
  untouched.
- Then the temporary file is preallocated to the same amount. This is the only check for a volume that cannot be
  queried (a UNC path, which `DriveInfo` does not support): a full volume fails here, before any content is
  written, but after the old file was deleted. Losing the old file in that corner is accepted, because it can be
  regenerated from its seed.
- Cancellation and errors go through one path that closes and deletes the temporary file. Ctrl+C and the
  termination signal (SIGTERM) both cancel one token, which is checked per block and per chunk, so the reaction time
  is a few milliseconds.

### D13. Command line on System.CommandLine 2.0.15

The spec gives the meaning and the default of each option. The accepted values:

| Option | Accepted values | Affects the bytes |
|---|---|---|
| `--output`, `-o` | a file path | no |
| `--size`, `-s` | a number with an optional unit, described below | yes |
| `--seed` | a whole number from 0 to 18446744073709551615 | yes |
| `--duplicates` | a whole number from 0 to 100 | yes |
| `--words` | `n` or `min-max`, with 1 ≤ min ≤ max ≤ 1024 | yes |
| `--edge-cases` | category names separated by commas, or `all` | yes |
| `--line-ending` | `lf` or `crlf` | yes |
| `--threads` | a whole number from 1 to 256 | no |
| `--overwrite` | a flag | no |
| `--buffered-io` | a flag | no |

`System.CommandLine` is Microsoft's parser, the one behind the `dotnet` CLI. It gives typed options, validation,
`--help` and `--version`. Without it each of the three programs would carry 100 to 150 lines of hand-written parsing
and help text, and a shared hand-written parser has no home, because the Checker must not reference
`FileSort.Common`. Version 2.0.15 is the latest patch of the line released with .NET 10 (2.0.0 on 2025-11-11) and
targets `net8.0` with no dependencies. Version 3.0.0 also runs on `net10.0`, but it was published on 2026-11-10,
eight days before this design; a year of patches outweighs a new major version here, and the upgrade can be
evaluated later. The package is the first NuGet dependency of production code. The regular publish does not run the
trimmer, so it cannot show a trimming problem inside the package; a second publish with `PublishTrimmed`, used only
as a check, makes the trimmer analyse the whole application (a task runs it). Unlike NativeAOT it needs no C++
toolchain.

Details that tests pin down: parse errors exit with code 2 (the library's default is 1); an error about a value
states the accepted form or lists the accepted names; `--help` shows the default of every option that has one; the
confirmation prompt and the progress line use a small console abstraction so that tests can drive them; the summary
and the reproducing command line are formatted with the invariant culture. The progress line shows the percentage,
the throughput and the estimated time left. The reproducing command line carries the output path, the size, the seed
and every option that affects the bytes, each with its value (`--edge-cases` only when a category is enabled);
`--threads`, `--buffered-io` and `--overwrite` are left out. Run as printed, it meets the file it just wrote and
asks before replacing it, like any other run. The summary reports the process's own peak working set, so every run
is also a memory measurement.

The README lists the options in a table typed by hand: each option with what it does, the values it accepts and its
default. A reviewer who reads the repository without building it then sees what the generator can do, and a table
reads better than help text. The table is a second copy of what `--help` prints. A test guards the part of it that
goes stale without anyone noticing: it reads the table from `README.md` and fails when the option names in it differ
from the options the command defines, or when a default that is a fixed value (such as `lf`) differs from the
default in the command. `README.md` is copied next to the test binaries as a linked file, so the test does not
search for the repository root. The rest of the table is prose that the test does not read: what an option does,
the values it accepts, and a default that is not a fixed value (a random seed, the number of logical processors).
For that part, a change that touches an option updates the table in the same change, and the task that writes the
table compares it with the output of `--help`. The price is a test that parses a Markdown table and has to change
when the layout of the table changes. *Alternative:* the output of `--help` word for word in the README, with a
test that compares the two; every word of it is checked, but it reads worse than a table. *Alternative:* examples
only and a pointer to `--help`; the reviewer never sees the options.

`--size` accepts a fraction (`1.5GB`), because the alternative is asking the user to work out `1536MB`. The number is
parsed as a `System.Decimal` with the invariant culture, multiplied by the unit and rounded up, so `0.1KB` is 103
bytes on every platform and no `double` is involved. Rounding goes up to keep the promise "at least the requested
size". A decimal comma is rejected: it means a fraction in some cultures and a thousands separator in others. An
exponent (`1e3MB`) is rejected as well: the number is digits with at most one period. A size above 2^63 − 1 bytes,
the largest file offset, is an invalid argument.

### D14. Memory accounting

| Item | Size at 16 threads | Scales with |
|---|---|---|
| Block buffers, `threads + 1` × 1 MiB | 17 MiB | thread count |
| Chunk buffers, 1 or 2 × 8 MiB | 8 to 16 MiB | constant |
| Repeat pool, 1024 Strings | under 0.1 MiB by default, up to 17 MiB for `--words 1024` | `--words` maximum |
| Dictionary and non-ASCII word list | about 10 KB | constant |
| Runtime, JIT code, thread stacks | to be measured, expected 30 to 60 MB | thread count |

Nothing scales with the output size. The expected peak working set is about 100 MB at 16 threads and about 150 MB at
64 threads, against the spec's limit of 512 MB; M5 measures it at 1, 10 and 100 GB and with 64 threads. For
comparison, the whole generator uses about 5% of the 2 GB budget that the sorter gets. The GC mode stays at the
default (workstation, concurrent): with no allocation per line there is nothing for the GC to do. M1 confirms it for
the block generator with zero allocated bytes per block; the pipeline adds one `Task` per block (D10).

### D15. Project layout

- `FileSort.Common`: format constants, `LineEnding`, line writer.
- `FileSort.Generator`: PRNG; dictionary; content options (the recipe); repeat pool; block generator; pipeline and
  the sink interface; file sink with the aligned buffer and the temporary file lifecycle; value parsers for sizes and ranges;
  the command definition; progress and summary; `Program`. Types are internal and visible to the tests and the
  benchmarks through `InternalsVisibleTo`. No separate library: `CLAUDE.md` asks for an engine library for the
  sorter only, and nothing else consumes the generator's code.
- `FileSort.Tests`: unit and integration tests, plus the naive line parser used as the oracle for validity.
- `FileSort.Benchmarks` (new, in `FileSort.slnx`): BenchmarkDotNet benchmarks and the `write-path` experiment.
  `BenchmarkDotNet` 0.15.8 is referenced here only.

### D16. Measurements

Every number that this change adds to the README comes from these, run on the reference machine. Figures of this
document that none of them reproduces stay here and are not quoted in the README: the dictionary comparison of D5,
the simulation of D6, the ordering probe of D8 and the `System.Random` fingerprint of D3.

| Id | Measurement | Decides or confirms |
|---|---|---|
| M1 | BenchmarkDotNet: PRNG against `System.Random`; Number formatting variants; word copy variants; block generation for default options, `--words 1`, `--edge-cases all` (MiB/s per core, allocated bytes) | Hot-path variants of D9; zero allocations |
| M2 | End to end at 10 GB with block capacity 256 KiB, 1 MiB and 4 MiB | The block capacity, frozen before the reference hashes are recorded |
| M3 | `write-path`: buffered and unbuffered, chunks of 1, 4, 8 and 16 MiB, 1, 4 and 8 writers, at 8 GiB and at 100 GB | Chunk size; one writer; the README table. The 100 GB run matters because 8 GiB fits inside the SSD's fast cache |
| M4 | File sink fed with ready blocks against the raw write of M3 | Whether the write moves to a second thread (threshold: below 90% of raw) |
| M5 | End to end: wall time, MiB/s, peak working set. Default mode at 1, 10 and 100 GB, `--buffered-io` at 10 and 100 GB, one 10 GB run with `--threads 64` | Throughput and memory requirements; the README table |
| M6 | At 10 GB with 1, 2, 4, 8 and 16 threads: generation into the discarding sink, and end to end | Thread scaling; where the disk becomes the limit |

## Risks / Trade-offs

- [The 8 GiB probes fit inside the SSD's fast cache, so 100 GB will be slower than 2,500 MiB/s] → M3 and M5 run at
  100 GB. The spec's limit of 5 minutes needs only about 340 MiB/s.
- [A file system rejects unbuffered writes, or its sector size is above 4096] → `--buffered-io`, described in
  `--help` and in the README.
- [Unbuffered writes and all measurements are Windows-only] → stated in the README under limitations; other systems
  run the buffered mode of the same code.
- [Byte-identical output on other operating systems and CPUs cannot be verified here] → the rules of D4 remove every
  platform-dependent operation, and the reference-hash tests, run on another platform, fail if it produces different
  bytes.
- [`System.CommandLine` is a dependency of a tool that must stay AOT- and trim-safe] → the check publish with
  trimming covers the trim half. AOT warnings inside the package would need the AOT compiler, which this change
  does not run, so that half rests on the package's own claim. The option set is small enough to fall back to
  hand-written parsing if a blocker appears.
- [BenchmarkDotNet builds a generated project under the repository's strict settings and an SDK 11-only machine] →
  if the default toolchain fails, the benchmarks use the in-process toolchain.
- [`--duplicates` sets the share of pool lines, not of repeated Strings: short Strings repeat by chance, even with
  `--duplicates 0`] → stated in the spec, in `--help` and in the README; `--words 4-5` gives mostly unique Strings.
- [Replacing an existing file deletes it before the new one is complete] → requires explicit consent; the file can
  be regenerated from its seed.
- [Leading zeros make lines with equal keys distinguishable] → the README is corrected here; the sorter change
  defines their order.
- [A real Ctrl+C cannot be sent reliably from an automated test on Windows] → the cancellation path is tested
  in-process through the token, and the key press is checked once by hand.

## Open Questions

None for this change. D2 leaves one decision to the changes of the checker and the sorter: how each of them learns
the line ending of a file it reads.

## Changes during implementation

The decisions above are the plan as it was reviewed, and they stay as written. The implementation departed from
them where a measurement or a review gave a reason; this section lists every departure. Where this document and
the code differ, the code, its tests and the READMEs are right. The measurements are in
`FileSort.Generator/README.md`; each item ends with the subsection of its section Performance, or the section, that
holds them.

- **The README was split.** The root `README.md` is the overview. The generator's options table, behaviour,
  measurements, design decisions and limitations are in `FileSort.Generator/README.md`. Where D13, D16 or
  `tasks.md` place those parts in "the README", they are in that file now, and the test of D13 reads the options
  table from it (linked into the test output as `FileSort.Generator.README.md`).
- **D1, D15: no line writer in `FileSort.Common`.** Common holds `LineFormat` (the separator, the line feed and
  carriage return bytes, the two limits of a Number and the byte order mark), `LineEnding` and
  `LineEndingExtensions`. `LineWriter` is internal to the generator (`Content/Lines`) and has no method that
  writes a whole line: the generator is the only program that composes lines, and the sorter copies them as bytes.
  The requirement "Canonical form of a written line" is about bytes and is unaffected. If the sorter ever needs to
  compose a line, the writer moves back.
- **D9, M1: Numbers are written without a digit loop.** Neither planned variant was kept for plain Numbers (at most
  32 bits, no leading zeros). They are written by `PlainNumberWriter`, which computes every digit at once: 8.59 ns
  against 15.93 ns for `Utf8Formatter`, because the length of a Number is random and a digit loop mispredicts on
  almost every line. The rare Numbers of `big-numbers` and `leading-zeros` go through `Utf8Formatter`; a
  hand-written digit loop measured 19.4 ns and was removed. Words are copied as fixed 16-byte slots (2.10 against
  6.76 ns), so the dictionary is expanded into a table of slots when it is first used. Bit operations sit behind
  small named methods (`RandomBits`, and the parts of `PlainNumberWriter`); the JIT listing shows them inlined. The
  capital letter of a String is set with one exclusive or, 2.25 ns against 5.30 ns for `Ascii.ToUpperInPlace`. The
  hot path; Design decisions.
- **D4, rule 3** holds in substance, not word for word: `PlainNumberWriter` packs digits into an integer, but stores
  it with an explicit little-endian write, so the bytes still do not depend on the byte order of the processor.
- **D11, M3: the unbuffered chunk is 16 MiB, not 8 MiB.** In three runs per size 16 MiB was about 8% faster than
  8 MiB; 32 MiB added 5% more and is not used. The buffered chunk is 1 MiB. The write path.
- **D11: the reason for one writer changed.** The probes behind "several writers were slower than one" gave each
  writer a region of one preallocated file, and NTFS fills the gap between the valid data length and a write with
  zeros first. With a file per writer, 4 or 8 writers take about 3.1 GiB/s against 2.2 to 2.5 for one. One writer
  stays, because the output is one file that grows from its start. M3 measures both layouts. The write path.
- **D11, M4: the file sink has two chunk buffers and a writer thread of its own**, in both write modes. A sink with
  one buffer reached 78% to 80% of the raw write, under the threshold of 90%, so the rule of D11 applied: the caller
  copies blocks into one buffer while the writer thread writes the other. The thread is dedicated rather than a
  thread-pool task by reasoning, not by measurement: a write must not queue behind the blocks being generated. The
  sink measured between 67% and 99% of the raw write, most runs at 86% or more; the cause of the slow runs was not
  found. The file sink.
- **D12: more checks before the old file is deleted, and one more signal.**
  - The temporary file is created, and deleted again, before an existing output is deleted, so a path that cannot
    be written does not cost the old file. The preallocation still comes after the deletion.
  - Outside Windows, free space is asked of the nearest existing directory above the output: the root `/` says
    nothing about the file system mounted at the output.
  - An output path that is a directory or ends with a separator is rejected before anything is touched. An empty
    output path is an invalid argument (exit code 2).
  - SIGHUP, which on Windows is the console window being closed, cancels like Ctrl+C and SIGTERM.
  - The token is checked for every block and after the prompt, not for every chunk: a block is at most 1 MiB, which
    keeps the reaction time to milliseconds.
  - A failed write of the settings, of the progress line or of the summary is reported as a failure of the console,
    not of the output file. Before the file is complete it stops the run with exit code 1; after the file is in
    place it costs only the summary, and the exit code stays 0. Removing the progress line never fails, and a
    cancelled run ends with 130 even when the error stream is broken. A pipe whose reader has gone is not a failure
    at all: the .NET runtime drops writes to it without an error.
  - The manual check of Ctrl+C (Risks, task 7.5) sent a real console Ctrl+C event to the published executable from
    a script instead of a key press: exit code 130 after 0.6 s, and no file left.
- **D13: the command line.**
  - The option name is added to every parse error in one place (`GeneratorApp.Describe`), not in each value
    parser.
  - A requirement was added after a review of the implementation, "Settings before the run" (task 7.8): every
    setting in effect, the defaults and a chosen seed included, goes to standard error before generation, because a
    run that fails or is cancelled prints no summary.
  - `FileSort.Benchmarks` parses its command line with `System.CommandLine` too and reads sizes with the
    generator's `SizeParser`, so options are declared, checked and documented one way in every program.
  - The reproducing command line puts a path that contains a space in double quotes. Other characters that a shell
    treats specially are left as they are: no single form of quoting works in both cmd and PowerShell.
- **D10 and D15 were edited in place** during the implementation, before this section existed: the hashing and the
  discarding sinks are test doubles and live with their users, in the tests and the benchmarks, and the program
  holds only the file sink. The Context line describes the reference machine in general terms, as the public
  documents do.
- **D14, M5: memory.**
  - Chunk buffers: 2 × 16 MiB, or 2 × 1 MiB with `--buffered-io`, not 1 or 2 × 8 MiB.
  - The estimate for the pool was wrong. For `--words 1024` it is about 6 MiB, at most 13 MiB without edge cases
    and 22 MiB with `unicode` and `separator`, not 17 MiB.
  - The dictionary takes about 24 KB with its table of slots, not 10 KB.
  - The measured peak working set in the default unbuffered mode is 81 to 92 MiB with 16 threads from 1 GB to
    100 GB, and 132 MiB with 64 threads, against the expected 100 and 150 MB; the buffered mode takes 56 to 62 MiB.
    End to end; Memory.
- **D15: layout.** Folders group files by responsibility: `Cli/Options`, `Cli/Reports` (settings, progress and
  summary), `Cli/Terminal` (the console and the prompt), `Content/Blocks`, `Content/Lines`, `Content/Words`,
  `Output`, `Pipeline`, `Random`; one top-level type per file. The window of blocks in flight is a type of its
  own, `Pipeline/BlockWindow`. The tests mirror these folders. `FileSort.Benchmarks` has three experiments,
  `write-path`, `file-sink` and `pipeline`, besides the BenchmarkDotNet benchmarks.
- **D16: how the measurements ran.**
  - M2 ran through the `pipeline` experiment; a second round hit the full cache of the SSD and was discarded.
    Block size.
  - M3 added 32 MiB chunks, a series of three runs per chunk size and the two layouts of writers above; at 100 GB
    one writer was measured. The write path.
  - M4 ran through the `file-sink` experiment, with the one-buffer sink as an extra variant, in two runs. The file
    sink.
  - M6: generation alone is a BenchmarkDotNet benchmark of the pipeline into a discarding sink over 256 MiB
    (`micro --filter *Pipeline*`), not a 10 GB stopwatch run; end to end is one run per thread count at 10 GB.
    Where the limit is.
  - The experiments take `--runs` and print the median, the slowest and the fastest run. The disk tables were
    recorded before that and were not taken again, to spare the disk; each table says how many runs stand behind
    it.
- **The goal "throughput limited by the disk"** holds for large files, not for small ones. At 100 GB the generator
  writes 1680 MiB/s where the raw disk takes 1835 MiB/s; at 10 GB it writes 1701 MiB/s where the raw disk took
  about 2600 MiB/s at 8 GiB. End to end; The write path; Where the limit is.
- **Added to the change:** GitHub Actions (`ci.yml`: build, tests and coverage with a threshold of 80% of the lines;
  `publish.yml`: the generator's executable on demand), the test-only package
  `Microsoft.Testing.Extensions.CodeCoverage`, and the rule in `CLAUDE.md` that tests cover at least 80% of the
  lines of production code. Root `README.md`, Code coverage and Continuous integration.
