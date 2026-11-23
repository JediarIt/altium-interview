# Proposal

## Why

The sorter's first priority is correctness, and at 100 GB nobody can look at the output and see that it is right.
The checker is the tool that says so: it confirms that an output file is the sorted version of an input file. It
goes before the sorter so that the sorter is developed against an oracle that already exists, was written without
knowing the sorter's code and has its own tests. Today `FileSort.Checker` is a placeholder that prints one line.

## What Changes

- Implement the checker in `FileSort.Checker`. For a pair of files, the input and the sorter's output, it
  establishes three things:
  - every line of both files is valid as `line-format` defines;
  - the lines of the output are in the order of the task: by String, then by Number;
  - the output has exactly the lines of the input: the same number of lines and the same order-independent hash of
    them.
- Define that order as a spec of its own, `sort-order`, because two programs rely on it: the sorter writes in it
  and the checker verifies it.
- Lift the limit on the length of a Number in `line-format`. A Number is one or more digits of any count and value.
  The author decided, when the generator was reviewed, that the limit of 20 digits and 64 bits goes (the sorter
  gets a fast path for Numbers that fit in 64 bits); the checker is the first program that reads lines, and it should not implement a rule that is about to be
  removed.
- Give the checker a command line in the style of the generator: `--help`, validation errors, settings printed
  before the check, progress, a summary, distinct exit codes, cancellation with Ctrl+C. The checker only reads
  files.
- Add a small shared library, `FileSort.Cli`, with what the console programs have in common: the exit codes, the
  console abstraction, the progress line, the handling of Ctrl+C and of command-line errors. The generator moves to
  it; its behavior does not change.
- Add tests: the scenarios of the specs, pairs made by the generator and sorted by a naive in-memory sort, and a
  catalogue of ways to break a correct output, each of which the checker must reject.
- Add benchmarks and two experiments to `FileSort.Benchmarks`, and measure the checker on files of 1 GB and
  smaller.
- Split continuous integration by program: a workflow for the generator and one for the checker, each started by
  the changes that can affect it. Publishing gets the checker as a second program.
- Update `README.md` and `FileSort.Generator/README.md`, add `FileSort.Checker/README.md`, and update
  `CLAUDE.md`.

## Capabilities

### New Capabilities

- `sort-order`: the order of lines that the sorter produces and the checker verifies: Strings by their UTF-8 bytes,
  then Numbers by value, and what holds for lines whose keys are equal.
- `output-checker`: behavior of the checker: what makes an output correct, how line endings and the byte order mark
  are handled, what is reported, the command line, exit codes, speed and memory.

### Modified Capabilities

- `line-format`: the requirement "Number" loses its limit of 20 digits and of the 64-bit range; the requirement
  "Canonical form of a written line" has one scenario reworded, because there is no largest Number any more.

## Assumptions

Each one is a decision of this change and is open to review.

- **The output is a permutation of the input, byte for byte.** A line of the output is a line of the input with the
  same bytes. A sorter that rewrites a line, for example `007. Apple` as `7. Apple`, produces a wrong output.
- **The output has the line ending of the input.** The checker reads both files with one line ending. This fixes,
  ahead of the sorter, that the sorter writes with the line ending of its input.
- **Lines with equal keys but different bytes** (`007. Apple` and `7. Apple`) are accepted in any order. The sorter
  change decides their order; the checker counts such pairs so that they are visible.
- **A byte order mark** at the start of either file is not part of a line and does not affect the result. Whether
  the sorter keeps the mark of its input is decided with the sorter.
- **Both files must be valid.** A line that is not valid stops the check, whichever file it is in. What the sorter
  does with a malformed input is decided with the sorter, and the checker is revisited then.
- **The checker guards against mistakes, not against forgery.** The hash is not cryptographic: an output crafted on
  purpose to pass is outside its scope.
- **Speed**: checking is not part of the acceptance targets of the task. This change sets a target of its own: a
  pair of 100 GB files in about five minutes, which the spec states as a rate measured on files of 1 GB, with memory
  that does not depend on the size of the files.
- **Platform**: the code is cross-platform; it is developed and measured on Windows only.

## Out of scope

- **The HTML report** built from the sorter's logs. It is the change `add-sorter-report`, after the sorter.
- **The sorter**, its policy for malformed lines, the order of lines with equal keys but different bytes, and
  whether its output keeps a byte order mark.
- **A generator edge case with Numbers above 64 bits.** It changes the bytes of generated files (a new content
  version) and belongs to the sorter change, which needs such files. This change tests long Numbers with lines
  written in the tests.
- **A check of the 100 GB case.** No sorted file of that size exists before the sorter. The checker is measured on
  files of 1 GB and smaller here, and at 100 GB once, together with the sorter.
- **Checking one file alone** (only the order, or only the validity). Both files are required, and naming the same
  file twice is rejected.
- **Telling which lines differ.** The checker says that the sets of lines differ and gives the number of lines of
  each file, not which line is missing or changed.
- **A Linux job in CI** and measurements on Linux and macOS.

## Impact

- **Code**: `FileSort.Checker` replaces its placeholder; the new library `FileSort.Cli` joins `FileSort.slnx`;
  `FileSort.Generator` loses the classes that move there; `FileSort.Common` changes only in the comments of two
  constants; `FileSort.Tests` gets the tests of the checker and of the
  library, and its naive parser accepts Numbers of any length; `FileSort.Benchmarks` gets new benchmarks and
  experiments. `FileSort.Checker` still does not reference `FileSort.Common`.
- **Dependencies**: `System.IO.Hashing` (a Microsoft package, for the hash of a line) and `System.CommandLine` in
  the checker; `System.CommandLine` in `FileSort.Cli`. The checker is published as a self-contained single file, so
  both packages go through the check publish with trimming, as the generator's package does.
- **Specs**: two new capabilities and a modified `line-format`. The generator's spec does not change: the generator
  still writes Numbers of at most 20 digits.
- **CI**: `ci.yml` is replaced by one workflow per program on top of a shared one; `publish.yml` publishes either
  program.
- **Documentation**: `README.md`, `FileSort.Generator/README.md`, the new `FileSort.Checker/README.md` and
  `CLAUDE.md`.
- **Later changes**: the sorter relies on `sort-order` and on the exit codes of `FileSort.Cli`, and its tests use
  the checker.
