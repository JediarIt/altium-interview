# Spec Delta

## Purpose

Sorts a file of `line-format` lines into the order of `sort-order` within a memory budget, whatever its size, as
long as the disk has room for it and its copies: the sorter reads the input in chunks that fit the memory, keeps
each chunk, sorted, as a run in a temporary file, and merges the runs into the output in one or more merge passes.
The spec states what the output holds, how line endings, the byte order mark and invalid input are handled, what
happens to temporary files and to the output file, the command line, exit codes, memory and speed. The word "sort"
names one execution of the sorter; "run" always means a sorted part of the input.

This spec states what users and the other programs rely on. Smaller details of the command line, such as accepted
ranges, message texts and the layout of the report, are defined by the code and shown by `--help`. Sizes in this
spec use binary units: 1 MB is 1048576 bytes and 1 GB is 1073741824 bytes.

## ADDED Requirements

### Requirement: Command line
The sorter SHALL take the input file in `--input` and the output file in `--output`; both are required. `--help`
SHALL print a usage text that lists every option. An unknown option, a missing required option or a value that an
option does not accept SHALL make the sorter exit with code 2 and an error on standard error that names the option.
The two options SHALL name different files: the same file in both is an invalid argument.

#### Scenario: Help
- **WHEN** the sorter is run with `--help`
- **THEN** it prints the usage text, which names every option, to standard output and exits with code 0

#### Scenario: Missing required option
- **WHEN** the sorter is run with `--input` but without `--output`
- **THEN** it prints an error that names `--output` to standard error and exits with code 2

#### Scenario: Invalid arguments
- **WHEN** a command line with both required options also contains the unknown option `--inputt data/test.txt`, or
  the value `--line-ending cr`, or the value `--memory lots`
- **THEN** the sorter prints an error that names the option to standard error and exits with code 2

#### Scenario: The same file twice
- **WHEN** `--input` and `--output` name the same file
- **THEN** the sorter reads and writes nothing, prints an error that names both options and exits with code 2

### Requirement: Sorted output
The output SHALL hold exactly the lines of the input, as many times as the input holds each of them, each with the
same bytes as in the input, in the order of the `sort-order` capability. Every line of the output, the last one
included, SHALL be followed by the terminator of the line ending that the sort works with. The output SHALL hold
nothing else.

#### Scenario: Example of the task
- **WHEN** the input is the lines `415. Apple`, `30432. Something something something`, `1. Apple`,
  `32. Cherry is the best` and `2. Banana is yellow`, each followed by `\n`
- **THEN** the output is `1. Apple`, `415. Apple`, `2. Banana is yellow`, `32. Cherry is the best`,
  `30432. Something something something`, each followed by `\n`

#### Scenario: Lines keep their bytes
- **WHEN** the input holds the lines `007. Apple`, `5.  Apple` with two spaces after the period, and `5. Ёлка 😀`
- **THEN** the output holds these three lines with the same bytes: no leading zero is removed, no space is
  trimmed and no character is changed

#### Scenario: Repeated lines
- **WHEN** the input holds the line `7. Apple` three times
- **THEN** the output holds it three times, one right after another

#### Scenario: Last line without a terminator
- **WHEN** the input is the bytes `2. Banana\n1. Apple`
- **THEN** the output is the bytes `1. Apple\n2. Banana\n`

#### Scenario: Empty input
- **WHEN** the input has zero bytes
- **THEN** the sorter exits with code 0 and the output has zero bytes

### Requirement: Lines with equal keys
Lines with equal keys, as `sort-order` defines them, SHALL be written in the order of their bytes, compared as
unsigned bytes like Strings. The output SHALL therefore depend only on the bytes of the input and on the line
ending: not on `--threads`, `--memory` or `--temp`, and not on where the input was cut into chunks.

#### Scenario: Equal keys, different bytes
- **WHEN** the input holds the lines `7. Apple`, `007. Apple`, `07. Apple`, `00. Apple` and `0. Apple`
- **THEN** the output holds them in the order `0. Apple`, `00. Apple`, `007. Apple`, `07. Apple`, `7. Apple`: the
  Number 0 comes before the Number 7, and within each value the lines are in the order of their bytes; `0. Apple`
  comes before `00. Apple` because its second byte is a period, which is smaller than the second `0` of `00. Apple`

#### Scenario: Same output for every setting
- **WHEN** the same input is sorted with `--threads 1` and the smallest accepted `--memory`, and again with the
  default options
- **THEN** the two outputs are byte-identical

### Requirement: Line ending
`--line-ending` SHALL accept `auto` (the default), `lf` or `crlf`. With `auto` the line ending is taken from the
first line feed of the input: `\r\n` when a carriage return stands right before it, `\n` otherwise and when the
input has no line feed. The input SHALL be read and the output SHALL be written with that line ending.

#### Scenario: Detected carriage return and line feed
- **WHEN** the input is the bytes `2. Banana\r\n1. Apple\r\n`
- **THEN** the output is the bytes `1. Apple\r\n2. Banana\r\n`

#### Scenario: Carriage return at the end of a String
- **WHEN** the input is the bytes `2. Banana\n1. Apple\r\n`
- **THEN** both lines are read with `\n`, the String of the second one is `Apple` followed by a carriage return, and
  the output is the bytes `1. Apple\r\n2. Banana\n`

#### Scenario: Line ending given explicitly
- **WHEN** the input is the bytes `1. Apple\r\n2. Banana\n`
- **THEN** with `--line-ending lf` the sort succeeds, and without the option the sorter takes `\r\n` from the first
  line feed, finds line 2 not valid and exits with code 3

#### Scenario: Input without a line feed
- **WHEN** the input is the bytes `1. Apple`, with no terminator
- **THEN** the output is the bytes `1. Apple\n`

### Requirement: Byte order mark
A UTF-8 byte order mark at the start of the input SHALL be skipped: it is not part of the first line. The output
SHALL NOT start with a byte order mark.

#### Scenario: Mark at the start of the input
- **WHEN** the input is the bytes `EF BB BF` followed by `2. Banana\n1. Apple\n`
- **THEN** the output is the bytes `1. Apple\n2. Banana\n`

#### Scenario: Mark and nothing else
- **WHEN** the input holds only the bytes `EF BB BF`
- **THEN** the sorter exits with code 0 and the output has zero bytes

### Requirement: Invalid input
Every line of the input SHALL be valid as the `line-format` capability defines, for the line ending that the sort
works with. At the first line that is not valid, in the order of the file, the sorter SHALL stop and name the input
file, the number of the line, its offset in bytes from the start of the file and the reason, and exit with code 3.
It SHALL write no
output and leave no temporary file; an output file that existed before is left as it was.

#### Scenario: Line without a separator
- **WHEN** the input is the bytes `1. Apple\nApple\n` and no file exists at the output path
- **THEN** the sorter names line 2 and the offset 9, exits with code 3, and still no file exists at the output path

#### Scenario: Invalid line far into the input
- **WHEN** an input many times larger than `--memory` has one line with the bytes `C3 28` in its String, near the
  end of the input
- **THEN** the sorter reports that line with its number and offset, exits with code 3, writes no output and leaves
  no temporary file

#### Scenario: Empty line
- **WHEN** the input is the bytes `1. Apple\n\n2. Banana\n`
- **THEN** the sorter reports line 2 as not valid and exits with code 3

### Requirement: Line length
The sorter SHALL accept lines of up to 1 MB, not counting the terminator. A longer line SHALL end the sort with exit
code 1 and a message that names the line and mentions the line ending as a possible cause. The sort then writes no
output and leaves no temporary file. When the input has both a line that is too long and a line that is not valid,
the one nearer to the start of the file SHALL be reported.

#### Scenario: Line longer than supported
- **WHEN** the input holds a line of 1 MB and one byte
- **THEN** the sorter exits with code 1 with a message that names that line and mentions the line ending

#### Scenario: Invalid line before a long line
- **WHEN** the input is the bytes `1. Apple\nApple\n` followed by a line of 1 MB and one byte, and it is sorted
  once with default options and once with the smallest accepted `--memory`
- **THEN** both sorts report line 2 as not valid and exit with code 3

#### Scenario: Longest line
- **WHEN** the input holds lines of exactly 1 MB among short ones
- **THEN** the sort succeeds and the output is correct

### Requirement: Memory budget
`--memory` SHALL set the largest peak working set of the sorter process; the default is 1 GB. For every input,
whether the sorter accepts it or rejects it, and every accepted value of `--threads`, the peak working set SHALL NOT
exceed that value. An input of any size SHALL sort within it, using temporary files on disk.

#### Scenario: Peak memory for a file of 1 GB
- **WHEN** a 1 GB file made by the generator with default options is sorted with default options
- **THEN** the peak working set reported in the summary is at most 1 GB

#### Scenario: Input larger than the buffers
- **WHEN** a 100 MB file made by the generator, larger than the buffers that the smallest accepted `--memory`
  leaves to the sort, is sorted with that `--memory`
- **THEN** the sort writes runs and merges them, the output is correct, and the peak working set is at most that
  value

#### Scenario: Many threads on few processors
- **WHEN** a 100 MB file is sorted with the largest accepted `--threads` and the smallest accepted `--memory`,
  limited to two logical processors
- **THEN** the peak working set is at most the value of `--memory`

#### Scenario: Long lines
- **WHEN** a file of 100 lines of 1 MB each is sorted with the smallest accepted `--memory`
- **THEN** the output is correct and the peak working set is at most that value

### Requirement: Parallelism
`--threads` SHALL set the largest number of threads that parse, sort and merge lines at the same time; the default
is the number of logical processors available to the process or the largest accepted value, whichever is smaller,
and the largest accepted value is at least 64. The output does not depend on it, as "Lines with equal keys"
requires.

#### Scenario: Default number of threads
- **WHEN** the sorter is run without `--threads` on a machine with 16 logical processors
- **THEN** the settings it prints give 16 threads

#### Scenario: Number of threads out of range
- **WHEN** the sorter is run with `--threads 0`
- **THEN** it prints an error that names `--threads` to standard error and exits with code 2


### Requirement: Temporary files
The sorter SHALL keep its runs in a new folder of its own, which it creates in the directory given in `--temp`; the
default is the directory of the output. It SHALL delete that folder with everything in it when the sort ends,
however it ends, unless the process is killed. Apart from that folder, and from the output and its temporary file
of "Output file" when the directory is the output's, it SHALL create, change or delete nothing in that directory.

#### Scenario: Default place
- **WHEN** an input larger than `--memory` is sorted without `--temp`
- **THEN** the runs are created in a new folder in the directory of the output

#### Scenario: Nothing left behind
- **WHEN** a sort with `--temp` naming a directory other than the output's ends successfully, or ends with an
  invalid input, a failure or a cancellation
- **THEN** that directory holds the same entries as before the sort

#### Scenario: Other files untouched
- **WHEN** `--temp` names a directory that holds files of the user
- **THEN** after the sort those files exist with the same bytes

### Requirement: Free space
Before it reads the input, the sorter SHALL check that the volumes of the temporary folder and of the output have
the free space that the sort needs: about the size of the input for the runs and as much for the output, the sum of
the two when they share a volume. When they do not, it SHALL exit with code 1 with a message that says how much
space is needed and how much is free.

#### Scenario: Not enough disk space
- **WHEN** the volume of the output has less free space than the sort needs
- **THEN** the sorter exits with code 1 before it reads the input, writes nothing, and its message gives the space
  needed and the space free

### Requirement: Output file
The output path SHALL never hold a partially written file: the output is written under a temporary name next to it
and gets its name when complete. An existing output SHALL be replaced only with `--overwrite` or a confirmation
typed at a prompt; with no console to ask, that counts as a refusal, which leaves the file untouched and exits with
code 1. After consent the old file is deleted once the whole input has been read, right before the output is written.

#### Scenario: Successful sort
- **WHEN** a sort finishes successfully
- **THEN** the output file exists and no temporary file is left next to it

#### Scenario: Overwrite option
- **WHEN** the output file exists and `--overwrite` is given
- **THEN** the file is replaced without a prompt

#### Scenario: Declined at the prompt
- **WHEN** the output file exists and the user presses Enter or answers `n` at the prompt
- **THEN** the file is left untouched and the sorter exits with code 1

#### Scenario: No console to ask
- **WHEN** the output file exists, standard input is redirected and `--overwrite` is not given
- **THEN** no prompt is shown, the file is left untouched, and the sorter exits with code 1 with a message that
  mentions `--overwrite`

### Requirement: Settings, progress and summary
Before it reads the input the sorter SHALL print to standard error the settings it works with: both paths, the
temporary directory, the memory, the threads and the line ending option. It SHALL show progress on standard error
unless that stream is redirected. On success it SHALL print a summary to standard output, which gets nothing else:
the line count, the byte count, the line ending used, the number of runs and of merge passes, the elapsed time, the
throughput and the peak working set.

#### Scenario: Options that were not given
- **WHEN** the sorter is run with only `--input` and `--output`
- **THEN** before any progress it prints to standard error both paths and the values of `--temp`, `--memory`,
  `--threads` and `--line-ending`

#### Scenario: Summary after a successful sort
- **WHEN** a sort finishes successfully
- **THEN** standard output holds the summary with every listed field and nothing else

#### Scenario: Redirected error stream
- **WHEN** standard error is redirected to a file
- **THEN** the settings are written to it and no progress updates are

### Requirement: Exit codes and error messages
The sorter SHALL exit with 0 when the output is written; with 3 when the input is not valid; with 2 on invalid
arguments; with 130 on cancellation; and with 1 when anything else prevents the sort, such as a file that cannot be
read or written, a line longer than supported, too little disk space or a declined overwrite. Every non-zero exit
SHALL come with a message on standard error that names the cause. A stack trace SHALL NOT be shown.

#### Scenario: Missing input file
- **WHEN** the file given in `--input` does not exist
- **THEN** the sorter exits with code 1 with a message that names the path and shows no stack trace

#### Scenario: Unwritable output path
- **WHEN** the output path is a directory or cannot be written
- **THEN** the sorter exits with code 1 with a message that names the path and shows no stack trace

### Requirement: Cancellation
On Ctrl+C or a termination signal the sorter SHALL stop within 2 seconds, delete its temporary folder and the
partially written output, report the cancellation and exit with code 130.

#### Scenario: Ctrl+C during the merge
- **WHEN** no file exists at the output path and the user presses Ctrl+C while the runs of a large input are being
  merged
- **THEN** the sorter reports the cancellation and exits with code 130 within 2 seconds, and neither the output
  file, nor its temporary file, nor the temporary folder is left

#### Scenario: Ctrl+C while a chunk is sorted
- **WHEN** the user presses Ctrl+C while a large chunk is being sorted with `--threads 1`
- **THEN** the sorter exits with code 130 within 2 seconds and leaves no temporary folder

### Requirement: Throughput
The sorter SHALL sort a 1 GB file in under 60 seconds with default options on the reference machine described in
`README.md`, with a peak working set under 2 GB in the same sort.

#### Scenario: A file of 1 GB
- **WHEN** a 1 GB file made by the generator with default options is sorted with default options on the reference
  machine
- **THEN** the sort takes under 60 seconds, and its peak working set is under 2 GB
