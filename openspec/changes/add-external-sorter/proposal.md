# Proposal

## Why

The sorter is the program the task is about, and today `FileSort.Sorter` is a placeholder that prints one line. The
generator makes test files and the checker verifies a sorted file against its input; both exist and are measured.
This change adds the sorter itself: the whole external merge sort, correct for inputs far larger than memory and
within the memory budget, so that a 1 GB file sorts in under a minute on the reference machine with the memory of
the task.

The author split the sorter into two changes. This one builds the complete algorithm and meets both targets of the
task on the reference machine. A second change adds the margin: tuning from measurements, a margin for unknown
hardware, and the one 100 GB run.

## What Changes

- Add the sorting engine, a class library with no console dependencies: it reads the input in large chunks, sorts
  each chunk in memory on all cores and writes it as a sorted run, then merges the runs into the output, in several
  passes when there are too many for one. An input that fits in one chunk is written to the output directly.
- Replace the placeholder `FileSort.Sorter` with a thin console host over the engine, in the style of the generator
  and the checker: `--help`, errors that name their option, settings printed before the run, progress, a summary
  with the time, the throughput and the peak working set, the shared exit codes, cancellation with Ctrl+C that
  removes every temporary file. The progress is drawn with Spectre.Console.
- Define what the sorter promises as a new spec, `external-sorter`: the output is the input's lines in the order of
  `sort-order`, byte for byte; lines with equal keys (the same String and the same value of the Number) come in the
  order of their bytes, so the output depends only on the input and its line ending; the line ending of the input is
  kept; an input that is not valid is reported, no output is written and no temporary file is left; the memory given
  in `--memory` is the limit of the whole process.
- Teach the generator the lines that the sorter must handle and that it cannot write yet: Numbers above 64 bits and
  Strings that end with a carriage return. This is a new content version of the generator.
- Move into `FileSort.Cli` the pieces of the command line that the sorter shares with the other programs: the
  parsing of sizes and of whole numbers, and the question before an existing output is replaced. The generator and
  the checker behave as before.
- Add tests: the scenarios of the specs, sorts with tiny memory budgets that force many runs and several merge
  passes, and comparison with a naive in-memory sort byte for byte, whatever the number of threads and the memory.
- Add benchmarks and experiments to `FileSort.Benchmarks`, and measure the sorter on files of 1 GB and smaller,
  plus one run of a few gigabytes.
- Add a workflow for the sorter to CI, and update `README.md`, the READMEs of the generator and the checker,
  `CLAUDE.md`, and a new `FileSort.Sorter/README.md`.

## Capabilities

### New Capabilities

- `external-sorter`: behavior of the sorter: what the output holds and in which order, line endings and the byte
  order mark, malformed input, temporary files and the output file, the command line, exit codes, memory, speed.

### Modified Capabilities

- `test-file-generator`: the requirement "Edge cases" gets Numbers above 64 bits and a new category of Strings that
  end with a carriage return; the requirement "Line ending" allows that carriage return inside a String and keeps
  the line ending of a file visible from its first line.

## Assumptions

Each one is a decision of this change and is open to review.

- **The output is the input's lines, byte for byte**, each followed by the input's line terminator, the last one
  too. No line is rewritten: `007. Apple` stays `007. Apple`.
- **Lines with equal keys and different bytes are written in the order of their bytes**: `007. Apple` before
  `7. Apple`. The output is then a function of the input and its line ending alone, not of the number of threads,
  the memory or where the chunks happened to end. The checker keeps accepting any order of such lines.
- **A malformed input is an error, not something to repair or skip.** The sorter stops at the first line that is
  not valid, names it and writes no output.
- **A line has a length limit.** A line longer than the limit ends the sort with an error. The limit is far above
  any real line of this format, and it lets the sorter keep its memory promise for every file it accepts. The
  checker has a higher limit of its own, for a different reason.
- **A byte order mark** at the start of the input is not part of a line and is not written to the output.
- **`--memory` is the limit of the whole process**, the peak working set, not of the sorter's buffers alone. The
  default leaves half of the task's 2 GB unused.
- **Temporary files go next to the output by default**, in a folder of the sorter's own, not into the system's
  temporary directory, which is often on a small system disk. A sort needs free space of about twice its input.
- **Speed**: this change meets the task's bar on the reference machine. The margin for slower machines, and the
  speed of the 100 GB case, belong to the second change.
- **Platform**: the code is cross-platform; it is developed and measured on Windows only.

## Out of scope

- **The second sorter change**: overlapping reading and writing with sorting, a parallel final merge, unbuffered
  I/O, software prefetch, the mode of the garbage collector, .NET 11, tiered compilation, the 100 GB run and a time
  requirement for it. The checker's measurement at 100 GB, which needs the sorter's 100 GB output, goes with that run.
- **The HTML report** built from the sorter's logs. It is the change `add-sorter-report`.
- **A checker that verifies the order of lines with equal keys.** The checker accepts any order of such lines today
  and keeps doing so; a stricter mode can follow.
- **A standalone executable of the sorter.** The sorter runs on the regular .NET runtime, as decided when the
  solution was laid out.
- **A Linux job in CI** and measurements on Linux and macOS.
- **The manual testing kit** with small files for every kind of error.

## Impact

- **Code**: the new class library `FileSort.Engine` joins `FileSort.slnx`; `FileSort.Sorter` replaces its
  placeholder; `FileSort.Cli` gets the shared parsers, which leave the generator and the checker, and the overwrite
  question, which leaves the generator;
  the generator gets the new edge cases and a new content version, and its plain content keeps its bytes;
  `FileSort.Common` changes only in the comment of the constant that gives the digits of the 64-bit range;
  `FileSort.Tests` gets the tests of the engine and the sorter, and its naive sort orders lines with equal keys by
  their bytes; `FileSort.Benchmarks` gets benchmarks and experiments of the sorter. The engine references
  `FileSort.Common` only; the checker still shares no line code with anything.
- **Dependencies**: Spectre.Console in `FileSort.Sorter` only. The generator and the checker, which are trim-checked
  single files, do not get it.
- **Specs**: one new capability and a modified `test-file-generator`. `line-format`, `sort-order` and
  `output-checker` do not change.
- **CI**: a new `sorter.yml` that calls `test-program.yml`; the path filters of the other two workflows are updated.
- **Documentation**: `README.md`, `FileSort.Generator/README.md`, `FileSort.Checker/README.md`, the new
  `FileSort.Sorter/README.md` and `CLAUDE.md`.
- **Later changes**: the second sorter change builds on the engine and its measurements; `add-sorter-report` reads
  what the sorter reports.
