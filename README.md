# FileSort

External sorting of very large text files (~100 GB and more) in C# / .NET 10, plus a test file generator and an
independent checker.

> **Status: work in progress.** The generator and the checker are implemented and measured; the sorter comes next.
> Sections marked *TBD* get filled in as each part is implemented and measured.

## Contents

- [Quick start](#quick-start)
- [The problem](#the-problem)
- [Build and run from source](#build-and-run-from-source)
- [Repository layout](#repository-layout)
- [Assumptions](#assumptions)
- [How it works](#how-it-works)
- [Verifying correctness](#verifying-correctness)
- [Performance](#performance)
- [Design decisions](#design-decisions)
- [Development process](#development-process)
- [Limitations and next steps](#limitations-and-next-steps)

## Quick start

Needs the [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) or newer and about 3 GB of free disk
space. In PowerShell, from the root of the repository:

```powershell
# 1. Build the generator and the checker as executables in publish/
dotnet publish FileSort.Generator -c Release -r win-x64 -o publish
dotnet publish FileSort.Checker -c Release -r win-x64 -o publish

# 2. Generate a 1 GB test file
publish/FileSort.Generator.exe --output data/test.txt --size 1GB

# 3. Sort it. Until the sorter exists, this makes a sorted copy in memory (files of up to 2 GB)
dotnet run -c Release --project FileSort.Benchmarks -- sorted-copy --input data/test.txt --output data/sorted.txt

# 4. Check the result: CORRECT and exit code 0 mean that the sorted file is right
publish/FileSort.Checker.exe --input data/test.txt --sorted data/sorted.txt
```

The executables run without .NET, so they can be copied to another machine. Every program lists its options with
`--help`. Other platforms, tests and running from source: [Build and run from source](#build-and-run-from-source).

## The problem

Each line of the input file has the form `Number. String`:

```
415. Apple
30432. Something something something
1. Apple
32. Cherry is the best
2. Banana is yellow
```

The output contains the same lines sorted by the **String** part first and, when Strings are equal, by the
**Number**:

```
1. Apple
415. Apple
2. Banana is yellow
32. Cherry is the best
30432. Something something something
```

Targets:

| Criterion | Target |
|---|---|
| Correctness | Output is sorted and contains exactly the input lines |
| Time | 1 GB file sorted in under 1 minute |
| Memory | About 2 GB or less while sorting |
| Scale | ~100 GB input, which does not fit in RAM |

The solution has three programs:

1. **Generator**: writes a test file of a given size in this format, with repeated String parts.
2. **Sorter**: sorts a file of arbitrary size within a bounded memory budget.
3. **Checker**: confirms that a sorted file is correct with respect to its input.

## Build and run from source

Requirements: [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) or newer (a .NET 11 SDK works too).
The programs target .NET 10, so running them needs the .NET 10 runtime: it comes with the .NET 10 SDK, and with a
newer SDK it is a separate install. The code is cross-platform; it is developed and measured on Windows.

Build and run the tests:

```powershell
dotnet build FileSort.slnx -c Release
dotnet test --solution FileSort.slnx -c Release
```

Generate a 1 GB test file (the options are in the [generator's README](FileSort.Generator/README.md#options)):

```powershell
dotnet run -c Release --project FileSort.Generator -- --output data/test-1gb.txt --size 1GB
```

The run prints the seed and the command line that writes the same file again. The `data/` directory is ignored by
git.

Sort the file: *TBD, the sorter is not implemented yet.*

Check that a sorted file is the sorted version of its input (the options are in the
[checker's README](FileSort.Checker/README.md#options)):

```powershell
dotnet run -c Release --project FileSort.Checker -- --input data/test-1gb.txt --sorted data/sorted-1gb.txt
```

The exit code is 0 when the sorted file is correct, 4 when it is not and 3 when a file is not valid; the report
says what was found. Until the sorter exists, a sorted file for trying the checker out can be made with
`dotnet run -c Release --project FileSort.Benchmarks -- sorted-copy --input data/test-1gb.txt --output data/sorted-1gb.txt`
(an in-memory sort for files of up to 2 GB).

The generator and the checker can also be published as single executables that run on a machine without .NET
installed (for example a QA machine), as in the [Quick start](#quick-start). For other platforms, use `linux-x64`,
`osx-arm64` and so on in place of `win-x64`; any machine with the .NET SDK can publish for any of them. The
published programs take the same options as `dotnet run`.

Disk space: sorting a file needs roughly 3x its size free (input, temporary runs and output). For the 100 GB
scenario, plan for about 300 GB.

## Repository layout

| Path | Purpose |
|---|---|
| `FileSort.Common/` | Class library: the line format shared by Generator and Sorter (format constants and the line ending) |
| `FileSort.Cli/` | Class library: the console plumbing shared by the programs (exit codes, the console abstraction, the progress line, Ctrl+C, command-line errors) |
| `FileSort.Generator/` | Console app: test file generator; its [README](FileSort.Generator/README.md) has the options, the measurements and the design decisions |
| `FileSort.Sorter/` | Console app: the external sorter |
| `FileSort.Checker/` | Console app: verifies a sorted output against its input; its [README](FileSort.Checker/README.md) has the options, what is checked, the measurements and the design decisions |
| `FileSort.Tests/` | Unit and integration tests |
| `FileSort.Benchmarks/` | Console app: BenchmarkDotNet micro-benchmarks and the experiments behind the numbers in [Performance](#performance) |
| `openspec/` | Requirements (`specs/`) and the history of change proposals with their designs (`changes/`), see [Development process](#development-process) |
| `CLAUDE.md` | Working rules for the AI assistant, see [Development process](#development-process) |
| `.claude/skills/`, `.agents/skills/` | OpenSpec workflow definitions for the AI assistant (generated) |
| `Directory.Build.props` | Settings shared by all projects: `net10.0`, nullable reference types, analyzers, warnings as errors |
| `Directory.Packages.props` | Central package versions |
| `nuget.config` | Restores from nuget.org only, so the build does not depend on machine-level feeds |
| `global.json` | Requires a .NET SDK 10.0.100 or newer (release versions only); runs tests on Microsoft.Testing.Platform |
| `.github/` | GitHub Actions: build and tests with coverage, a workflow per program, and publishing the generator or the checker by hand, see [Development process](#development-process) |

`FileSort.Checker` intentionally does not reference `FileSort.Common`. It parses and compares lines with its own
deliberately simple code, so a bug in the optimized sorting code cannot be confirmed by that same code.

## Assumptions

The task leaves several details open. The table shows how each one is resolved; items marked *TBD* are decided
together with the implementation. The line format and the order of lines are specified, with acceptance scenarios,
in [openspec/specs/line-format](openspec/specs/line-format/spec.md) and
[openspec/specs/sort-order](openspec/specs/sort-order/spec.md).

| Topic | Decision |
|---|---|
| String comparison | Ordinal, byte by byte over UTF-8, which equals Unicode code point order. Case-sensitive: `Banana` sorts before `apple`. |
| Number comparison | Numeric, not textual: `2` comes before `10`. A Number is one or more decimal digits; neither the count of digits nor the value is limited, so `18446744073709551616` (one above the 64-bit range) is a valid Number. Leading zeros are valid and do not change the value: `007` is 7. No sign, no spaces. The Number takes part only when the Strings are equal. |
| Separator | The first `". "` in a line. The String part may itself contain `". "`. |
| Duplicate lines | Kept: the output has every line of the input, byte for byte, as many times as the input has it. Lines with equal keys are either identical or differ only in the leading zeros of their Numbers: `007. Apple` and `7. Apple`. A file counts as sorted whichever of such lines comes first; the order that the sorter gives them is decided together with the sorter. |
| Line endings | A file has one line ending for all its lines, `\n` or `\r\n`, and the program that reads it works with that one. The last line may lack it. A carriage return that is not part of the line ending is an ordinary byte of the String, also at its end. The sorter's output has the line ending of its input: the checker reads both files with one line ending, taken from the input unless it is given. |
| Encoding | UTF-8; a String is well-formed UTF-8 without a line feed. A file may start with a byte order mark (BOM), which is not part of the first line. None is written. |
| Malformed lines | The checker stops at the first line that is not valid, in either file, and names the file, the line number and the reason. What the sorter does with a malformed input: *TBD*, decided together with the sorter. |
| Empty String part | Valid: `5. ` followed by the line ending is a line with an empty String. |

Why ordinal comparison: it is deterministic and identical on every machine and OS, it does not depend on culture
settings, and it works directly on bytes without decoding to UTF-16, which matters at 100 GB. Culture-aware
ordering would make the output depend on the machine's locale.

## How it works

### Generator

In a real team, test data like this is usually produced by QA engineers, and their machines may not have .NET
installed. The generator is therefore built as a tool for them as much as for developers: besides the regular build,
it publishes as a single self-contained executable (see [Design decisions](#design-decisions)).

It writes a file of the requested size, made of valid `Number. String` lines:

```powershell
FileSort.Generator --output data/test.txt --size 1GB --seed 42
```

- **Reproducible.** The same seed and options give the same bytes on any machine, with any number of threads and on
  any .NET version. Every run prints the command line that writes its file again.
- **Made for testing a sorter.** Strings of dictionary words with a configurable share of repeated Strings, Numbers
  of every length, fully identical lines.
- **Edge cases on request** (`--edge-cases`): text outside ASCII, the separator inside a String, letter case,
  Numbers that do not fit in 32 or 63 bits, leading zeros, empty Strings.
- **Careful with files.** The output path never holds a partial file, and an existing file is replaced only with
  consent.
- **Fast and small.** 100 GB in about a minute with under 100 MiB of memory (see [Performance](#performance)).

The options, what the lines contain, how the file is written, the measurements and the design decisions of the
generator are in [FileSort.Generator/README.md](FileSort.Generator/README.md).

### Sorter

*TBD.* The approach is an external merge sort: split the input into chunks that fit the memory budget, sort each
chunk in memory and write it to a temporary file (a "run"), then merge the runs into the output. Details such as the
in-memory line representation, parallelism, buffer sizes and the merge strategy follow from measurements and will be
described here.

The sorting engine is a class library, and the console app is a thin host around it. This keeps the algorithm
testable in-process and usable outside the command line.

### Checker

At 100 GB nobody can look at an output and see that it is right. The checker says so: it confirms that a sorted
file is the sorted version of an input file. It was written before the sorter, without its code, so the sorter is
developed against an oracle that already exists. Like the generator, it also publishes as a single self-contained
executable, so QA can use it.

```powershell
FileSort.Checker --input data/test.txt --sorted data/sorted.txt
```

- **Three checks, one result.** Every line of both files is valid; the lines of the sorted file are in order, by
  String and then by Number; and the sorted file has exactly the lines of the input, byte for byte (the same number
  of lines and the same order-independent 128-bit hash of them). The result is `CORRECT`, `NOT CORRECT` or
  `INVALID FILE`, with an exit code for each.
- **Both files, always.** The order alone proves little: an empty output is in order, and so is one that lost
  lines. The input is what shows that.
- **A report that points at the cause.** The first ten order violations with their lines, the number of lines of
  each file, and separate verdicts on the order and on the lines.
- **Its own code.** It shares no line parsing or comparison with the other programs.
- **Fast and small.** A pair of 1 GB files in under 2 s with under 70 MiB of memory, which does not grow with the files
  (see [Performance](#performance)).

The options, what exactly is checked, the report, the measurements and the design decisions of the checker are in
[FileSort.Checker/README.md](FileSort.Checker/README.md). A static HTML report built from the sorter's logs, with
time per phase, throughput and memory, is a later piece of work, after the sorter.

## Verifying correctness

*TBD for the sorter.* Automated tests compare the sorter against a naive in-memory sort on generated files, with a
tiny memory budget that forces many temporary runs. The Checker covers the large files.

The checker is the tool that verifies the sorter, so it is verified itself, against code that shares nothing with
it: a naive parser, comparison and sort that live in the test project. Its tests include a catalogue of the ways a
sorter can plausibly get an output wrong (a line lost, written twice or changed, neighbours swapped, Numbers
ordered as text, a file cut short, the other line ending), each of which the checker must reject. They are
described in [its README](FileSort.Checker/README.md#tests); the tests of the generator in
[its README](FileSort.Generator/README.md#tests).

### Code coverage

The rule is at least 80% of the lines of production code. Measure it with:

```powershell
dotnet test --solution FileSort.slnx -c Release --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

| Assembly | Lines | Branches |
|---|---|---|
| `FileSort.Common` | 100% | 100% |
| `FileSort.Cli` | 89.7% | 100% |
| `FileSort.Generator` | 98.3% | 91.8% |
| `FileSort.Checker` | 98.5% | 96.8% |
| All | 97.9% (1802 of 1841) | 94.6% (669 of 707) |

The table of a run, and the 80% check, come from `./.github/scripts/coverage-summary.ps1 -Report
TestResults/coverage.cobertura.xml`. CI applies the check to the assemblies of one program at a time: the
generator with the two shared libraries, the checker with `FileSort.Cli`.

Four classes are under 80%, on purpose. They are the thin layer between a program and the process it runs in,
and tests run the programs in-process, through `GeneratorApp` and `CheckerApp`, with a console of their own,
which covers everything behind that layer:

- `Program` of the generator and of the checker (0%): one line each, the entry point.
- `SystemConsole` (0%): a pass-through to `System.Console`.
- `CancellationSignals` (64.7%): the handlers of Ctrl+C and of the termination signals run only when a signal
  arrives; a test cannot send one to its own process without ending the test run.

These are checked on the published executables instead: the generator writes the pinned reference file, the
checker gives the exit codes 0 and 4 for a correct and a broken pair, and a real Ctrl+C console event, sent by a
script, ends each of them with exit code 130: the generator with no file left, the checker with the cancellation
reported and no result. `FileSort.Sorter` is still a placeholder and is not in the report.

## Performance

Measured on the development machine, called the reference machine below:

| Component | Value |
|---|---|
| OS | Windows 11 |
| CPU | 8 cores, 16 logical processors |
| RAM | 32 GB |
| Disk | NVMe SSD, NTFS |
| .NET | Runtime 10.0.15, built with SDK 11.0.100 |

Two properties of this machine shape every number below, and they are worth knowing before reading any of them:

- **The processor is limited by power and heat.** Under a long load eight busy cores are far from eight times one
  core, and the same measurement varies by about 10% from run to run.
- **The SSD has a fast cache.** It takes about 2.5 GiB/s while the cache has room and well under 1 GiB/s once a long
  series of writes has filled it; it needs idle time to recover. A series of runs without pauses measures the state
  of the cache, not the code. The first run of the write-path experiment showed exactly that: the configurations
  that ran last looked two times slower than the same ones in a series with pauses. Every disk measurement below
  was taken with pauses between runs, and a series that ran into the full cache is marked.

How the numbers were taken, and how far they can be trusted:

- **Processor-only measurements use BenchmarkDotNet**: warm-up, dozens of iterations, statistics, allocated bytes.
  These are the tables of the hot path and of the thread scaling without a disk in the
  [generator's README](FileSort.Generator/README.md#performance), and of one thread and of the hash in the
  [checker's README](FileSort.Checker/README.md#performance).
- **Disk measurements use a stopwatch around one whole run**, and that is the right tool: a run lasts seconds, so
  the timer's error does not matter, and a harness that repeats every case dozens of times would write hundreds of
  gigabytes. What limits them is the number of runs. **A disk figure below comes from one run unless its table
  shows several**, so a difference under about 10% between two rows proves nothing. The experiments now take
  `--runs` and print the median, the slowest and the fastest run; the tables below were recorded before that and
  were not taken again, to spare the disk.

### Sorter

*TBD.*

| Input size | Wall time | Peak memory |
|---|---|---|
| 1 GB | *TBD* | *TBD* |
| 10 GB | *TBD* | *TBD* |
| 100 GB | *TBD* | *TBD* |

### Checker

The published executable with default options checks a pair of 1 GB files, an input and its sorted copy, in 1.9 s
(the median of five runs) and uses under 70 MiB of memory at 100 MB and at 1 GB alike. The requirements are a pair
of 1 GB in under 3 seconds, which is the rate of a pair of 100 GB in 5 minutes, and under 256 MB of memory that
does not grow with the files. The files were read from the file cache of the operating system: the figure is the
processor cost of a check.

The tables, how the check scales with threads, why it is parallel, the cost of the hash and the memory are in the
[checker's README](FileSort.Checker/README.md#performance).

### Generator

The published executable with default options writes 100 GB in about a minute and uses under 100 MiB of memory at
any size. The requirements are 100 GB in under 5 minutes and under 512 MB of memory that does not grow with the
file.

The full table, where the limit is, what the disk takes, the file sink, the block size, the hot path and the memory
are in the [generator's README](FileSort.Generator/README.md#performance).

## Design decisions

Notable decisions and their reasons, recorded as they are made.

- **One solution, flat layout.** Each program lives in its own project folder at the root; shared line format code
  lives in `FileSort.Common`.
- **Independent checker.** The Checker has its own parsing and comparison of lines and shares none with the Sorter
  or the Generator, so that it can catch their bugs instead of repeating them. What it does share is the console
  plumbing of `FileSort.Cli`, which has no line code.
- **Exit codes mean the same in every program.** 0 is success, 1 a failure to do the work, 2 invalid arguments,
  130 a cancellation; the checker adds 3 for a file that is not valid and 4 for an output that is not correct. Code 4
  is not code 1 because the most useful thing a script can learn from a checker is the difference between "checked,
  and it is wrong" and "could not check". The codes are defined once, in `FileSort.Cli`.
- **Reports instead of a UI.** Visual analysis is planned as a static HTML report built from the Sorter's logs,
  after the sorter exists. A dedicated UI application would add another technology stack and a lot of code without
  making the sorter any better.
- **Decisions about the checker** (digits instead of converted Numbers, a sum of 128-bit hashes, one line ending
  for the pair, why it checks in parallel) are in [its README](FileSort.Checker/README.md#design-decisions).
- **Standalone generator and checker.** Test data is usually produced, and results checked, by QA engineers whose
  machines may not have .NET. The generator and the checker therefore publish as self-contained single executables;
  the sorter runs on the regular .NET runtime. Options measured on a minimal build (win-x64):

  | Publish mode | Size | Startup |
  |---|---|---|
  | Single file (chosen) | 70 MB | 69 ms |
  | Single file, compressed | 36 MB | 135 ms |
  | Single file, trimmed | 12 MB | 376 ms |

  Size doesn't matter for an internal tool, and trimming slows startup (it drops the framework's precompiled code)
  and adds risk, so the plain single file wins. NativeAOT was also considered: it gives the same "no .NET needed"
  result, but requires a C++ toolchain on the build machine, can't publish for another OS, and gives up the JIT's
  dynamic profile-guided optimization. The generator's code stays AOT-compatible, so switching later is one flag.
  Like any self-contained app, both bundle the runtime and must be republished after .NET security updates.
- **Decisions about the generator** (its own random number generator, dictionary words instead of random letters,
  Numbers of every length, `System.CommandLine`, how the file is written) are in
  [its README](FileSort.Generator/README.md#design-decisions).
- **`FileSort.Common` holds only what two programs really share.** Today that is the constants of the line format
  and the line ending. The code that writes a line lives in the generator: the generator is the only program that
  composes lines, while the sorter copies them as bytes. The line parser is not there yet either: it is the
  sorter's hottest code and its shape depends on the sorter's design, so it is written and measured together with
  the sorter. Until then the tests validate generated files with their own naive parser, which shares no code with
  the writer: a writer and a parser built on one misunderstanding would agree with each other.
- **The console code is a library of its own, `FileSort.Cli`, not a part of `FileSort.Common`.** The programs
  share what a console program needs: the exit codes, a console that tests can replace, the progress line, Ctrl+C
  and the termination signals as one cancellation token, command-line errors that name their option. Putting these
  classes into `FileSort.Common` would save a project and break two rules that project references enforce today.
  The checker cannot call the shared line code, because it has no reference to `FileSort.Common`; with the console
  classes there it would get that reference, and its independence would become a promise. And the sorting engine
  must have no console dependencies, but it references `FileSort.Common` for the line format, so the console
  classes and `System.CommandLine` would reach it through that reference. A test fails if the checker's assembly
  references `FileSort.Common`.
- **Strict build.** Nullable reference types, the recommended .NET analyzers and warnings as errors for every
  project. The one exception is NuGet vulnerability audit warnings: they remain visible but do not fail the build,
  because new advisories are published over time and a clean checkout must keep building later.
- **Any recent SDK builds it.** The programs target .NET 10 (LTS), but any SDK from 10.0 up can build them. The C#
  version and the analyzer rule set are pinned to .NET 10, so a newer SDK produces the same warnings and cannot
  break the strict build.
- **Invariant globalization.** Comparison is ordinal, so the programs need no culture data.
- **Reproducible restore.** A local `nuget.config` and central package versions keep the build independent of the
  machine it runs on.

## Development process

The solution is developed with an AI assistant using spec-driven development
([OpenSpec](https://github.com/Fission-AI/OpenSpec)). Each significant piece of work starts as a change proposal in
`openspec/changes/` with a proposal, requirement deltas, a design and tasks; it is reviewed before implementation and
archived afterwards. `openspec/specs/` holds the resulting requirements with acceptance scenarios, and the archived
changes keep the history of design decisions. `CLAUDE.md` contains the working rules for the assistant.

### Continuous integration

GitHub Actions workflows live in `.github/workflows/`:

- **Generator** (`generator.yml`) and **Checker** (`checker.yml`) each build the solution and run the tests of one
  program with code coverage, on every push to `main` and on every pull request that can affect that program. The
  generator's workflow starts on changes of the generator, of the shared libraries, of the tests and of the build
  files; the checker's on changes of the checker and on the same shared parts, and also on changes of the
  generator, because the tests of the checker make their inputs with it. A commit that touches only the checker
  does not run the generator's tests, and a commit that touches only documentation or `openspec/` starts nothing.
- **Test a program** (`test-program.yml`) holds the steps once; the two workflows call it with the namespaces of
  the tests to run and the assemblies whose coverage counts. It builds the whole solution (warnings are errors in
  every project), runs the tests, and shows the test results and the coverage table on the page of the run; the
  run fails when the line coverage of the program's assemblies, taken together, is below 80%. Compiler messages
  appear as annotations on the changed lines. The test results (JUnit XML) and the coverage report (Cobertura) are
  attached to the run.
- **Publish a program** (`publish.yml`) is started by hand, with the "Run workflow" button and with the program
  (generator or checker) and the target platform as its inputs. It builds the self-contained executable and
  attaches it to the run. For `win-x64` it also runs the executable: the generator must write the same bytes as
  the regular build, and the checker must give exit code 0 for a correct pair and 4 for a broken one.

The workflows run on Windows. The sorter gets a third workflow of the same shape, where the split will matter
most: its tests will be the slow ones.

## Limitations and next steps

- The sorter is not implemented yet; the generator and the checker are the first two of the three programs.
- The checker has not checked 100 GB yet: no sorted file of that size exists before the sorter. It is measured on
  1 GB, and at 100 GB once, together with the sorter.
- The HTML report of a sort is a later piece of work, after the sorter.
- The limitations of the generator and of the checker are in their READMEs:
  [generator](FileSort.Generator/README.md#limitations), [checker](FileSort.Checker/README.md#limitations).
