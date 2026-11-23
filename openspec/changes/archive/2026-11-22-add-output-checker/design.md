# Design

## Context

See `proposal.md` for the motivation and the scope. What shapes the design:

- `FileSort.Checker` is a placeholder. It must not reference `FileSort.Common`: it is an oracle with its own
  deliberately simple parsing and comparison, so that a bug in the optimized code cannot be confirmed by the same
  code. An oracle that is wrong gives false confidence, so simple code is worth more here than fast code.
- No sorter exists. Sorted files for tests and measurements are made by a naive in-memory sort.
- The generator already has everything a console program needs (exit codes, a console abstraction, a progress
  line, Ctrl+C), inside its own project. `System.CommandLine` 2.0.15 is in use.
- The checker publishes as a self-contained single file, like the generator, so it stays trim- and AOT-safe.
- The reference machine is the only one available; nothing is measured on Linux or macOS.
- Measurements must spare the disk: files of 1 GB, written once and then only read.
- Sizes use binary units, as in the specs: 1 GB is 1 GiB, and MB and MiB mean the same.
- The specs state the contract. Details below that level (message texts, the layout of the report, sizes of
  buffers, names of types) are decided here and written only here. Once the code exists, the code, its tests and
  `--help` are their single home; this document is archived with the change. The one deliberate second copy is the
  table of options in the checker's README (D13). Type names below are for orientation and may change.

## Goals / Non-Goals

**Goals:**

- A verdict that can be trusted: every kind of wrong output that a sorter can plausibly produce is rejected, and
  the tests show it kind by kind (D14).
- Code simple enough to be read in one sitting: one thread per file, and next to nothing shared between the two
  (D11 lists it).
- Memory that does not depend on the size of the files.
- A pair of 100 GB files checked in minutes.
- One way to build a console program in this solution: the checker and the generator share the console plumbing.

**Non-Goals:**

- Telling which lines differ between the files, or repairing anything.
- Protection against an output forged on purpose.
- Unbuffered reads, and any tuning beyond what the requirement "Throughput" needs.
- A shared library for the checker's own logic: nothing else consumes it.

## Decisions

### D1. Three checks, one verdict

The checker establishes three facts (both files are valid, the output is in order, the output has the lines of the
input) and gives one of three results. The rows are in order of precedence:

| What the files are like | Result | Exit code |
|---|---|---|
| A line of either file is not valid | `INVALID FILE` | 3 |
| Both files are valid; the order is violated, or the lines differ, or both | `NOT CORRECT` | 4 |
| Both files are valid, the output is in order and has the lines of the input | `CORRECT` | 0 |

The input is read once and gives the digest of its lines (D4): two counts and one hash. The output is read once and
gives the same plus the order check. Besides a few counters and one previous line nothing is kept, which is why
memory does not depend on the size.

Validity comes first because the other two facts are about keys and lines, and a line that is not valid has no
key. The order check and the comparison of the lines are independent, and both are always reported: "the lines are
the same, the order is wrong in a million places" points at the comparison code of a sorter, and "the order is
right, one line is missing" points at its input or output code.

Why the input is needed at all: the order alone proves little. An empty output is in order, and so is an output
that lost lines or has one line twice in place of another. These are the typical faults of an external sort (the
tail of a buffer that was never written, a line repeated where two temporary files meet), and they leave the order
perfect. Only the comparison with the input shows them. The author asked this question when reading the plan, so
the README of the checker answers it first, with such an example.

### D2. The checker's own parsing

The checker parses a line in the plainest way that is correct:

1. A line is the bytes up to the terminator of the file's line ending (D5).
2. The Number is the run of ASCII digits at the start of the line; it must have at least one digit.
3. The two bytes after the digits must be `. `. This is the same rule as "the first `. ` in the line is the
   separator, and everything before it is digits": digits contain no period, so the first `. ` cannot come earlier.
4. The rest is the String. It must be well-formed UTF-8 (`Utf8.IsValid` of the framework). A line feed inside a
   line of a `\r\n` file never reaches the parser: the reader finds it first (D5, D7).

No value is computed from the digits: the Number stays a span of digits (D3). Parsing allocates nothing.

*Alternative:* the naive parser of the test project, which uses `BigInteger` and `string`. It allocates per line
and would make the 100 GB case take hours. It stays in the tests as the oracle for the checker's parser (D14).

### D3. Numbers are compared as digits

Strings are compared with `SequenceCompareTo` on their bytes, which is the comparison of `sort-order`: unsigned
bytes, and the shorter of two Strings first when one is the beginning of the other.

Numbers are compared without converting them: drop the leading zeros of both, the longer run of digits is the
greater value, and runs of equal length compare byte by byte. `0` and `000` both become an empty run and are equal.
This works for a Number of any length, so the checker needs no change when the sorter gets its fast path for
Numbers that fit in 64 bits. A 64-bit fast path in the checker would be faster by a few nanoseconds per pair of
equal Strings and would add a second code path to the oracle; it is not added.

Two neighbouring lines with equal keys and different bytes can only differ in leading zeros. They are counted, not
reported as a violation.

This is also where the change lifts the limit of `line-format`: a Number of any length is valid and comparable. In
the code that exists, the naive parser of the tests loses its two checks (at most 20 digits, a value within 64
bits), and the two constants of `FileSort.Common`, `MaxNumber` and `MaxNumberDigits`, keep their names and values
but are described as the 64-bit range (what the generator writes at most, and where the sorter's fast path will
end), not as limits of the format.

### D4. The same lines: a 128-bit sum of line hashes

For each file the checker keeps three numbers, the digest of its lines: the count of lines, the count of their
bytes without terminators, and the sum, modulo 2^128, of the `XxHash128` of every line (the bytes of the line
without its terminator). The output has the lines of the input when the three numbers of the two files are equal.

Why a sum and not an exclusive or. Under exclusive or two identical lines cancel each other, so an output that lost
a pair of duplicates and gained another pair has the same hash; the scenario "A pair of identical lines replaced by
another pair" is exactly that. Under addition a line that occurs n times contributes n times its hash.

How likely a miss is. If the two sets of lines differ, the difference of the sums is a sum of line hashes, each
multiplied by the difference in how often that line occurs. With hashes that behave like random 128-bit values this
is zero with a probability of 2^-128, about 3 in 10^39, as long as at least one of those differences is odd. The
weak spot of a sum modulo a power of two: if every difference is a multiple of 2^k, k bits are lost. A file of
100 GB has fewer than 2^35 lines, so even a bug that drops lines in equal groups of the largest possible size
leaves a probability below 2^-93. With a 64-bit sum the same reasoning gives 2^-64 in the usual case, 2^-44 when
1,048,576 identical lines (2^20) disappear at once, and 2^-29 in that worst case: small for anything a real bug
does, but it needs an argument, and 128 bits do not.

Why the raw bytes and not the parsed key. The task says that the output contains the lines of the input. Hashing
the bytes makes `007. Apple` and `7. Apple` different lines, so a sorter that rewrites Numbers is caught.

The two counts are not needed for the proof, since the sum already tells a missing line, and the spec names only
the count of lines and the hash. They make the report useful: "the output has 1 line fewer" says more than "the
hashes differ".

`System.IO.Hashing` 10.0.15 provides `XxHash128` and `XxHash3`. It is a Microsoft package from the runtime's own
repository, has no dependencies, and is the checker's second package after `System.CommandLine`. The hash is not
cryptographic and is not keyed: the checker guards against mistakes of a sorter, not against an opponent.

*Alternatives:*

- `XxHash3` (64 bits) with a 64-bit sum: enough by the numbers above, and faster by a little; M1 measures by how
  much. M1 only records the cost: the spec names a 128-bit hash, so the measurement does not switch anything. If
  `XxHash128` in M1 takes more than half of the time per line of the output scan of M2 on default content (its
  MiB/s and the average length of a line give that time), the question goes back to the author with the numbers.
- SHA-256 from the framework, with no package: a cryptographic strength that is not needed, at several times the
  cost for short lines (M1 measures it).
- A sum modulo a prime, which has no weak spot for multiples: more arithmetic to explain for a gain that 128 bits
  already give.
- Sorting the input independently and comparing byte by byte: that is a second sorter.

### D5. One line ending for the pair

`--line-ending` is `auto`, `lf` or `crlf`.

- With `lf` or `crlf` both files are read with that line ending, literally.
- With `auto` the checker looks at the first line feed of the input: a carriage return before it means `\r\n`,
  anything else means `\n`, and so does an input without a line feed. That line ending is then used for both files.
  The output is not examined on its own. The scenario "Carriage return at the end of a String" of `output-checker`
  shows why: in a sorted file the first line can be one whose String ends with a carriage return, and it would
  look like `\r\n`.

In a `\r\n` file the reader searches for line feeds and looks at the byte before each: a line feed without a
carriage return is not a terminator, and it makes its line invalid on the spot, before the line is parsed. So an
output written with `\n` for an input with `\r\n` fails at its first line, without reading 100 GB as one line. The
message says that the line ending `\r\n` was taken from the input.

The other direction cannot fail early. An output written with `\r\n` for an input with `\n` consists of valid lines
whose Strings end with a carriage return. The comparison of the lines fails, and the checker explains why. In a
`\n` file it counts the lines that are followed by a terminator and the ones among them whose last byte is a
carriage return. The summary gets a remark that the line endings of the two files seem to differ when the output
has lines followed by a terminator, every one of them ends with a carriage return, and that is not so for the
input.

Where the auto detection is wrong:

- An `\r\n` file of one line without a terminator is taken for an `\n` file: it has no line feed to look at. Read
  with `\n` it is the same one line, so the input is read correctly; the output is then expected with `\n` or with
  no terminator.
- An `\n` file whose first line has a String that ends with a carriage return is taken for an `\r\n` file. The
  generator never writes such a String.

In both cases the fix is to give `--line-ending`. The message about an invalid line mentions the option when the
line ending was detected.

*Alternative:* detect the line ending of each file separately and compare only the contents of the lines. A sorter
that converts line endings would pass, two options would be needed to override the detection, and the detection of
the output would be wrong for the scenario named at the start of this decision.

### D6. Byte order mark

The three bytes `EF BB BF` at the start of a file are skipped and are not part of the first line; anywhere else
they are ordinary bytes, which make a line invalid when they stand where a digit should. The summary says for each
file whether it starts with the mark. The result does not depend on it: whether the sorter keeps the mark of its
input is not decided yet, and `line-format` allows a file with it and without it.

### D7. An invalid line stops the check

A line can be invalid for one of these reasons, which the message names. The first one is found by the reader
while it looks for the end of the line, so it wins over the others:

- the line contains a line feed without a carriage return before it (only in a `\r\n` file);
- the line is empty;
- the line does not start with a digit;
- the digits are not followed by `. `;
- the String is not well-formed UTF-8.

The report gives the file, the number of the line (from 1), the offset of its first byte from the start of the
file (from 0; the first line of a file with a byte order mark has the offset 3), the reason, and the first 200
bytes of the line with control bytes and ill-formed UTF-8 shown as escapes. It ends with `Result: INVALID FILE`.

There is no reading on after an invalid line: without a key the line cannot take part in the order check, and
"skip it and go on" would hide the bugs that show up exactly this way, such as a tail cut inside a Number or a byte
order mark that travelled with a line.

A line longer than 64 MiB ends the check with exit code 1: it is a limit of the tool, not of the format. The limit
protects memory when a file is read with the wrong line ending; no real `Number. String` file comes near it. The
limit is on the bytes that the reader has gathered for one line without finding its end, whatever those bytes
are: a line of 100 MiB that starts with a letter gives exit code 1, not 3. Only the bare line feed above is
noticed earlier, because the reader meets it on the way. The limit is a setting of the application with no option
on the command line, so tests lower it. The spec and the README of the checker state the limit.

Three things end a scan before the end of its file: an invalid line, a line above the limit, and a read error.
The scan that meets one stops the other scan, so that a file that is wrong at its start does not cost the full
reading of 100 GB. The signal for this is separate from the cancellation by Ctrl+C, so a cancellation by the user
is never taken for it. Two rules keep the outcome from depending on which thread is faster:

- A scan looks at that signal only between buffers and never before it has handled its first buffer. A file that
  fits in one buffer is therefore never cut short by the other scan, and the outcome for such a pair is the same
  on every run. The tests of the check use such files; the tests with buffers of a few bytes are about the reader
  alone.
- When both scans ended early on their own, both ends are reported: an invalid line on standard output, a line
  above the limit or a read error on standard error. Of two invalid lines the input's comes first. The exit code
  is 3 when either end is an invalid line and 1 otherwise, so it agrees with the printed result.

For large files one dependence on timing remains and is accepted: if each file has a fault and the two lie far
apart, the scan that reaches its fault first stops the other before it reaches its own, so which of the two is
reported, and with which of the codes 1 and 3, can differ between runs. Both are failures, and each run names a
real fault.

### D8. The report

Order violations are printed while the files are being read, the first ten of them, so the user sees the first one
within moments and can stop the check. All of them are counted. Each violation names the kind (Strings out of
order, or equal Strings with Numbers out of order), the number of the line and shows the line and its predecessor,
shortened as in D7:

```
Order violation at output line 1234568: equal Strings, Numbers out of order
  1234567: 415. Apple
  1234568: 32. Apple
```

The summary follows when both files are read to the end:

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
Elapsed:          1.42 s
Throughput:       1442 MiB/s
Peak working set: 58 MiB
Result:           NOT CORRECT
```

"Same lines" says `OK`, or `FAILED` with what is known: the difference in the number of lines, or "the same number
of lines, different lines" when only the hashes differ, plus the remark about line endings of D5. After an invalid
line (D7) there is no summary: the report of that line is followed by `Result: INVALID FILE`.

What the checker says about the content of the files goes to standard output: violations, the report of an invalid
line with its result, the summary. Settings, progress and errors (a file that cannot be read, a line above the
limit, a cancellation) go to standard error. Counts and times are formatted with the invariant culture. Only the main
thread writes to the console: the scans put violations into a queue and add to a counter of bytes read, and the
main thread drains the queue and redraws the progress line a few times a second while it waits for the scans.

A console that cannot be written to (a stream redirected to a file on a full disk) is handled as in the generator.
A failed write of the settings or of the progress ends the check with exit code 1 and a message that names the
console. A failed write of a violation, of the report of an invalid line or of the summary does not change the
exit code, which still carries the
verdict; the error stream says that the report was lost, if it can.

### D9. Exit codes mean the same in every program

| Code | Meaning | Programs |
|---|---|---|
| 0 | Success; for the checker, the output is correct | all |
| 1 | The program could not do its work: a file that cannot be read or written, a declined overwrite, a line longer than supported | all |
| 2 | Invalid arguments | all |
| 3 | A file is not a valid `line-format` file | the checker; free for the sorter's malformed input, which the sorter change decides |
| 4 | Both files are valid and the output is not the sorted input | the checker |
| 130 | Cancelled | all |

The codes are defined once, in `FileSort.Cli` (D10). Code 4 is separate from code 1 because the most useful thing a
script can learn from a checker is the difference between "checked, and it is wrong" and "could not check".

*Alternative:* the convention of `diff` and `cmp`, where 1 is the negative answer and errors are 2 and above. It is
familiar, but 1 would then mean "failed" in the generator and "the answer is no" in the checker, and 2 is already
taken by invalid arguments.

The author confirmed the numbering of the table above at the review of this plan.

### D10. `FileSort.Cli`: the console plumbing shared by the programs

A new class library, referenced by the generator and the checker and later by the sorter's console host. It holds
what two programs use today, moved from the generator:

| What | From |
|---|---|
| `ExitCode`, with the codes 3 and 4 added | `FileSort.Generator/Cli` |
| `IAppConsole`, `SystemConsole`, `ConsoleWriteException` | `FileSort.Generator/Cli/Terminal` |
| `ProgressLine`, `ByteSize` | `FileSort.Generator/Cli/Reports` |
| Ctrl+C, SIGTERM and SIGHUP wired to one cancellation token | the body of the generator's `Program` |
| Running a parsed command: the description of parse errors that names the option, exit code 2, help and version through the console | `GeneratorApp.Run` and `GeneratorApp.Describe` |
| Writing an error without failing on a broken error stream | `GeneratorApp.TryWriteError` |

What stays in the generator: the overwrite prompt, the value parsers (sizes, word ranges, edge cases), the command
definition, the settings and the summary. They move when a second program needs them, which for the prompt and the
size parser is probably the sorter. The library references `System.CommandLine` and nothing else, is marked
AOT-compatible, and its types are public, like those of `FileSort.Common`.

The behavior of the generator does not change: its tests pass as they are, apart from namespaces, and the pinned
reference hashes stay.

The checker does not get the line ending from here or from `FileSort.Common`: it has its own three-valued setting.

*Alternative, raised by the author:* put these classes into `FileSort.Common` and have no second shared project.
Rejected, with the author's agreement at the review of this plan: two rules are enforced today by project
references, not by agreement. The checker cannot call
the shared line code because it has no reference to `FileSort.Common`; with the console classes there it would get
that reference, and its independence would become a promise. And the sorting engine must have no console
dependencies, but it will reference `FileSort.Common` for the line format; the console classes and
`System.CommandLine` would reach it through that reference. A test keeps the first rule visible: it fails if the
checker's assembly references `FileSort.Common`.

*Alternatives:* the same source files linked into each program (every type would exist once per assembly, and the
test project, which sees the internals of all of them, would not compile); a copy of about 200 lines in the checker.

### D11. Reading: one thread per file, streaming

Each file is scanned by its own task, and the two run at the same time. A scan reads the file from a `Stream` into
one buffer, of 4 MiB unless M3 chooses 1 or 16, handles every complete line in the buffer, moves the unfinished
tail to the start and reads on. A line that does not fit makes the buffer double, up to the limit of D7. The file
is opened for sequential reading with no second buffer inside `FileStream`. The scans take a `Stream`, so tests run
them on memory.

For each line the input scan parses it (D2) and adds it to the digest (D4). The output scan does the same and
compares the line with the previous one (D3), a copy of which it keeps in a buffer of its own. A scan allocates
nothing per line.

The two scans share four things and nothing else: the cancellation token of Ctrl+C, the signal with which one scan
stops the other (D7), a counter of bytes read and the queue of violations (D8). Ctrl+C is checked once per buffer,
from the first one on, which keeps the reaction within milliseconds.

Expected speed: the work per line is two vectorized searches, a UTF-8 validation, one hash and one comparison,
estimated at 40 to 70 ns for a line of 25 bytes, which is 340 to 600 MiB/s per file. The requirement "Throughput"
needs 341 MiB/s per file, so the estimate does not settle whether one thread per file is enough; M2 and M4 do.

Decision rule: if the median of M4 for the default pair does not meet the requirement "Throughput", the scan
becomes parallel within a file. No reserve is asked on top of the requirement: nobody times the checker, a check of
100 GB in five minutes is already short next to the sort it follows, and every step away from the simple scan is a
risk to the oracle. The parallel scan works like this. The file is cut into chunks at
line boundaries; workers parse and hash chunks and check the order inside each; the chunks are consumed in order
through a bounded window, as the generator consumes its blocks, which gives the line numbers and the comparison of
the last line of a chunk with the first line of the next. The sum of hashes does not depend on the order, so the
chunks need no coordination for it. This is several times faster and has more places to be wrong, so it is built
only if the rule asks for it. If it is built, it comes with a `--threads` option like the generator's, because the
degree of parallelism has to be configurable, and the spec and the options table get that option in the same
task.

Reads go through the file cache of the operating system. Unbuffered reads would need aligned buffers and would
only matter if the disk were the limit, and two sequential reads at the required rate are far below what an NVMe
disk delivers.

### D12. Memory accounting

| Item | Size | Scales with |
|---|---|---|
| Read buffers, one per file | 2 × 4 MiB, or what M3 chooses | the longest line, up to 2 × 64 MiB |
| Copy of the previous line of the output | the longest line so far | the longest line, up to 64 MiB |
| The first ten violations, two shortened lines each | under 10 KB | constant |
| Runtime, compiled code, thread stacks | to be measured, expected 30 to 50 MB | constant |

Nothing scales with the size of the files. The expected peak working set is 40 to 60 MB, against the limit of
256 MB that the spec sets for files whose lines are at most 1 MB; M4 measures it at 100 MB and at 1 GB. Files read
through the file cache occupy the cache of the operating system, not the working set of the process. The garbage
collector stays in its default mode: there is nothing for it to collect. The 2 GB budget of the task is the
sorter's; the checker uses a few percent of it.

### D13. Command line

| Option | Accepted values | Default |
|---|---|---|
| `--input`, `-i` | the path of the input file | required |
| `--sorted`, `-s` | the path of the sorted file, the output of the sorter | required |
| `--line-ending` | `auto`, `lf` or `crlf` | `auto` |

The input keeps the name it has in the sorter, `--input`. The sorted file is given in `--sorted`, not in
`--output`: in the generator and the sorter `--output` is the file that the program writes, and the checker writes
nothing. A first draft used `--output`, so that the paths of a sort could be passed under the same names, and the
author took it for a file that the checker creates. An empty path is an invalid argument, and so is the same file in both options: a
file compared with itself always has "the same lines", and a slip of the hand would get `CORRECT` for a check that
compared nothing. The two full paths are compared as the file system of the platform compares names; a second
name for one file, such as a link, is not looked for. There is no option for threads or buffers: the defaults are
chosen by measurement, and an oracle with fewer switches has fewer ways to be run wrong (D11 names the one case
that adds `--threads`).

Before reading, the checker prints its settings to standard error (both paths in full and the line ending option),
then shows one progress line: the share of the bytes of both files read so far, the throughput and the time left.

The checker's README lists the options in a table typed by hand, and a test compares the option names and the fixed
defaults of the table with the command, as for the generator. The code that reads a table from a README becomes a
helper of the tests, used for both programs.

### D14. Tests

The checker is tested against two things that share no code with it and live in the test project:

- the naive parser, changed to accept a Number of any length and to carry its value as a `BigInteger`;
- a naive comparison of two lines parsed by the naive parser, which is a plain loop over the bytes of the Strings
  and then the `BigInteger` values, and a naive sort built on it.

Kinds of tests:

- Every scenario of `sort-order`, run against the checker's comparison. Every scenario of `line-format` except
  those of "Canonical form of a written line", which is about writing: the scenarios of "Line structure", "Number"
  and "String" against the checker's parser, those of "Line terminator" and "File structure" against its reader
  together with the parser.
- Every scenario of `output-checker` that is not a measurement. The check of a pair takes two streams, so the
  scenarios about the result run on streams in memory. The application opens the files and hands their streams to
  the check, so the scenarios about the command line, the files, the printed report and the exit codes run through
  the application in-process, with the console of the tests and files of a few bytes.
- Correct pairs: files made by the generator in memory (1 MB, both line endings, default options and
  `--edge-cases all`) and sorted by the naive sort must be `CORRECT`. Lines that the generator cannot write yet,
  such as Numbers above 64 bits and Strings that end with a carriage return, are added by the tests.
- The catalogue of breakages: a correct pair is broken in one way at a time, and each must give the stated result.

| Breakage of the output | Expected |
|---|---|
| One line removed | `NOT CORRECT` |
| One line written twice | `NOT CORRECT` |
| One line replaced by a copy of another | `NOT CORRECT` |
| A pair of identical lines replaced by a pair of another line | `NOT CORRECT` |
| Two neighbouring lines with different Strings swapped | `NOT CORRECT`, the lines are the same |
| Two neighbouring lines with equal Strings and different Numbers swapped | `NOT CORRECT`, the lines are the same |
| Numbers ordered as text within equal Strings | `NOT CORRECT`, the lines are the same |
| Strings ordered without regard to letter case, or by UTF-16 code units (on the pair with `--edge-cases all`: default content has the same order under the right comparison and under these wrong ones) | `NOT CORRECT`, the lines are the same |
| One byte of a String changed, the order kept | `NOT CORRECT`, the order is right |
| A leading zero of a Number removed | `NOT CORRECT`, the order is right |
| A space at the end of a String removed | `NOT CORRECT` |
| Two lines joined: the terminator between them lost | `NOT CORRECT` |
| The file cut inside a String, between two characters | `NOT CORRECT` |
| The file cut inside a character of several bytes | `INVALID FILE` |
| The file cut inside a Number | `INVALID FILE` |
| An empty line inserted | `INVALID FILE` |
| The byte order mark of the input kept in front of a line in the middle | `INVALID FILE` |
| The other line ending | `INVALID FILE` or `NOT CORRECT`, as in D5 |
| `007. Apple` and `7. Apple` swapped | `CORRECT` |
| The terminator of the last line added or removed | `CORRECT` |
| A byte order mark added or removed at the start | `CORRECT` |

- The reader on buffer boundaries: the same content read with buffers of a few bytes gives the same result as with
  one large buffer, including a terminator split between two reads and a byte order mark split between two reads.
- The digest: the sum does not depend on the order of the lines, and the pair-for-pair replacement changes it.
- `FileSort.Cli`: tests of its own, so that its coverage does not depend on which program's tests run (D16).

Tests write next to nothing to the disk: a few small files for what only a file can show (a missing file, files
left untouched).

Coverage follows the rule of `CLAUDE.md`: at least 80% of the lines, for the whole and for each class, or a reason
in the README. The entry point, the pass-through console and the wiring of the signals are checked on the
published executable instead, as for the generator, and the README says so. CI enforces the total of a program's
assemblies and lists the classes below the threshold, as it does today (D16).

### D15. Project layout

- `FileSort.Cli` (new, in `FileSort.slnx`): the types of D10, in folders by responsibility.
- `FileSort.Checker`:
  - `Lines/`: the checker's own understanding of `line-format` and `sort-order`: splitting a stream into lines, parsing
    a line, comparing two lines.
  - `Checks/`: the order check, the digest of a set of lines, the scan of a file, the check of a pair and its
    result.
  - `Cli/`, `Cli/Options/`, `Cli/Reports/`: the application, the command and its settings, the settings printout,
    the violations and the summary.
  - Types are internal and visible to the tests and the benchmarks. References: `FileSort.Cli`,
    `System.CommandLine`, `System.IO.Hashing`. No reference to `FileSort.Common`. The project is marked
    AOT-compatible, as the generator is: that is what turns on the trimming, single-file and AOT analyzers, and
    without it the requirement "Standalone executable" would hold with the analyzers off.
- `FileSort.Tests`: `Checker/` and `Cli/` mirror the folders of the code; the console of the tests moves to `Cli/`.
- `FileSort.Benchmarks`: the benchmarks of M1 and M2 and two experiments, `sorted-copy` and `check` (D17). It
  references `FileSort.Checker`.

### D16. Continuous integration by program

Path filters work per workflow file, so each program gets a workflow of its own, and the steps live once in a
workflow that the others call:

| File | Role |
|---|---|
| `test-program.yml` | Called by the others. Builds the solution, runs the tests of the given namespaces with coverage, writes the test and coverage summaries, fails when the given assemblies, taken together, are under 80% of their lines, uploads the results |
| `generator.yml` | Tests in the namespaces of the generator, of `FileSort.Common`, of the naive parser and of `FileSort.Cli`; coverage of `FileSort.Generator`, `FileSort.Common` and `FileSort.Cli` |
| `checker.yml` | Tests in the namespaces of the checker, of the naive parser and of `FileSort.Cli`; coverage of `FileSort.Checker` and `FileSort.Cli` |

`ci.yml` is removed. What starts a workflow:

| Change | `generator.yml` | `checker.yml` |
|---|---|---|
| `FileSort.Generator/**`, `FileSort.Common/**` | yes | yes: the checker's tests make their inputs with the generator |
| `FileSort.Checker/**`, `FileSort.Tests/Checker/**` | no | yes |
| `FileSort.Cli/**`, `FileSort.Benchmarks/**`, the rest of `FileSort.Tests/**` | yes | yes |
| `FileSort.Sorter/**`, until the sorter has a workflow of its own: the solution builds it | yes | yes |
| The solution file, the shared build files, `test-program.yml`, `.github/scripts/**` | yes | yes |
| `generator.yml` | yes | no |
| `checker.yml` | no | yes |
| `FileSort.Generator/README.md`, whose options table a test reads | yes | no |
| `FileSort.Checker/README.md`, whose options table a test reads | no | yes |
| Any other Markdown file | no | no |

The three rows about Markdown files win over the rows of the folders.

Each workflow builds the whole solution: the test project references every program, and warnings are errors in all
of them. The tests are selected with `--filter-namespace`, which the test runner of this project accepts with a
`*` at the end and with several namespaces. `coverage-summary.ps1` gets a parameter with the assemblies to count.
The sorter gets a third workflow in its change. The commands that run everything at once stay as they are; the
commands of one program are added to `CLAUDE.md`.

`publish.yml` gets a second input, the program, next to the platform. For `win-x64` the published checker is run on
two small pairs written by the workflow: a correct one must give exit code 0 and a broken one exit code 4.

*Alternative:* one workflow for everything, as today. The whole run takes under two minutes, and the split saves
the generator's tests only when a commit touches the checker alone. The author chose the split now, with the same
structure ready for the sorter, whose tests will be the slow ones.

### D17. Measurements and what they write

| Id | Measurement | Decides or confirms |
|---|---|---|
| M1 | BenchmarkDotNet: the hash of one line with `XxHash128`, `XxHash3` and SHA-256, over a million generated lines | The cost of the choice of D4 |
| M2 | BenchmarkDotNet, one core, 64 MiB of sorted generated lines in memory: the input scan and the output scan, for default options and for `--edge-cases all`; MiB/s and allocated bytes | The cost per file; no allocations per line |
| M3 | Experiment `check`: the whole check of the default pair from its files, with read buffers of 1, 4 and 16 MiB, five runs each, median and spread | The read buffer of D11 |
| M4 | The published executable on the default pair, the edge-case pair and the small pair: elapsed time, throughput and peak working set from its summary; five runs for the default pair, one for each of the others | "Throughput", "Bounded memory", the decision rule of D11, the README table |

M1 and M2 run over a million lines or more, not over a short series in a loop: the generator's benchmarks showed
that a short series lets the processor learn the branches and hides their cost. The setup of M2 generates its
lines and sorts them in memory with the checker's own comparison; `sorted-copy` below uses the same code.

The pairs, in the ignored `data/` directory:

| Pair | Input | Written |
|---|---|---|
| Default | 1 GB, default options, a fixed seed | 1 GB + 1 GB |
| Edge-case | 1 GB, `--edge-cases all --line-ending crlf`, a fixed seed | 1 GB + 1 GB |
| Small | 100 MB, default options | 0.1 GB + 0.1 GB |

About 4.2 GB is written, once; every measurement after that only reads. The sorted copy of each input is made by
the experiment `sorted-copy`: it reads a file of up to 2 GB into memory, orders an index of its lines with the
checker's own comparison and writes the lines out. That file serves only to measure time. It proves nothing about
the checker, and the tests do not use it; their sorted files come from the naive sort of D14.

Checking 100 GB is not measured here. It is measured once, on the output of the sorter's own 100 GB run, and the
spec gets its scenario then.

## Risks / Trade-offs

- [The checker itself is wrong and accepts a wrong output] → the catalogue of D14 names the kinds of wrong output
  and has a test for each; the parser and the comparison are tested against the naive ones; the code has one thread
  per file and no tuning.
- [A hash can collide] → the probability is below 2^-93 in the worst case of D4; the README states that the check
  is probabilistic and is not a defence against forgery.
- [A hash mismatch does not say which line differs] → the report gives the difference in the number of lines and
  the remark about line endings; for small files the two can be compared after a naive sort. Stated in the README.
- [The estimate of D11 straddles the requirement, so one thread per file may be too slow] → the decision rule
  switches to the parallel scan, which is designed; the tasks carry it as a conditional task.
- [The measurements are taken with a warm file cache, at 1 GB] → they show the processor cost of the checker. The
  disk is not expected to be the limit at the required rate, and the 100 GB run with the sorter, whose files cannot
  fit in the cache, settles it.
- [Auto detection takes `\r\n` for a `\n` file whose first String ends with a carriage return] → the message names
  `--line-ending`; the generator never writes such a String.
- [An output with the other line ending gives exit code 3 in one direction and 4 in the other] → both are failures
  with a message that names the line endings; the codes follow their definitions.
- [For large files with a fault in each, which fault is reported can depend on timing (D7)] → no file of the
  tests is cut short by the other scan, so tests are repeatable; a run always names a real fault and never passes.
- [The scenarios "Largest Number" and "Writing the largest Number" of `line-format` keep names that no longer fit]
  → OpenSpec does not let a modified requirement drop or rename a scenario; their texts now say "the largest that
  fits in 64 bits".
- [Lifting the Number limit before the sorter exists] → the generator still writes at most 20 digits, so no
  existing file or reference hash changes; this change describes the constants in `FileSort.Common` as the 64-bit
  range (D3), which is what the sorter's fast path will use.
- [The checker now enforces two rules of the sorter that the sorter change was going to decide: byte-exact lines
  and the line ending of the input] → both are stated as assumptions in the proposal, and the sorter change starts
  from them.
- [Moving classes out of the generator breaks it] → its tests and the pinned reference hashes run unchanged, and
  the check publish with trimming is repeated for the generator.
- [The new workflows cannot be run before a push] → the commands they run are run locally, filters and coverage
  script included; the first push is their first real test.
- [`System.IO.Hashing` in a tool that must stay trim-safe] → the check publish with trimming covers the checker and
  both its packages.
- [A real Ctrl+C cannot be sent reliably from an automated test on Windows] → the cancellation path is tested
  in-process through the token, and the published executable gets a real console Ctrl+C event from a script once,
  as the generator did.

## Open Questions

None that change this change. Three decisions of the sorter change will come back to the checker: the order of
lines with equal keys and different bytes (the checker can then verify it), the policy for malformed input lines,
and whether the output keeps the byte order mark of the input.

## Changes during implementation

The decisions above are the plan as it was reviewed, and they stay as written. The implementation departed from
them where a measurement or a review gave a reason; this section lists every departure. Where this document and
the code differ, the code, its tests and the READMEs are right. The measurements are in
`FileSort.Checker/README.md`, section Performance; each item names its subsection there.

- **D11: one thread per file was too slow, so the scan is parallel within a file.** The simple scan was built
  first, as D11 says. On the default pair of 1 GB it took 4.8 to 5.1 s (five runs for each of three read buffers)
  against the 3 s of "Throughput"; with the objects of the two scans created apart, 4.2 s (see the next item).
  The decision rule asked for the parallel scan, and task 5.4 carried it. It is built as D11 describes it: a
  thread per file reads and cuts chunks at line ends, workers check chunks, the results are taken in the order of
  the chunks. The published executable now checks the pair in under 2 s. There is one implementation of the scan,
  not two: with one thread the same chunks are checked one at a time. (Why the check is parallel; End to end;
  Threads.)
- **A finding on the way: the two scans wrote to the same cache lines.** The readers and counters of the two
  scans were created one after another by one thread and lay side by side in memory, while two threads wrote to
  them on every line. Creating each scan on its own thread took the pair from 5.1 s to 4.2 s. The parallel scan
  does not have the problem: the state of a chunk is created by the thread that checks it.
- **D11, D13: `--threads`.** The option exists, as D11 said it would if the parallel scan was built. It limits
  the chunks that are handed out at the same time over both files together, not per file: the two scans share one
  set of workers, so the option means one thing and the chunks in flight are bounded once. The two threads that
  read the files are not counted. It accepts 1 to 64 (see the item on memory). The spec `output-checker` got the
  requirement "Parallelism" and names the threads among the settings printed before the check; the README table
  and the settings printout have the option. "Parallelism" promises the same findings for every value only for a
  pair in which at most one file has a fault: with a fault in each, D7 already accepts that which one is reported
  can depend on timing.
- **D11: the reader became two types.** `ChunkReader` cuts a stream into chunks of whole lines (it has the byte
  order mark, the growth for a long line and the search for a line end within the limit), and `LineSplitter`
  splits the bytes of a chunk into lines (it has the line ending, the bare line feed and the limit on a line that
  has its end). The planned single reader with one buffer does not exist. A chunk is cut after the last line
  feed of what was read, whatever the line ending: read with `\r\n`, a chunk that ends with a bare line feed is
  reported by the splitter at that line, as a single reader would. A stream that can tell its length ends a chunk
  that took its last byte exactly as the last chunk, so a file of exactly one chunk is one chunk, as D7 needs.
  For the output the reader also puts a copy of the last line of a chunk in front of the next chunk, in the same
  buffer, so that the first line of a chunk is compared with its predecessor inside the chunk.
- **D11, M3: chunks of 1 MiB**, not 4 MiB. With 1, 4 and 16 MiB the check takes about the same time (1 MiB was
  the fastest in the last series), and the chunks in flight are the largest part of the memory. (Chunk size.)
- **D7: a line above the limit is a result, not an exception.** The reader that finds no end of a line within the
  limit does not know the number of that line: the numbers come from the order of the chunks. It hands over a
  chunk that stands for the line, and the scan of the file reports it when its turn comes, with the number and
  the offset it has in the file. The rules of D7 hold as written: the bare line feed is noticed first only when
  it lies within the limit, a line that is too long and not valid is too long, and the two scans look at the stop
  signal only between chunks, never before the first chunk was merged and never after the last one was read.
- **D12: memory, and what the review found.** The read buffers are chunks: one per chunk handed out and one more
  per file, of 1 MiB each, from the framework's pool of arrays. The independent review of the implementation found
  that the first version broke "Bounded memory": `--threads` accepted up to 256, every chunk handed out holds its
  buffer, and on two cores 256 threads took 323 MiB. Measuring the worst case of the spec, lines of 1 MB, found a
  second cause: every chunk copied its first, last and previous line into arrays of their own, megabytes of
  garbage per chunk that the collector reclaimed late, up to 251 MiB with 16 threads on 16 processors and 342 MiB
  with 64 threads on two cores. Now `--threads` accepts 1 to 64, the order check refers to the lines in the buffer
  instead of copying them, and the copy of the last line of a chunk that the next chunk needs travels in front of
  that chunk's bytes (see the reader above). The worst case measured is 160 MiB, in ten runs of 64 threads on two
  cores. (Memory.)
- **D4, M1, M2: the hash was not reopened.** `XxHash128` takes 16.2 ns per line, about a quarter of the 65 ns that
  one thread spends on a line of the output: under the half at which D4 sends the question back. M2 measures the
  check of chunks on one thread, because that is what a core does now; the scan of a file is no longer one loop.
  (One thread; The hash of a line.)
- **Not in the plan: tiered compilation is off for the checker.** With the default of .NET the first second of a
  check ran in unoptimized code: 2.6 to 3.9 s for the pair against 1.9 to 2.2 s in the same series with every
  method compiled with full optimization at once. It is one line of the project file, measured, and it applies to
  the checker only.
  (Compilation.)
- **D8: details of the report.** The thread that owns the console looks at the scans every 100 ms and draws the
  progress line once before the first wait, so a check of two small files shows it too. The settings printout
  names the files `Checking` and `Against input`. The remark about line endings is a line of its own in the
  summary, `Note`. The report of an invalid line ends with the line ending and where it came from. A line in a
  report shows a byte order mark and other characters that take no room as escapes of their bytes.
- **D10: what `FileSort.Cli` holds beyond the list.** `ProgramHost`, which is the whole entry point of a program
  (the signals and the message for an unexpected failure), the delegate `ConsoleApp`, `CancellationSignals`,
  `CommandRunner` and `AppConsoleExtensions.TryWriteError`: the code that D10 lists, under the names it got. The
  progress line treats a total of zero bytes as complete, which the generator never had. The test of D10 also
  checks that `FileSort.Cli` itself does not reference `FileSort.Common`.
- **D13: the threads option is parsed by the checker's own parser**, like the numbers of the generator: the
  library's conversion of a value that is not a number throws where a validator runs, and the error must name the
  option and the range.
- **D15: folders.** `Checks` has `Chunks`, `Order` and `Scans`; the application has `Cli/BackgroundWork`, which
  runs the check on other threads while the calling thread writes to the console. `FileSort.Benchmarks` was split
  by program (`Micro/Generator`, `Micro/Checker`, `Experiments/Generator`, `Experiments/Checker`) when it passed
  seven files per folder, and got `Sorting` for the in-memory sort.
- **D16: no YAML anchors.** The list of paths of a workflow is written twice, for pushes and for pull requests,
  as in the workflow it replaces. The coverage script counts the lines of the named assemblies itself: the report
  has totals only for all assemblies together.
- **D17: the experiment `check`** takes sizes of a chunk (`--chunks`) and thread counts (`--threads`), so that it
  measures both M3 and the scaling with threads. The pairs were written once, 4.2 GB in all.
- **Tests: mutations.** Before the review, ten faults were put into the checker one at a time (an exclusive or
  in place of the sum, Numbers compared as text, the pair of lines across two chunks not compared, line numbers
  not moved to the file, a bare line feed accepted, UTF-8 not checked, and others); the tests failed for each.
  After the review, three more for the new code (the line in front of a chunk not compared, the wrong line put in
  front, the end of a file not seen when a chunk takes its last byte); the tests failed for each.
- **Other fixes from the review.** A device or a pipe given as a file (`NUL` on Windows) is refused with its path
  and exit code 1, instead of an unexpected error: the checker needs the size of a file and reads the start of the
  input twice. The order of a pair of lines is decided by `LineComparer.Compare` alone, and the verdict that names
  the kind of a violation only looks at the Strings again when there is one; every scenario of `sort-order` runs
  through both. The progress line shows `--:--:--` for the time left before the first byte is read, instead of an
  estimate from a rate of one byte per second, and its hours do not wrap at a day.
