# FileSort

External sorting of very large text files (~100 GB and more) in C# / .NET 10, plus a test file generator and an
independent checker.

> **Status: work in progress.** The generator is implemented and measured; the checker and the sorter come next.
> Sections marked *TBD* get filled in as each part is implemented and measured.

## Contents

- [The problem](#the-problem)
- [Quick start](#quick-start)
- [Repository layout](#repository-layout)
- [Assumptions](#assumptions)
- [How it works](#how-it-works)
- [Verifying correctness](#verifying-correctness)
- [Performance](#performance)
- [Design decisions](#design-decisions)
- [Development process](#development-process)
- [Limitations and next steps](#limitations-and-next-steps)

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

## Quick start

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

Sort and check the file: *TBD, the sorter and the checker are not implemented yet.*

The generator and the checker can also be published as single executables that run on a machine without .NET
installed (for example a QA machine):

```powershell
dotnet publish FileSort.Generator -c Release -r win-x64 -o publish/generator
dotnet publish FileSort.Checker -c Release -r win-x64 -o publish/checker
```

Use `linux-x64`, `osx-arm64` and so on for other platforms; any machine with the .NET SDK can publish for any of
them. The published generator takes the same options:

```powershell
publish/generator/FileSort.Generator.exe --output data/test-1gb.txt --size 1GB
```

Disk space: sorting a file needs roughly 3x its size free (input, temporary runs and output). For the 100 GB
scenario, plan for about 300 GB.

## Repository layout

| Path | Purpose |
|---|---|
| `FileSort.Common/` | Class library: the line format shared by Generator and Sorter (format constants and the line ending) |
| `FileSort.Generator/` | Console app: test file generator; its [README](FileSort.Generator/README.md) has the options, the measurements and the design decisions |
| `FileSort.Sorter/` | Console app: the external sorter |
| `FileSort.Checker/` | Console app, a development tool: verifies a sorted output against its input and builds an HTML report from the sorter's logs |
| `FileSort.Tests/` | Unit and integration tests |
| `FileSort.Benchmarks/` | Console app: BenchmarkDotNet micro-benchmarks and the experiments behind the numbers in [Performance](#performance) |
| `openspec/` | Requirements (`specs/`) and the history of change proposals with their designs (`changes/`), see [Development process](#development-process) |
| `CLAUDE.md` | Working rules for the AI assistant, see [Development process](#development-process) |
| `.claude/skills/`, `.agents/skills/` | OpenSpec workflow definitions for the AI assistant (generated) |
| `Directory.Build.props` | Settings shared by all projects: `net10.0`, nullable reference types, analyzers, warnings as errors |
| `Directory.Packages.props` | Central package versions |
| `nuget.config` | Restores from nuget.org only, so the build does not depend on machine-level feeds |
| `global.json` | Requires a .NET SDK 10.0.100 or newer (release versions only); runs tests on Microsoft.Testing.Platform |
| `.github/` | GitHub Actions: build and tests with coverage on every code change, and publishing the generator by hand, see [Development process](#development-process) |

`FileSort.Checker` intentionally does not reference `FileSort.Common`. It parses and compares lines with its own
deliberately simple code, so a bug in the optimized sorting code cannot be confirmed by that same code.

## Assumptions

The task leaves several details open. The table shows how each one is resolved; items marked *TBD* are decided
together with the implementation. The line format is specified, with acceptance scenarios, in
[openspec/specs/line-format](openspec/specs/line-format/spec.md).

| Topic | Decision |
|---|---|
| String comparison | Ordinal, byte by byte over UTF-8, which equals Unicode code point order. Case-sensitive: `Banana` sorts before `apple`. |
| Number comparison | Numeric, not textual: `2` comes before `10`. A Number is 1 to 20 decimal digits with a value from 0 to 18446744073709551615 (an unsigned 64-bit integer). Leading zeros are valid and do not change the value: `007` is 7. No sign, no spaces. |
| Separator | The first `". "` in a line. The String part may itself contain `". "`. |
| Duplicate lines | Kept. Lines with equal keys can still differ in their bytes, because of leading zeros: `007. Apple` and `7. Apple`. The order of such lines is decided together with the sorter. |
| Line endings | A file has one line ending for all its lines, `\n` or `\r\n`, and the program that reads it works with that one. The last line may lack it. A carriage return that is not part of the line ending is an ordinary byte of the String, also at its end. Line ending of the sorter's output: *TBD*. |
| Encoding | UTF-8; a String is well-formed UTF-8 without a line feed. A file may start with a byte order mark (BOM), which is not part of the first line. None is written. |
| Malformed lines | *TBD*: fail with the line number, or skip and report. |
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

Primarily a development tool for the sorter, but QA can use it as well: like the generator, it publishes as a single
self-contained executable. *TBD.* Planned features:

- Correctness: every line of the output is ordered with respect to the previous one, and the output is a
  permutation of the input (same line count and same order-independent hash of all lines).
- Report: a static HTML page built from the sorter's structured logs, with time per phase, throughput and memory
  usage. It opens in any browser and needs no extra runtime.

## Verifying correctness

*TBD for the sorter.* Automated tests compare the sorter against a naive in-memory sort on generated files, with a
tiny memory budget that forces many temporary runs. The Checker covers the large files.

The tests of the generator are described in [its README](FileSort.Generator/README.md#tests).

### Code coverage

The rule is at least 80% of the lines of production code. Measure it with:

```powershell
dotnet test --solution FileSort.slnx -c Release --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

| Assembly | Lines | Branches |
|---|---|---|
| `FileSort.Common` | 100% | 100% |
| `FileSort.Generator` | 96.2% | 92.7% |
| Both | 96.2% (989 of 1028) | 92.8% (333 of 359) |

Two classes are at 0%, on purpose: `Program` (the entry point: it wires Ctrl+C and the termination signals to a
cancellation token) and `SystemConsole` (a pass-through to `System.Console`). Tests run the generator in-process
through `GeneratorApp` with a console of their own, which covers everything behind these two. The entry point
itself is checked on the published executable: it writes the pinned reference file, and a real Ctrl+C console event
ends it with exit code 130 and no file left. `FileSort.Sorter` and `FileSort.Checker` are still placeholders and
are not in the report.

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
  [generator's README](FileSort.Generator/README.md#performance).
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
- **Independent checker.** The Checker shares no code with the Sorter so that it can catch the Sorter's bugs.
- **Reports instead of a UI.** Visual analysis comes from a static HTML report that the Checker builds from the
  Sorter's logs. A dedicated UI application would add another technology stack and a lot of code without making the
  sorter any better.
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

Two GitHub Actions workflows live in `.github/workflows/`:

- **CI** (`ci.yml`) builds the solution and runs the tests with code coverage on every push to `main` and on every
  pull request, when code changed: a commit that touches only documentation or `openspec/` does not start it. The
  page of a run shows the test results and the coverage table; the run fails when the line coverage is below 80%.
  Compiler messages appear as annotations on the changed lines. The test results (JUnit XML) and the coverage
  report (Cobertura) are attached to the run.
- **Publish the generator** (`publish.yml`) is started by hand, with the "Run workflow" button and the target
  platform as its input. It builds the self-contained executable, checks for `win-x64` that it writes the same
  bytes as the regular build, and attaches it to the run.

For now CI builds and tests the whole solution on any code change, on Windows: one test project covers all the
programs. It is split by program when the sorter and the checker get tests of their own.

## Limitations and next steps

- The sorter and the checker are not implemented yet; the generator is the first of the three programs.
- The limitations of the generator are in [its README](FileSort.Generator/README.md#limitations).
