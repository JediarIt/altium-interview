# FileSort

External sorting of very large text files (~100 GB and more) in C# / .NET 10, plus a test file generator and an
independent checker.

> **Status: work in progress.** The solution structure, assumptions and engineering rules are in place. Sections
> marked *TBD* get filled in as each part is implemented and measured.

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

Generate, sort and check a 1 GB file: *TBD, the command-line interfaces are not implemented yet.*

The generator and the checker can also be published as single executables that run on a machine without .NET
installed (for example a QA machine):

```powershell
dotnet publish FileSort.Generator -c Release -r win-x64 -o publish/generator
dotnet publish FileSort.Checker -c Release -r win-x64 -o publish/checker
```

Use `linux-x64`, `osx-arm64` and so on for other platforms; any machine with the .NET SDK can publish for any of
them.

Disk space: sorting a file needs roughly 3x its size free (input, temporary runs and output). For the 100 GB
scenario, plan for about 300 GB.

## Repository layout

| Path | Purpose |
|---|---|
| `FileSort.Common/` | Class library: the line format shared by Generator and Sorter |
| `FileSort.Generator/` | Console app: test file generator |
| `FileSort.Sorter/` | Console app: the external sorter |
| `FileSort.Checker/` | Console app, a development tool: verifies a sorted output against its input and builds an HTML report from the sorter's logs |
| `FileSort.Tests/` | Unit and integration tests |
| `openspec/` | Requirements (`specs/`) and the history of change proposals with their designs (`changes/`), see [Development process](#development-process) |
| `CLAUDE.md` | Working rules for the AI assistant, see [Development process](#development-process) |
| `.claude/skills/`, `.agents/skills/` | OpenSpec workflow definitions for the AI assistant (generated) |
| `Directory.Build.props` | Settings shared by all projects: `net10.0`, nullable reference types, analyzers, warnings as errors |
| `Directory.Packages.props` | Central package versions |
| `nuget.config` | Restores from nuget.org only, so the build does not depend on machine-level feeds |
| `global.json` | Requires a .NET SDK 10.0.100 or newer (release versions only); runs tests on Microsoft.Testing.Platform |

`FileSort.Checker` intentionally does not reference `FileSort.Common`. It parses and compares lines with its own
deliberately simple code, so a bug in the optimized sorting code cannot be confirmed by that same code.

## Assumptions

The task leaves several details open. The table shows how each one is resolved; items marked *TBD* are decided
together with the implementation.

| Topic | Decision |
|---|---|
| String comparison | Ordinal, byte by byte over UTF-8, which equals Unicode code point order. Case-sensitive: `Banana` sorts before `apple`. |
| Number comparison | Numeric, not textual: `2` comes before `10`. Supported range and leading zeros: *TBD*. |
| Separator | The first `". "` in a line. The String part may itself contain `". "`. |
| Duplicate lines | Kept. Lines with equal keys are identical, so stability does not matter. |
| Line endings | Input may use `\n` or `\r\n`; the last line may lack a line break. Output line ending: *TBD*. |
| Encoding | UTF-8. A BOM in the input is tolerated; none is written. |
| Malformed lines | *TBD*: fail with the line number, or skip and report. |
| Empty String part | *TBD*. |

Why ordinal comparison: it is deterministic and identical on every machine and OS, it does not depend on culture
settings, and it works directly on bytes without decoding to UTF-16, which matters at 100 GB. Culture-aware
ordering would make the output depend on the machine's locale.

## How it works

### Generator

In a real team, test data like this is usually produced by QA engineers, and their machines may not have .NET
installed. The generator is therefore built as a tool for them as much as for developers: besides the regular build,
it publishes as a single self-contained executable (see [Design decisions](#design-decisions)).

*TBD.* Planned properties: output size close to the requested size, a configurable share of repeated String parts,
deterministic output for a given seed (the same seed and size always give the same file), and enough throughput to
produce 100 GB in reasonable time.

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

*TBD.* Automated tests compare the sorter against a naive in-memory sort on generated files, with a tiny memory
budget that forces many temporary runs. The Checker covers the large files.

## Performance

Measured on the development machine:

| Component | Value |
|---|---|
| OS | Windows 11 Pro |
| CPU | 16 logical cores |
| RAM | 32 GB |
| Disk | NVMe SSD |
| .NET | Runtime 10.0.15, built with SDK 11.0.100 |

Results: *TBD.*

| Input size | Wall time | Peak memory |
|---|---|---|
| 1 GB | *TBD* | *TBD* |
| 10 GB | *TBD* | *TBD* |
| 100 GB | *TBD* | *TBD* |

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

## Limitations and next steps

*TBD.*
