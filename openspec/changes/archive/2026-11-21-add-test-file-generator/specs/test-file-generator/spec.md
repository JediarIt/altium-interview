# Spec Delta

## Purpose

Creates test files in the `line-format` format for the sorter: a requested size, reproducible content with repeated
Strings and optional edge cases, produced fast enough for files of about 100 GB.

This spec states what users and the other programs rely on. Smaller details of the command line, such as accepted
ranges, message texts and the layout of the progress line, are defined by the code and shown by `--help`. Sizes in
this spec use binary units, as `--size` does: 1 MB is 1048576 bytes.

## ADDED Requirements

### Requirement: Command line
The generator SHALL take the output path in `--output` and the size in `--size`; both are required. `--help` SHALL
print a usage text that lists every option. An unknown option, a missing required option or a value that an option
does not accept SHALL end the run with exit code 2 and an error on standard error that names the option, and no file
SHALL be created.

#### Scenario: Help
- **WHEN** the generator is run with `--help`
- **THEN** it prints the usage text, which names every option, to standard output, exits with code 0 and creates no
  file

#### Scenario: Missing required option
- **WHEN** the generator is run with `--output` but without `--size`
- **THEN** it prints an error that names `--size` to standard error, exits with code 2 and creates no file

#### Scenario: Invalid arguments
- **WHEN** an otherwise valid command line contains `--sizee 1GB`, `--size 10XB`, `--duplicates 101` or
  `--edge-cases emojis`
- **THEN** the generator prints an error that names the option to standard error, exits with code 2 and creates no
  file

### Requirement: Size
`--size` SHALL accept a number greater than zero, which may have a fraction after a period and may be followed by a
unit: `B`; `KB`, `MB`, `GB` or `TB`; or the same four written as `KiB`, `MiB`, `GiB` or `TiB`. Units are
case-insensitive and binary in both spellings: `KB` and `KiB` are 1024 bytes, `MB` and `MiB` are 1024 times that, and
so on. The requested size is the number times the unit, rounded up to a whole byte.

#### Scenario: Units and fractions
- **WHEN** the size is `1048576`, `500mb`, `1GB`, `1.5GB` or `0.1KB`
- **THEN** the requested size is 1048576, 524288000, 1073741824, 1610612736 or 103 bytes, in that order

### Requirement: Output size
The output SHALL be at least as large as the requested size and SHALL exceed it by less than the length of its last
line with its terminator. The output SHALL end with a complete line and its terminator.

#### Scenario: One gigabyte
- **WHEN** a file is generated with `--size 1GB`
- **THEN** its size is at least 1073741824 bytes, and without its last line it would be smaller than 1073741824 bytes

#### Scenario: Smallest size
- **WHEN** a file is generated with `--size 1`
- **THEN** it contains exactly one line

### Requirement: Valid output
Every line of the output SHALL be valid as defined by the `line-format` capability for the line ending that
`--line-ending` sets, for every combination of options. The output SHALL be UTF-8 without a byte order mark.

#### Scenario: Default options
- **WHEN** a file is generated with default options
- **THEN** every line of it is valid, and the file does not begin with a byte order mark

#### Scenario: Every edge case at once
- **WHEN** a file is generated with `--edge-cases all` and `--line-ending crlf`
- **THEN** every line of it is valid for the line ending `\r\n`, and the file does not begin with a byte order mark

### Requirement: Default content
Without `--edge-cases`, every line SHALL be plain: the Number is in canonical form and not greater than 4294967295,
and the String consists of words of ASCII letters separated by single spaces, the first word capitalized and the
others in lower case. With default options, any output of 10 MB or more SHALL contain Numbers of every length from 1
to 10 digits, fully identical lines, and lines that share a String but differ in Number.

#### Scenario: Shape of a default line
- **WHEN** a file is generated with default options
- **THEN** every line matches `(0|[1-9][0-9]{0,9})\. [A-Z][a-z]*( [a-z]+)*` followed by the terminator, and no
  Number is greater than 4294967295

#### Scenario: Repeats in a small file
- **WHEN** a file of 10 MB is generated with default options
- **THEN** Numbers of every length from 1 to 10 digits occur, at least two lines are byte-identical, and at least one
  String occurs with two different Numbers

### Requirement: Repeated Strings
`--duplicates <percent>` (default 30) SHALL set the probability that a line takes its String from a pool of 1024
Strings derived from the seed; `0` turns the pool off. The other lines get freshly composed Strings, which can
coincide by chance, so short Strings repeat more often than the option says.

#### Scenario: Share of pool lines
- **WHEN** a file of 10 MB is generated with `--duplicates 30` and `--words 5`
- **THEN** between 29% and 31% of its lines have a String that also occurs on another line

#### Scenario: Pool turned off
- **WHEN** a file of 10 MB is generated with `--duplicates 0` and `--words 5`
- **THEN** fewer than 1% of its lines have a String that also occurs on another line

#### Scenario: Only repeated Strings
- **WHEN** a file of 10 MB is generated with `--duplicates 100`
- **THEN** it contains at most 1024 distinct Strings

### Requirement: String length in words
`--words <min>-<max>` (default `1-5`) SHALL set how many words a String has: a whole number chosen at random from the
range for each String, unless the `empty` edge case leaves the String empty. A single number means exactly that many
words.

#### Scenario: Range of words
- **WHEN** a file of 1 MB is generated with `--words 2-3`
- **THEN** every String has 2 or 3 words, and both counts occur

#### Scenario: Exact number of words
- **WHEN** a file is generated with `--words 4`
- **THEN** every String has exactly 4 words

### Requirement: Edge cases
`--edge-cases <list>` SHALL add valid but unusual lines to the output. The list names categories, separated by
commas: `unicode`, `separator`, `case`, `big-numbers`, `leading-zeros` and `empty`; `all` enables every one. With
the default `--words` and `--duplicates`, each enabled category SHALL affect at least 1% of the lines of any output
of 1 MB or more. A category that is not enabled SHALL NOT appear in the output.

#### Scenario: Category unicode
- **WHEN** a file of 1 MB is generated with `--edge-cases unicode`
- **THEN** its Strings contain non-ASCII characters that UTF-8 encodes with 2, 3 and 4 bytes. Among them are U+FF21
  and U+1F600, a pair that orders differently by UTF-8 bytes and by UTF-16 code units, and the letter U+0439 in both
  forms, precomposed and decomposed (U+0438 U+0306)

#### Scenario: Category separator
- **WHEN** a file of 1 MB is generated with `--edge-cases separator`
- **THEN** some Strings contain `. `, some begin with digits followed by `. `, and some begin with a space

#### Scenario: Category case
- **WHEN** a file of 1 MB is generated with `--edge-cases case`
- **THEN** dictionary words occur in lower case, capitalized and in upper case, in any position: some Strings begin
  with a lower-case ASCII letter, and some have an upper-case ASCII letter after the first word

#### Scenario: Category big-numbers
- **WHEN** a file of 1 MB is generated with `--edge-cases big-numbers`
- **THEN** Numbers greater than 4294967295 occur, among them 4294967296, 9223372036854775807, 9223372036854775808 and
  18446744073709551615

#### Scenario: Category leading-zeros
- **WHEN** a file of 1 MB is generated with `--edge-cases leading-zeros`
- **THEN** some Numbers have two or more digits and begin with `0`, and no Number has more than 20 digits

#### Scenario: Category empty
- **WHEN** a file of 1 MB is generated with `--edge-cases empty`
- **THEN** some lines end right after the separator

#### Scenario: All categories
- **WHEN** a file of 1 MB is generated with `--edge-cases all`
- **THEN** lines of every category occur, each category on at least 1% of the lines

#### Scenario: Selected categories only
- **WHEN** a file of 1 MB is generated with `--edge-cases empty,leading-zeros`
- **THEN** lines of both categories occur, and no line of any other category occurs

### Requirement: Line ending
`--line-ending` SHALL accept `lf` (the default on every operating system) or `crlf` and SHALL set the line ending of
the output, `\n` or `\r\n`, in the sense of the `line-format` capability. A carriage return SHALL appear in the output
only as part of a `\r\n` terminator, never inside a String, so the line ending of a generated file can be told from
any of its lines.

#### Scenario: Default on Windows
- **WHEN** a file is generated on Windows without `--line-ending`
- **THEN** the output contains no carriage return

#### Scenario: Carriage return and line feed
- **WHEN** a file is generated with `--line-ending crlf`
- **THEN** every line is followed by `\r\n`, no line feed appears without a carriage return before it, and no
  carriage return appears without a line feed after it

### Requirement: Deterministic output
The bytes of the output SHALL depend only on the seed, on `--size`, `--duplicates`, `--words`, `--edge-cases` and
`--line-ending`, and on the content version. They SHALL NOT depend on the number of threads, the write mode, the
output path, the operating system or the .NET runtime version. The content version is a whole number; any change to
the generator that alters the bytes produced for an existing seed and options SHALL increase it.

#### Scenario: Same seed and options twice
- **WHEN** two files are generated with the same seed and the same options
- **THEN** they are byte-identical

#### Scenario: Different thread counts and write modes
- **WHEN** three files are generated with the same seed and options: one with `--threads 1`, one with `--threads 16`
  and one with `--buffered-io`
- **THEN** they are byte-identical

#### Scenario: Different runtime versions
- **WHEN** the same build generates a file on the .NET 10 runtime and on a newer .NET runtime
- **THEN** the two files are byte-identical

#### Scenario: Different seeds
- **WHEN** two files of 1 MB are generated with the same options and different seeds
- **THEN** they differ

#### Scenario: Size only truncates
- **WHEN** two files are generated with the same seed and options except for `--size`
- **THEN** the smaller file is a prefix of the larger one

#### Scenario: Pinned reference files
- **WHEN** two files are generated with seed 1 and size `1MB` on any supported platform, one with default options and
  one with `--duplicates`, `--words`, `--edge-cases` and `--line-ending` all changed from their defaults
- **THEN** their SHA-256 hashes equal the reference hashes recorded in the test suite for the current content version

### Requirement: Seed
`--seed` SHALL set the seed, a whole number. When it is omitted, the generator SHALL choose a random seed. In both
cases it SHALL print the seed and a command line that reproduces the content.

#### Scenario: Seed omitted
- **WHEN** the generator is run twice with `--size 1MB` and without `--seed`
- **THEN** the two outputs differ, and each run prints the seed it used

#### Scenario: Reproducing a file
- **WHEN** the command line printed by an earlier run is run again with another output path
- **THEN** the output is byte-identical to the output of that run

### Requirement: Existing output file
When the output file already exists, the generator SHALL NOT replace it without consent: `--overwrite`, or a
confirmation typed at a prompt. When there is no console to ask, that counts as a refusal. Without consent it SHALL
leave the file untouched and exit with code 1. After consent the existing file is deleted before the new content is
written, so the volume needs no room for both files, and a run that fails or is cancelled after that point leaves no
file at the output path.

#### Scenario: Overwrite option
- **WHEN** the output file exists and `--overwrite` is given
- **THEN** the file is replaced without a prompt

#### Scenario: Confirmed at the prompt
- **WHEN** the output file exists and the user answers `y` at the prompt
- **THEN** the file is replaced

#### Scenario: Declined at the prompt
- **WHEN** the output file exists and the user presses Enter or answers `n` at the prompt
- **THEN** the file is left untouched and the generator exits with code 1

#### Scenario: No console to ask
- **WHEN** the output file exists, standard input is redirected and `--overwrite` is not given
- **THEN** no prompt is shown, the file is left untouched, and the generator exits with code 1 with a message that
  mentions `--overwrite`

### Requirement: No partial output
The output path SHALL never hold a partially written file. The content is written to a temporary file next to the
output, which is moved to the output path only after the whole content is written and is removed when generation
fails or is cancelled. On Ctrl+C the generator SHALL stop within 2 seconds and exit with code 130.

#### Scenario: Successful run
- **WHEN** generation finishes successfully
- **THEN** the exit code is 0, the output file exists and no temporary file is left next to it

#### Scenario: Failure during generation
- **WHEN** an error occurs after generation has started
- **THEN** no file exists at the output path, no temporary file is left, and the generator exits with code 1

#### Scenario: Ctrl+C during generation
- **WHEN** the user presses Ctrl+C while a file is being generated
- **THEN** the generator reports the cancellation and exits with code 130 within 2 seconds, and neither the output
  file nor a temporary file is left

### Requirement: Exit codes and error messages
The generator SHALL exit with 0 on success, 2 on invalid arguments, 130 on cancellation and 1 in every other case
where it produced no file, such as a runtime failure or a declined overwrite. Every non-zero exit SHALL come with a
message on standard error that names the cause. A stack trace SHALL NOT be shown.

#### Scenario: Not enough disk space
- **WHEN** the requested size is larger than the space available on the target volume
- **THEN** the generator exits with code 1 before any content is written, with a message that says there is not
  enough free space

#### Scenario: Unwritable output path
- **WHEN** the output path is a directory or cannot be written
- **THEN** the generator exits with code 1 with a message that names the path and shows no stack trace

### Requirement: Settings before the run
Before generation starts, the generator SHALL print to standard error the settings it works with: every option with
its value, whether or not the user gave it, and the seed, also when the generator chose it. A redirected standard
error still gets them.

#### Scenario: Options that were not given
- **WHEN** the generator is run with only `--output` and `--size`
- **THEN** before any progress it prints to standard error the output path, the requested size, the seed it chose,
  and the values of `--duplicates`, `--words`, `--edge-cases`, `--line-ending`, `--threads`, the write mode,
  `--overwrite` and the content version

#### Scenario: A run that does not finish
- **WHEN** a run is cancelled or fails after generation has started
- **THEN** its settings, the seed included, have already been printed

### Requirement: Progress and summary
The generator SHALL show its progress on standard error unless that stream is redirected; then it SHALL write no
progress updates. On success it SHALL print a summary to standard output: output path, size in bytes, number of
lines, average line length, seed, content version, elapsed time, throughput, peak working set and the command line
that reproduces the output.

#### Scenario: Summary after a successful run
- **WHEN** generation finishes successfully
- **THEN** the summary on standard output contains every listed field

#### Scenario: Redirected error stream
- **WHEN** standard error is redirected to a file
- **THEN** no progress updates are written

### Requirement: Throughput
The generator SHALL produce a 100 GB file in under 5 minutes on the reference machine described in `README.md`.
`--threads <n>` SHALL limit how many threads generate content at the same time; the default is the number of logical
processors, up to the largest value the option accepts. By default the generator SHALL write without the operating
system's file cache where that is supported, which is Windows; `--buffered-io` SHALL make it write through the file
cache instead.

#### Scenario: One hundred gigabytes
- **WHEN** a file is generated with `--size 100GB` and default options on the reference machine
- **THEN** generation finishes in under 5 minutes

### Requirement: Bounded memory
The memory used by the generator SHALL NOT grow with the output size. With `--threads` up to 64 its peak working set
SHALL stay below 512 MB.

#### Scenario: Peak memory for a large file
- **WHEN** a 100 GB file is generated with 16 threads
- **THEN** the peak working set reported in the summary is below 512 MB

#### Scenario: Many threads
- **WHEN** a 10 GB file is generated with `--threads 64`
- **THEN** the peak working set reported in the summary is below 512 MB

#### Scenario: Memory does not follow the size
- **WHEN** a 1 GB file and a 100 GB file are generated with the same options
- **THEN** their peak working sets differ by less than 64 MB

### Requirement: Standalone executable
The generator SHALL be publishable as a single self-contained executable that runs on a machine without .NET
installed and produces the same bytes as the regular build. Publishing SHALL NOT report trimming, single-file or AOT
analysis warnings, and a check publish with trimming enabled SHALL NOT report trim warnings for the application or
its packages. The shipped executable stays untrimmed.

#### Scenario: Publishing for Windows
- **WHEN** `dotnet publish FileSort.Generator -c Release -r win-x64` is run
- **THEN** it produces one executable file and reports none of those warnings

#### Scenario: Trim analysis of the whole application
- **WHEN** the generator is published once more with `PublishTrimmed`, only as a check
- **THEN** the trimmer, which also analyses the referenced packages, reports no warnings

#### Scenario: Same bytes as the regular build
- **WHEN** the published executable and the regular build generate a file with the same seed and options
- **THEN** the two files have the same SHA-256 hash
