# Spec Delta

## Purpose

Confirms that an output file is the correctly sorted version of an input file: both files are valid as
`line-format` defines, the lines of the output are in the order of `sort-order`, and the output has exactly the
lines of the input. The checker is an oracle that shares no line parsing or comparison code with the sorter, and it
is fast enough for files of about 100 GB.

This spec states what users and the other programs rely on. Smaller details of the command line, such as message
texts and the layout of the report, are defined by the code and shown by `--help`. Sizes in this spec use binary
units: 1 MB is 1048576 bytes.

## ADDED Requirements

### Requirement: Command line
The checker SHALL take the input file in `--input` and the sorted file in `--sorted`; both are required. `--help`
SHALL print a usage text that lists every option. An unknown option, a missing required option or a value that an
option does not accept SHALL end the run with exit code 2 and an error on standard error that names the option.
The two options SHALL name different files: the same file in both is an invalid argument.

#### Scenario: Help
- **WHEN** the checker is run with `--help`
- **THEN** it prints the usage text, which names every option, to standard output and exits with code 0

#### Scenario: Missing required option
- **WHEN** the checker is run with `--input` but without `--sorted`
- **THEN** it prints an error that names `--sorted` to standard error and exits with code 2

#### Scenario: Invalid arguments
- **WHEN** a command line with both required options also contains the unknown option `--inputt data/test.txt`,
  or the value `--line-ending cr`
- **THEN** the checker prints an error that names the option to standard error and exits with code 2

#### Scenario: The same file twice
- **WHEN** `--input` and `--sorted` name the same file
- **THEN** the checker reads nothing, prints an error that names both options and exits with code 2

### Requirement: Parallelism
`--threads` SHALL set the largest number of threads that check lines at the same time: a whole number from 1 to
64, by default the number of logical processors or 64, whichever is smaller. For a pair in which at most one file has a line that
is not valid, what the checker finds SHALL NOT depend on it: the order violations with their line numbers, the
report of an invalid line, the counts of the summary, the result and the exit code are the same for every value.

#### Scenario: Result does not depend on the number of threads
- **WHEN** the same pair of files is checked with `--threads 1` and with `--threads 8`
- **THEN** both runs print the same order violations with the same line numbers, the same summary apart from the
  time, the throughput and the memory, and exit with the same code

#### Scenario: Number of threads out of range
- **WHEN** the checker is run with `--threads 0` or `--threads 65`
- **THEN** it prints an error that names `--threads` to standard error and exits with code 2

### Requirement: Files are only read
The checker SHALL only read the two files. It SHALL NOT create, change or delete any file.

#### Scenario: Nothing is written
- **WHEN** the checker has checked a pair of files, whatever the result
- **THEN** both files have the bytes they had before, and no other file has appeared next to them

### Requirement: Valid files
Every line of both files SHALL be valid as the `line-format` capability defines, for the line ending that the check
works with. At the first line it finds that is not valid the checker SHALL stop, name the file, the number of the
line, its offset in bytes from the start of the file and the reason, and end with the result `INVALID FILE` and
exit code 3. It then prints no summary and gives no verdict on the order or the lines; order violations found
earlier may already be printed.

#### Scenario: Output cut inside a Number
- **WHEN** the last line of the output is `12`, because the file was cut off
- **THEN** the checker names the output file, the number of that line and the reason, prints the result
  `INVALID FILE` and exits with code 3

#### Scenario: Invalid line in the input
- **WHEN** the input is the bytes `1. Apple\nApple\n`
- **THEN** the checker names the input file, line 2 and the offset 9, and exits with code 3

#### Scenario: Empty line
- **WHEN** the files are read with the line ending `\n` and the output contains the bytes `1. Apple\n\n2. Banana\n`
- **THEN** the checker reports line 2 of the output as not valid and exits with code 3

#### Scenario: Byte order mark inside the output
- **WHEN** a line in the middle of the output starts with the bytes `EF BB BF`, because a sorter took the byte
  order mark of its input for a part of the first line
- **THEN** the checker reports that line as not valid and exits with code 3

#### Scenario: Ill-formed UTF-8
- **WHEN** the String of a line of the output contains the bytes `C3 28`
- **THEN** the checker reports that line as not valid and exits with code 3

### Requirement: Order
The checker SHALL compare every line of the output with the line before it. A line that comes before its
predecessor in the order of the `sort-order` capability is an order violation, and an output with an order violation
is not correct. Lines with equal keys are in order whichever of them comes first.

#### Scenario: Sorted output
- **WHEN** the input is the five lines of the example of the task and the output is the same lines in sorted order
- **THEN** the output is correct

#### Scenario: Strings out of order
- **WHEN** the output is `2. Banana` followed by `1. Apple`
- **THEN** line 2 is an order violation and the output is not correct

#### Scenario: Numbers out of order
- **WHEN** the output is `10. Apple` followed by `9. Apple`
- **THEN** line 2 is an order violation and the output is not correct

#### Scenario: Equal keys in any order
- **WHEN** the input has the lines `007. Apple` and `7. Apple`, and the output has `7. Apple` followed by
  `007. Apple`, or the two in the other order
- **THEN** there is no order violation, and the summary counts one pair of neighbouring lines with equal keys and
  different bytes

### Requirement: Same lines
The output SHALL be correct only if it has exactly the lines of the input, each as many times as the input has it.
A line is its bytes without the terminator. The checker SHALL establish this by the number of lines and an
order-independent 128-bit hash of the lines, so that a difference between the two sets of lines goes unnoticed only
with a negligible probability.

#### Scenario: A line is missing
- **WHEN** the output is the sorted input without one of its lines
- **THEN** the output is not correct, and the summary gives the number of lines of each file

#### Scenario: A line replaced by a copy of another
- **WHEN** the output has as many lines as the input, but one line of the input is absent and another one occurs
  once more than in the input
- **THEN** the output is not correct

#### Scenario: A pair of identical lines replaced by another pair
- **WHEN** the input has `1. Apple` twice and `7. Kiwi` once, and the output has `7. Kiwi` three times
- **THEN** the output is not correct

#### Scenario: One byte changed
- **WHEN** one byte of a String differs between the output and the input, and the output is still in order
- **THEN** the output is not correct

#### Scenario: A leading zero removed
- **WHEN** the input has the line `007. Apple` and the output has `7. Apple` in its place
- **THEN** the output is not correct, although the two lines have equal keys

#### Scenario: Terminator of the last line
- **WHEN** the last line of the input has no terminator and the output ends with a terminator, or the other way
  round
- **THEN** this difference alone does not make the output incorrect

#### Scenario: Empty files
- **WHEN** both files have zero bytes
- **THEN** the output is correct, with zero lines

### Requirement: Line ending
`--line-ending` SHALL accept `auto` (the default), `lf` or `crlf`. With `auto` the line ending is taken from the
first line feed of the input: `\r\n` when a carriage return stands right before it, `\n` otherwise and when the
input has no line feed. One line ending applies to both files: the output is read with the line ending of the
input, and an output written with another line ending does not pass the check.

#### Scenario: Detected line feed
- **WHEN** the input is the bytes `2. Banana\n1. Apple\n` and the output is `1. Apple\n2. Banana\n`
- **THEN** the output is correct, and the summary gives the line ending `lf`

#### Scenario: Detected carriage return and line feed
- **WHEN** the input is the bytes `2. Banana\r\n1. Apple\r\n` and the output is `1. Apple\r\n2. Banana\r\n`
- **THEN** the output is correct, the summary gives the line ending `crlf`, and no String has a carriage return

#### Scenario: Carriage return at the end of a String
- **WHEN** the input is the bytes `2. Banana\n1. Apple\r\n` and the output is `1. Apple\r\n2. Banana\n`
- **THEN** both files are read with `\n`, as the first line feed of the input shows, the String of one line is `Apple`
  followed by a carriage return, and the output is correct

#### Scenario: Line ending given explicitly
- **WHEN** the input and the output are both the bytes `1. Apple\r\n2. Banana\n`
- **THEN** with `--line-ending lf` the output is correct, and without the option the checker takes `\r\n` from the
  first line feed of the input, finds line 2 not valid and exits with code 3

#### Scenario: Input without a line feed
- **WHEN** the input is the bytes `1. Apple`, with no terminator, and the output is `1. Apple\n`
- **THEN** both files are read with `\n`, and the output is correct

#### Scenario: Output with line feeds for an input with carriage returns
- **WHEN** the input is the bytes `2. Banana\r\n1. Apple\r\n` and the output is `1. Apple\n2. Banana\n`
- **THEN** the checker reports line 1 of the output as not valid for the line ending `\r\n`, says that the line
  ending was taken from the input, and exits with code 3

#### Scenario: Output with carriage returns for an input with line feeds
- **WHEN** the input is the bytes `2. Banana\n1. Apple\n` and the output is `1. Apple\r\n2. Banana\r\n`
- **THEN** the output is not correct, and the summary notes that every line of the output ends with a carriage
  return and not every line of the input does, which points at different line endings

### Requirement: Byte order mark
A UTF-8 byte order mark at the start of either file SHALL be skipped: it is not part of the first line and SHALL
NOT affect the result. The summary SHALL say which of the files start with one.

#### Scenario: Mark in the input only
- **WHEN** the input starts with the bytes `EF BB BF`, the output does not, and the output is otherwise correct
- **THEN** the output is correct, and the summary says that the input starts with a byte order mark

#### Scenario: Mark in the output only
- **WHEN** the output starts with the bytes `EF BB BF`, the input does not, and the output is otherwise correct
- **THEN** the output is correct, and the summary says that the output starts with a byte order mark

### Requirement: Report of order violations
An order violation SHALL NOT end the check: the checker reads on. It SHALL print the first ten order violations,
each with the number of the line that is out of order, that line and the line before it, and count all of them.

#### Scenario: A few violations
- **WHEN** the output has three order violations
- **THEN** all three are printed with their line numbers and lines, and the summary gives the count 3

#### Scenario: More violations than are printed
- **WHEN** the output has a thousand order violations
- **THEN** the first ten are printed, and the summary gives the count 1000

### Requirement: Summary and result
After both files are read to the end, the checker SHALL print a summary to standard output: for each file its
path, size, number of lines and byte order mark; the line ending; the order check with the number of violations;
the comparison of the lines; the number of pairs of neighbouring lines of the output with equal keys and different
bytes; elapsed time, throughput and peak working set; and the result, `CORRECT` or `NOT CORRECT`.

#### Scenario: Correct output
- **WHEN** a correct pair is checked
- **THEN** the summary contains every listed field and ends with the result `CORRECT`

#### Scenario: Both checks fail
- **WHEN** the output has an order violation and lacks a line of the input
- **THEN** the summary reports the order check and the comparison of the lines as failed, each on its own line,
  and ends with the result `NOT CORRECT`

### Requirement: Settings and progress
Before it reads the files, the checker SHALL print to standard error the settings it works with: both paths, the
value of `--line-ending` and the number of threads. It SHALL show its progress on standard error unless that stream is redirected. During a
check, order violations, the report of an invalid line with its result, and the summary go to standard output, and
nothing else does.

#### Scenario: Redirected error stream
- **WHEN** standard error is redirected to a file
- **THEN** the settings are written to it and no progress updates are

#### Scenario: Findings on standard output
- **WHEN** an output with an order violation is checked
- **THEN** standard output holds the violation and the summary, and standard error holds the settings

### Requirement: Exit codes and error messages
The checker SHALL exit with 0 when the output is correct; with 4 when both files are valid and the output is not
correct; with 3 when a file is not valid; with 2 on invalid arguments; with 130 on cancellation; and with 1 when
anything else prevents the check, such as a file that cannot be read. Every non-zero exit SHALL come with a message
that names the cause: in the report on standard output for the codes 3 and 4, on standard error for the others. A
stack trace SHALL NOT be shown.

#### Scenario: Exit code of a correct output
- **WHEN** a correct pair is checked
- **THEN** the exit code is 0

#### Scenario: Incorrect output
- **WHEN** both files are valid and the output has an order violation, or does not have the lines of the input,
  or both
- **THEN** the exit code is 4

#### Scenario: Missing file
- **WHEN** the file given in `--input` or in `--sorted` does not exist
- **THEN** the checker exits with code 1 with a message that names the path and shows no stack trace

### Requirement: Cancellation
On Ctrl+C or a termination signal the checker SHALL stop within 2 seconds, report the cancellation and exit with
code 130.

#### Scenario: Ctrl+C during a check
- **WHEN** the user presses Ctrl+C while a pair of files is being checked
- **THEN** the checker reports the cancellation and exits with code 130 within 2 seconds

### Requirement: Bounded memory
The memory used by the checker SHALL NOT grow with the size of the files. Its peak working set SHALL stay below
256 MB when no line is longer than 1 MB. A line longer than 64 MB SHALL end the check with exit code 1 and a
message, instead of using memory without limit.

#### Scenario: Peak memory for a pair of 1 GB
- **WHEN** a 1 GB file made by the generator with default options and its sorted copy are checked
- **THEN** the peak working set reported in the summary is below 256 MB

#### Scenario: Memory does not follow the size
- **WHEN** two pairs of a generated file and its sorted copy are checked, one of 100 MB files and one of 1 GB
  files
- **THEN** their peak working sets differ by less than 32 MB

#### Scenario: Line longer than supported
- **WHEN** a file contains a line longer than 64 MB
- **THEN** the checker exits with code 1 with a message that names the file and the line and mentions the line
  ending as a possible cause

### Requirement: Throughput
The checker SHALL check a pair of 1 GB files in under 3 seconds on the reference machine described in `README.md`.
That is the rate at which a pair of 100 GB files takes 5 minutes.

#### Scenario: A pair of 1 GB
- **WHEN** a 1 GB file made by the generator with default options and its sorted copy are checked five times on
  the reference machine
- **THEN** the median time of a check is under 3 seconds

### Requirement: Standalone executable
The checker SHALL be publishable as a single self-contained executable that runs on a machine without .NET
installed and gives the same results as the regular build. Publishing SHALL NOT report trimming, single-file or AOT
analysis warnings, and a check publish with trimming enabled SHALL NOT report trim warnings for the application or
its packages. The shipped executable stays untrimmed.

#### Scenario: Publishing for Windows
- **WHEN** `dotnet publish FileSort.Checker -c Release -r win-x64` is run
- **THEN** it produces one executable file and reports none of those warnings

#### Scenario: Trim analysis of the whole application
- **WHEN** the checker is published once more with `PublishTrimmed`, only as a check
- **THEN** the trimmer, which also analyses the referenced packages, reports no warnings

#### Scenario: Same results as the regular build
- **WHEN** the published executable checks a correct pair and a pair whose output has an order violation
- **THEN** it exits with code 0 for the first and with code 4 for the second, as the regular build does
