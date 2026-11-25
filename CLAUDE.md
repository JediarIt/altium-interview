# CLAUDE.md

This file provides guidance to Claude Code when working in this repository. The problem statement, assumptions,
repository layout and design rationale are in [README.md](README.md); requirements with acceptance scenarios are
kept in [openspec/specs](openspec/specs).

## Priorities

1. Correctness of generation and sorting.
2. A 1 GB file sorts in under 1 minute using about 2 GB of memory or less, both at the same time. Inputs of
   ~100 GB must work.
3. Code quality.

The hardware the solution will run on is unknown: keep a wide margin on time and memory, and make the memory budget,
the temp directory and the degree of parallelism configurable. Prefer decisions backed by measurements over
intuition.

## Commands

```powershell
dotnet build FileSort.slnx -c Release
dotnet test --solution FileSort.slnx -c Release

# Tests with code coverage: the report goes to TestResults/coverage.cobertura.xml (ignored by git)
dotnet test --solution FileSort.slnx -c Release --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml

# The coverage table that CI prints, and its 80% check; without GitHub it prints to the console
./.github/scripts/coverage-summary.ps1 -Report TestResults/coverage.cobertura.xml

# One program, as its CI workflow does it: the tests of its namespaces, the coverage of its assemblies
dotnet test --solution FileSort.slnx -c Release --filter-namespace FileSort.Tests.Generator* FileSort.Tests.Common* FileSort.Tests.Format* FileSort.Tests.Cli* --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
./.github/scripts/coverage-summary.ps1 -Report TestResults/coverage.cobertura.xml -Assemblies FileSort.Generator,FileSort.Common,FileSort.Cli
dotnet test --solution FileSort.slnx -c Release --filter-namespace FileSort.Tests.Checker* FileSort.Tests.Format* FileSort.Tests.Cli* --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
./.github/scripts/coverage-summary.ps1 -Report TestResults/coverage.cobertura.xml -Assemblies FileSort.Checker,FileSort.Cli
dotnet test --solution FileSort.slnx -c Release --filter-namespace FileSort.Tests.Sorter* FileSort.Tests.Engine* FileSort.Tests.Format* FileSort.Tests.Cli* --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
./.github/scripts/coverage-summary.ps1 -Report TestResults/coverage.cobertura.xml -Assemblies FileSort.Sorter,FileSort.Engine,FileSort.Cli

dotnet publish FileSort.Generator -c Release -r win-x64 -o publish/generator
dotnet publish FileSort.Checker -c Release -r win-x64 -o publish/checker
# The sorter runs on the .NET runtime: a framework-dependent build
dotnet publish FileSort.Sorter -c Release -o publish/sorter

# Generator: a test file in the ignored data/ directory (all options: --help, or FileSort.Generator/README.md)
dotnet run -c Release --project FileSort.Generator -- --output data/test-1gb.txt --size 1GB --seed 1

# Sorter: sort a file (all options: --help, or FileSort.Sorter/README.md)
dotnet run -c Release --project FileSort.Sorter -- --input data/test-1gb.txt --output data/sorted-1gb.txt

# Checker: is the second file the sorted version of the first? (all options: --help, or FileSort.Checker/README.md)
dotnet run -c Release --project FileSort.Checker -- --input data/test-1gb.txt --sorted data/sorted-1gb.txt

# Check publish: the trimmer analyses a whole program, its packages included; it must report no warnings
dotnet publish FileSort.Generator -c Release -r win-x64 -p:PublishTrimmed=true -o publish/trim-check
dotnet publish FileSort.Checker -c Release -r win-x64 -p:PublishTrimmed=true -o publish/trim-check

# Benchmarks and experiments (usage: run without arguments)
dotnet run -c Release --project FileSort.Benchmarks -- micro --filter *
dotnet run -c Release --project FileSort.Benchmarks -- write-path --file data/write-path.bin --writers 1 --pause 30
dotnet run -c Release --project FileSort.Benchmarks -- file-sink --file data/file-sink.bin
dotnet run -c Release --project FileSort.Benchmarks -- pipeline --sink discard
dotnet run -c Release --project FileSort.Benchmarks -- block-merge
dotnet run -c Release --project FileSort.Benchmarks -- sort-phases
dotnet run -c Release --project FileSort.Benchmarks -- progress-cost

# A sorted copy of a file of up to 2 GB, made in memory, and the check of the pair by chunk size and threads
dotnet run -c Release --project FileSort.Benchmarks -- sorted-copy --input data/test-1gb.txt --output data/sorted-1gb.txt
dotnet run -c Release --project FileSort.Benchmarks -- check --input data/test-1gb.txt --sorted data/sorted-1gb.txt
```

`dotnet test` runs on Microsoft.Testing.Platform (set in `global.json`).

A change that alters the bytes the generator writes for an existing seed raises `ContentFormat.Version` and records
new reference hashes in `ReferenceFileTests`; a hash recorded for an existing version is never edited.

## Architecture rules

- `FileSort.Common` holds the line format shared by the Generator and the Sorter.
- `FileSort.Cli` holds the console plumbing shared by the console programs: exit codes, the console abstraction,
  the progress line, Ctrl+C, command-line errors, the parsers of sizes and whole numbers, the overwrite question. It
  references `System.CommandLine` and nothing else. Console
  code never goes into `FileSort.Common`: the checker and the sorting engine must not get it through that
  reference.
- The sorting engine lives in `FileSort.Engine`, a class library with no console dependencies (a test checks its
  references); `FileSort.Sorter` is only a thin CLI host over it (arguments, console output, progress, exit codes).
  Spectre.Console, which draws the sorter's progress, is referenced by `FileSort.Sorter` and nothing else.
- `FileSort.Checker` must not reference `FileSort.Common`. It is an independent oracle with its own deliberately
  simple parsing and comparison, so a bug in the optimized code cannot be confirmed by the same code.
- The Generator and the Checker also ship as self-contained single-file executables (`dotnet publish -r <rid>`): in
  practice QA engineers generate test data and check results, and their machines may not have .NET. The Sorter runs
  on the regular runtime. Generator and Common code stays AOT- and trim-safe: no reflection-based serialization, no
  dynamic code.
- `.claude/skills/` and `.agents/skills/` are generated by OpenSpec: refresh them with `openspec update`, never edit
  them by hand.

## Performance

- Work on raw UTF-8 bytes (`Span<byte>` / `Memory<byte>`); no `string` per line on hot paths.
- No per-line heap allocations: large pooled buffers, struct-based line entries. No array of 85,000 bytes or more
  allocated per chunk or per run either: it lives on the heap of large objects, which only a full collection frees,
  and a sort that allocates almost nothing never causes one. Rent such arrays or keep them.
- Account for the memory budget explicitly (data buffers, line index, I/O buffers) and verify it via peak working
  set. Choose the GC mode deliberately and measure it.
- Overlap I/O with CPU work and use all cores for parsing and sorting.
- Measure, don't guess: BenchmarkDotNet for hot primitives (parse, compare), plus an end-to-end harness that
  reports wall time and peak memory.
- Disk measurements: files of 1 to 2 GB by default, the 100 GB case once at the end. Leave pauses between runs (an
  SSD's fast cache fills, and the next run measures the cache), repeat a case with `--runs` and report the median
  and the spread; say how many runs stand behind a number. Processor-only measurements go through BenchmarkDotNet.
- Build the solution before a `--no-build` run of `FileSort.Benchmarks`: `dotnet test` does not rebuild it.

## Code

- Target `net10.0` with C# 14. The language version and the analyzer rules are pinned to .NET 10 in
  `Directory.Build.props`, so any SDK from 10.0 up produces the same warnings; don't raise them without a reason.
  `RollForward` is `Major` there, so a machine that has only a newer runtime still runs what it builds.
- Warnings are errors and nullable reference types are on. NuGet audit warnings (NU1901-NU1904) are the only
  exception: new advisories appear over time and must not break a clean checkout later.
- Package versions live in `Directory.Packages.props`; `PackageReference` items carry no `Version`.
- Small, single-responsibility types with clear boundaries: line format and parsing, comparison, run creation,
  merging, CLI. No god classes and no speculative abstractions.
- One top-level type per file, and the file is named after the type. No exceptions, in tests and benchmarks too: a
  two-line record or an enum that only one class uses still gets its own file. Types nested inside a class stay in
  it.
- Folders group files by what they do (`Cli`, `Content`, `Output`, `Pipeline`), not by what kind of type they hold:
  no `Enums`, `Extensions` or `Constants` folders. A folder is a short list, not a sheet: when it grows past about
  seven files, split it into subfolders by responsibility (`Cli/Options`, `Cli/Terminal`, `Content/Words`,
  `Content/Lines`, `Content/Blocks`). The namespace of a file is its folder, and the tests mirror the folders of the
  code they test.
- Before writing a constant, a helper or a loop, check whether the framework already has it (`Encoding.UTF8.Preamble`,
  `Utf8Formatter`, `Directory.CreateTempSubdirectory`). Hand-written code replaces a framework call only when a
  benchmark shows the gain, and a comment next to it says so.
- Use a primary constructor where a constructor only stores its parameters; keep private fields, initialized from
  the parameters, so the `_field` naming stays. A constructor that validates or computes stays an ordinary one.
- Lines read at a glance. A line, code or comment, is at most 120 characters; a longer call or signature is wrapped,
  one argument or parameter per line when they do not fit on one continuation line, and a value computed for an
  argument gets a local with a name. No lambda inside a lambda: the inner one becomes a named method. LINQ is for
  plain projections and filters; work with side effects, such as starting threads or writing, goes in a loop. `?:`
  and `?? throw` only when the whole expression is short; otherwise an `if`.
- Keep methods flat, about two levels of nesting. State that several steps of a method share becomes a small type
  with named methods, not a local function that captures the method's locals; a `try`, a loop or a branch that
  would nest deeper becomes a method with a name.
- Keep third-party dependencies in production code to a minimum.
- A clear CLI: `--help`, meaningful error messages, non-zero exit codes; Ctrl+C cancels and cleans up temp files.
- The generator is deterministic for a given seed (reproducible test files) and fast enough to produce 100 GB in
  reasonable time.

## Testing

- Unit tests for parsing and comparison edge cases.
- Integration tests: generate, then sort with a tiny memory budget (forces many runs and a multi-pass merge), then
  compare against a naive in-memory sort.
- The Checker confirms that an output is sorted AND is a permutation of its input (line count plus an
  order-independent hash).
- Tests write as little to the disk as they can: megabytes, not hundreds of megabytes. What can be checked in
  memory (content, determinism, thread counts) is checked in memory; files are for what only a file can show.
- Tests cover at least 80% of the lines of production code (every project except the tests and the benchmarks).
  Measure the coverage before calling a change done. Below 80%, for the whole or for one class, either add tests
  or say in the README why that code is not covered by tests and what checks it instead.

## Workflow

Each significant piece of work (generator, sorter, checker) goes through OpenSpec:

1. `/openspec-propose` creates `openspec/changes/<name>/` with a proposal, spec deltas, a design and tasks. Planning
   only, no code.
2. The author reviews and edits the artifacts.
3. `/openspec-apply-change` implements the tasks.
4. `/openspec-archive-change` merges the spec deltas into `openspec/specs/` and archives the change.

Small fixes don't need a change. The README's assumptions move into `openspec/specs/` as changes get archived.

Continuous integration is in `.github/workflows/`. Every program has a workflow of its own (`generator.yml`,
`checker.yml`, `sorter.yml`), started by the changes that can affect it (not by documentation or `openspec/`
alone); each calls `test-program.yml`, which builds the solution, runs the tests of the program's namespaces and
checks the coverage of its assemblies. `publish.yml` builds the executable of the generator or of the checker by
hand; the sorter runs on the .NET runtime and is not in it. What CI runs is what the Commands above run: change a
command there and here together. A new program gets a workflow of its own that calls `test-program.yml` and, when
it ships as an executable, an option in `publish.yml`. A new test namespace is added to the workflow of its
program: tests in a namespace that no workflow names do not run in CI. A test helper that the tests of another
program use is listed by name in that program's path filters. Test results leave the run as JUnit XML and as a
Markdown summary; coverage as Cobertura.

Rules for every change:

- Update the READMEs in the same change as every decision, feature or measured result; they must never be stale.
  The root `README.md` is the overview and stays short; the details of a program (options, behavior, measurements,
  its design decisions and limitations) live in the `README.md` of its project folder, and the root links to it.
  A table is written in one of them, not in both; the root quotes headline figures only.
- Documentation describes the reference machine in general terms (cores, memory, kind of disk): no model names,
  no paths of the development machine. Example paths are neutral, like `C:\data\test.txt` or `data/test.txt`.
- Write code, comments, commit messages and docs in English.
- Never commit large data files: use the ignored `data/` directory or a path outside the project.
