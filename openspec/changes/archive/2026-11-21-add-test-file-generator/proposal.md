# Proposal

## Why

The sorter cannot be built, tested or measured without input files, and the task itself asks for a utility that
creates a test file of a given size with repeated String parts. Today `FileSort.Generator` is an empty console app and
the line format exists only as a README table with several *TBD* cells. This change goes first because every later
change (checker, sorter, report) consumes files in this format, and the open format questions have to be settled
before any reader is written.

## What Changes

- Define the `Number. String` line format as a spec: what a valid line is, including the cases the README still lists
  as *TBD* (Number range, leading zeros, empty String).
- Add the shared line format code to `FileSort.Common`: the format constants and the line ending. Writing a line
  stays in the generator, the only program that composes lines.
- Implement the generator in `FileSort.Generator`:
  - output of a requested size (`--size 1GB`, binary units) that ends on a line boundary;
  - deterministic content: the same seed and content options give a byte-identical file on any OS, with any thread
    count and on any .NET runtime; a seed is chosen and printed when none is given;
  - content built from a built-in dictionary, with a configurable share of lines whose String comes from a pool of
    repeated Strings (`--duplicates`), a configurable String length in words (`--words`) and Numbers of every
    length, so that fully identical lines and equal Strings with different Numbers both occur;
  - opt-in categories of edge cases (`--edge-cases`); without them the output is plain ASCII that every reasonable
    comparer orders the same way;
  - parallel generation with one ordered writer, unbuffered writes on Windows, and memory use that does not depend
    on the output size;
  - a command-line interface with `--help`, validation errors, distinct exit codes, a confirmation before an
    existing file is replaced, and removal of the unfinished file on Ctrl+C or failure.
- Add the `FileSort.Benchmarks` project: BenchmarkDotNet micro-benchmarks for the generator's hot path and a raw
  write-path experiment that measures what the disk can take.
- Add the first tests: line format, PRNG reference vectors, determinism, size, validity of every line, repeats.
- Update `README.md` (Repository layout, Assumptions, Generator, Quick start, Performance, Design decisions,
  Limitations), add `FileSort.Generator/README.md` with the generator's options, measurements, design decisions and
  limitations, and update the command list in `CLAUDE.md`.

## Capabilities

### New Capabilities

- `line-format`: the `Number. String` line and file format shared by all programs: what makes a line valid and how a
  line is written.
- `test-file-generator`: behavior of the generator: size, determinism, content, edge cases, command-line interface,
  safety of the output file, throughput and memory.

### Modified Capabilities

None. `openspec/specs/` is empty; these are the first two capabilities.

## Assumptions

The task statement leaves these open. Each one is a decision of this change and is open to review.

- **Number** is an unsigned integer from 0 to 18446744073709551615 (the `UInt64` maximum), written with 1 to 20
  decimal digits. Leading zeros are valid and do not change the value (`007` is 7). No sign, no spaces.
- **String** may be empty, may contain `. ` and is valid UTF-8 without a line feed. The first `. ` in a line is the
  separator.
- **Line ending**: a file is read with one line ending for all its lines, `\n` or `\r\n`. A carriage return that is
  not part of that terminator gets no special treatment: in a String it is one of its bytes, also at its end.
- **Encoding** is UTF-8. A file may start with a byte order mark, which is not part of the first line; the generator
  never writes one.
- **Sizes** use binary units: `1GB` is 1,073,741,824 bytes. A fraction is accepted (`1.5GB`) and the result is
  rounded up to a whole byte. The output is at least the requested size and exceeds it by less than one line.
- **"Reasonable time"** for 100 GB means under 5 minutes on the reference machine described in the README. The design
  goal is stricter: throughput limited by the disk, not by the generator.
- **Generated files are disposable**: they can be regenerated from the seed. So when the user confirms replacing an
  existing file, it is deleted before generation starts, and an interrupted run leaves no file at that path.
- **Platform**: the code is cross-platform, but it is developed and measured on Windows only. Unbuffered writes are
  used on Windows; other systems use regular buffered writes.

## Out of scope

- **Parsing a line** in `FileSort.Common`. The sorter is the only consumer and its parser is a hot path whose shape
  depends on the sorter design; it is written and measured in `add-external-sorter`. Tests in this change validate
  generated lines with their own simple parser.
- **What the sorter does with malformed lines**, and **the order of lines whose keys are equal but whose bytes
  differ** (`007. Apple` and `7. Apple`). Both are decided in `add-external-sorter`.
- **How the sorter and the checker learn the line ending of a file** (an option and its default). Each is decided in
  the change of that program; the generator only writes, and `--line-ending` sets what it writes.
- **Generating malformed files** (deliberately invalid lines). Postponed until the sorter's policy for malformed
  lines exists, because that policy defines what such a flag should produce.
- **A user-supplied dictionary** (`--dictionary <file>`). The built-in dictionary is enough for now.
- The sorter, the checker and the HTML report.
- Measurements on Linux and macOS, and direct I/O (`O_DIRECT`) on those systems.

## Impact

- **Code**: `FileSort.Common` gets its first code; `FileSort.Generator` replaces the placeholder `Program.cs`;
  `FileSort.Tests` gets its first tests, so `dotnet test` no longer exits with code 8; the new `FileSort.Benchmarks`
  project joins `FileSort.slnx`.
- **Dependencies**: `System.CommandLine` 2.0.15 in the generator. It is a Microsoft package, but it is the first
  NuGet dependency of production code, so a check publish with trimming verifies that the whole application, the
  package included, has no trim warnings. `BenchmarkDotNet` 0.15.8 in the benchmarks project only.
- **Documentation**: `README.md`, `FileSort.Generator/README.md` and `CLAUDE.md`.
- **Later changes**: the checker and the sorter rely on `line-format`; the sorter change adds the parser and the
  requirements for reading.
