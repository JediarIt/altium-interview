# Tasks

Measurement ids (M1 to M6) and decision ids (D1 to D16) refer to `design.md`; names in quotes are requirements and
scenarios of the specs of this change. A task says what to check and points to where the values are; it does not
repeat them. Large files go to the ignored `data/` directory and are deleted after each measurement.

## 1. Setup

- [ ] 1.1 Add `System.CommandLine` 2.0.15 and `BenchmarkDotNet` 0.15.8 to `Directory.Packages.props` and reference
  `System.CommandLine` from `FileSort.Generator`. Verify: `dotnet build FileSort.slnx -c Release` succeeds with no
  warnings.
- [ ] 1.2 Create the `FileSort.Benchmarks` console project (references `FileSort.Generator`, `FileSort.Common` and
  `BenchmarkDotNet`), add it to `FileSort.slnx`, make the internals of `FileSort.Generator` visible to
  `FileSort.Tests` and `FileSort.Benchmarks`, and add the project to the Repository layout table in `README.md`.
  Verify: the solution builds and `dotnet run -c Release --project FileSort.Benchmarks` prints its usage.

## 2. Line format

- [ ] 2.1 Add the format constants, the line ending choice and the line writer to `FileSort.Common` (D1), with unit
  tests for every scenario of "Canonical form of a written line" plus zero-padded Numbers and the `\r\n` terminator.
  Verify: the tests pass.
- [ ] 2.2 Add the naive parser to `FileSort.Tests` as a test-only oracle that uses nothing from `FileSort.Common`:
  it is given the line ending of the file, skips a byte order mark at the start, splits the file into lines at that
  terminator and parses each line. Add a test for every scenario of the `line-format` spec, valid and invalid.
  Verify: the tests pass.
- [ ] 2.3 Update the Assumptions table in `README.md` from the `line-format` spec: Number range and leading zeros,
  empty String, the Line endings row (one line ending per file, and what a carriage return is elsewhere), the
  Encoding row, and the row about duplicate lines (lines with equal keys can differ in leading zeros; their order is
  decided with the sorter). Verify: the rows for Number comparison and Empty String part have no *TBD*, every row
  agrees with `specs/line-format/spec.md`, and the only *TBD* left in the Line endings row is the line ending of the
  sorter's output, which `add-external-sorter` decides.

## 3. Random numbers

- [ ] 3.1 Implement SplitMix64, xoshiro256** and the seeding of a stream from the seed and a stream id (D3). Verify:
  known-answer tests with the values listed in D3 pass, and tests show that different seeds and different stream ids
  give different sequences.

## 4. Content

- [ ] 4.1 Add the built-in dictionary and the non-ASCII word list as static UTF-8 data (D5, D8). Verify: tests
  confirm what D5 states about the dictionary (the number of words, the alphabet, the longest word, the words of the
  task's examples) and what D8 states about the non-ASCII list (no ASCII character, the byte lengths, the named
  characters), and that both are well-formed UTF-8.
- [ ] 4.2 Implement the content options and the composition of a String and of a Number directly into a span (D5,
  D6). Verify: tests for the scenarios of "String length in words" and for the scenario "Shape of a default line"
  pass, using the naive parser, and a test confirms the shares of lines by Number length given in D6.
- [ ] 4.3 Implement the repeat pool and the `--duplicates` decision (D7). Verify: tests for the scenarios of
  "Repeated Strings" and for the scenario "Repeats in a small file" pass.
- [ ] 4.4 Implement the edge case categories (D8). Verify: tests for the scenarios of "Edge cases" pass; tests
  confirm the two rules of D8 that the spec leaves out (the number of words stays, and `empty` wins over the other
  String categories).
- [ ] 4.5 Implement the block generator: whole lines for given options and a block index, with the byte and line
  counts (D4). Verify: tests show that the same inputs give the same bytes, different block indexes give different
  bytes, a block never exceeds its capacity and ends with a terminator, and no String contains a carriage return
  with either line ending (D2); and tests for the scenarios of "Valid output" and "Line ending" pass.
- [ ] 4.6 Add the M1 benchmarks, keep the faster variants, and record MiB/s per core in the Performance section of
  `README.md`. Verify: the BenchmarkDotNet report shows 0 bytes allocated per block, and the README numbers match
  the report.

## 5. Pipeline

- [ ] 5.1 Implement the sink interface with the in-memory hashing sink and the discarding sink, and the pipeline: a
  bounded window of block tasks consumed in order, the cut at the first line boundary at or after the size, line
  counting and cancellation (D10). Verify: tests with the hashing sink pass for "Output size" (sizes 1, 4095, 4096,
  4097, 1 MiB ± 1, 8 MiB ± 1 and 20 MiB), for the scenarios "Same seed and options twice", "Different seeds" and
  "Size only truncates", for thread counts 1, 2 and 16 giving the same bytes, and for cancellation stopping the
  pipeline within the time that "No partial output" allows.

## 6. Output file

- [ ] 6.1 Implement the aligned buffer and the file sink (D11, D12): the temporary file, preallocation, both write
  modes, the padded last chunk and the exact final length, commit by rename and abort by delete. Verify: tests show
  that the file equals the hashing sink's content in both write modes for sizes around 4096 bytes and around the
  chunk size; and tests at the level of the sink cover the rules of D12: a committed write leaves only the output,
  an aborted one leaves neither file, a stale temporary file is replaced, missing directories are created, and a
  size above the free space fails before any content is written (exit codes and messages are checked in 7.2).
- [ ] 6.2 Add the `write-path` experiment to `FileSort.Benchmarks` and run M3; set the chunk size from the result;
  record the table and the conclusion in `README.md` together with the command that reproduces it. Verify: the
  documented command runs as written and the README table matches its output.
- [ ] 6.3 Run M4 and apply its decision rule (D11). Record the ratio and the decision in `README.md`. Verify: the
  recorded ratio is at or above the threshold of M4, or the README states why it is not.
- [ ] 6.4 Run M2, freeze the block capacity and record the M2 table and the choice in the Performance section of
  `README.md`. Then set the content version to 1 and record, for that version, the SHA-256 of the two reference
  files described in D4. Verify: the README table matches the measured output, and the scenarios "Pinned reference
  files" and "Different thread counts and write modes" pass.

## 7. Command line

- [ ] 7.1 Implement the value parsers and range checks for the options of the D13 table: size with units and
  fractions, word range, edge case list, line ending, and the whole numbers of `--seed`, `--duplicates` and
  `--threads`. Verify: tests show that each one accepts the values of the D13 table and of the scenario "Units and
  fractions" and rejects values outside them, including a zero size, a decimal comma and an exponent (the exit code
  is checked in 7.2).
- [ ] 7.2 Replace the placeholder `Program.cs` with the command on `System.CommandLine`: every option of D13,
  `--help`, the exit codes, error messages without a stack trace, and the small console abstraction (D13). Verify:
  tests for the scenarios of "Command line" and "Exit codes and error messages" pass, and a test confirms that
  `--help` shows the defaults.
- [ ] 7.3 Implement the handling of an existing output file (D12): `--overwrite`, the prompt, the refusal without a
  console, the free-space check, and deleting the old file before generation. Verify: tests for the scenarios of
  "Existing output file" pass, tests cover the answers and the console rule of D12, and a unit test of the
  free-space decision covers both outcomes: it fits only because the old files count as available, and it does not
  fit even then.
- [ ] 7.4 Implement the random seed when `--seed` is omitted, the progress line and the summary with the reproducing
  command line (D13). Verify: tests for the scenarios of "Seed" and "Progress and summary" pass.
- [ ] 7.5 Wire Ctrl+C and the termination signal to the cancellation token and to the exit code for cancellation.
  Verify: an in-process test cancels a run and finds that exit code and neither the output nor the temporary file;
  and one manual check of the scenario "Ctrl+C during generation": start `--size 100GB`, which runs long enough to
  press the key, press Ctrl+C, look at the time it takes, at `$LASTEXITCODE` and at the directory.
- [ ] 7.6 Update `README.md`: Quick start (commands that generate a 1 GB file with `dotnet run` and with the
  published executable), the Generator section (what the options are for, shown by examples; what makes a file
  reproducible; what `--duplicates` controls; edge cases; existing file and temporary file; exit codes), Design
  decisions (own PRNG, dictionary against random letters and real text, Number distribution, `System.CommandLine`:
  the reasons, with only those numbers that a committed benchmark or experiment reproduces) and Limitations (no
  malformed files, no custom dictionary, unbuffered writes and measurements on Windows only). The Generator section
  also has a table of the options, typed by hand: each option with what it does, the values it accepts and its
  default (D13). In `CLAUDE.md`, add the generator and benchmark commands and remove the note that `dotnet test`
  exits with code 8 while there are no tests. Verify: every command in Quick start runs as written and gives the
  described result; the table has the same options, accepted values and defaults as the output of `--help`; and
  `CLAUDE.md` has no statement that this change made false.
- [ ] 7.7 Add the test of the options table in `README.md` (D13): link `README.md` into the output of
  `FileSort.Tests`, read the table and compare its option names and its fixed defaults with the command. Verify: the
  test passes; it fails with a row removed from the table and it fails with a default changed in the table (try
  both, then restore the table).

## 8. Publish and end-to-end checks

- [ ] 8.1 Run `dotnet publish FileSort.Generator -c Release -r win-x64 -o publish/generator`. Verify: the scenarios
  of "Standalone executable" hold, where the file that the published executable generates with default options is
  compared against the pinned reference hash of that file, and the check publish is run with
  `-p:PublishTrimmed=true -o publish/trim-check`; and the scenario "Different runtime versions" holds: the regular
  build run on the .NET 11 runtime (`DOTNET_ROLL_FORWARD=LatestMajor`) produces the same hash.
- [ ] 8.2 Run M5 with the published executable; record wall time, MiB/s and peak working set in the Performance
  section of `README.md`, which calls the machine the reference machine. Verify: the scenarios of "Throughput" and
  "Bounded memory" hold.
- [ ] 8.3 Run M6; record the table in `README.md` and state at which thread count the disk becomes the limit.
  Verify: the README table matches the measured output.
- [ ] 8.4 Final check of the whole change. Verify: `dotnet build FileSort.slnx -c Release` and
  `dotnet test --solution FileSort.slnx -c Release` pass with no warnings; `openspec validate
  add-test-file-generator` passes; the README sections Assumptions, Generator, Quick start, Performance, Repository
  layout, Design decisions and Limitations contain no stale statement about the generator; the `data/` directory
  holds no file left from the measurements (it is ignored by git, so `git status` would not show one); and
  `git status --short` lists only files that belong to this change.

## Workflow follow-up

- The author reviews the change; it is committed only on the author's command, as one commit.
- Archive the change with `/openspec-archive-change` after the review.
