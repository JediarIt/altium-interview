# Tasks

Decision ids (D1 to D17) and measurement ids (M1 to M4) refer to `design.md`. Names in quotes are requirements and
scenarios: of the specs of this change, or of the main specs `line-format` and `test-file-generator` where the
task names that spec. A task says what to check and points to where the values are; it does not repeat them. Large
files go to the ignored `data/` directory.

## 1. Shared console library

- [ ] 1.1 Create the `FileSort.Cli` class library, add it to `FileSort.slnx`, move into it the types and the code that D10
  lists and reference it from `FileSort.Generator`. Verify: `dotnet build FileSort.slnx -c Release` succeeds with no
  warnings; the tests of the generator pass with no change other than namespaces; the scenario "Pinned reference
  files" of `test-file-generator` passes.
- [ ] 1.2 Move the console of the tests to `FileSort.Tests/Cli` and add tests of the library there: the progress
  line, the description of command-line errors, an error written to a broken stream, and the values of the exit
  codes of D9. Verify: the tests pass, and the coverage report of a run of that folder's tests alone shows
  `FileSort.Cli` at or above the coverage rule of `CLAUDE.md`, apart from the pass-through console and the wiring
  of the signals, which D14 leaves to the published executable.
- [ ] 1.3 Publish the generator again, regular and as the check with trimming. Verify: the scenarios of "Standalone
  executable" of `test-file-generator` still hold.
- [ ] 1.4 Update `README.md` (the row of `FileSort.Cli` in Repository layout; in Design decisions, why the console
  code is a library of its own and not part of `FileSort.Common`, D10), the Architecture rules of `CLAUDE.md`, and
  the paragraph about `System.CommandLine` in the Design decisions of `FileSort.Generator/README.md`, which calls
  it the only package of production code and says that a shared parser has no home. Verify: none of the three
  files has a statement that this move made false.

## 2. Line format and the naive oracle

- [ ] 2.1 Change the naive parser of the tests to accept a Number of any length and to carry its value as D14
  says; add the naive comparison and the naive sort of D14 to the tests; describe the two constants of `LineFormat`
  as the 64-bit range instead of limits of the format. Verify: a test for every scenario of the modified
  requirement "Number" passes against the naive parser; the naive sort orders the lines of every scenario of `sort-order` as the scenario
  says; the tests of the generator still pass with unchanged reference hashes.
- [ ] 2.2 Update the Assumptions table of `README.md`: the rows about Number comparison and about duplicate lines.
  Verify: every row of the table agrees with the specs `line-format` and `sort-order` of this change.

## 3. Lines and checks

- [ ] 3.1 Set up `FileSort.Checker` as D15 describes: folders, references, the package of D4 in
  `Directory.Packages.props`, the AOT-compatible mark, internals visible to the tests and the benchmarks. Add the
  test of D10 that the checker's assembly does not reference `FileSort.Common`. Verify: the solution builds with no
  warnings and that test passes.
- [ ] 3.2 Implement the parsing of a line (D2) with the reasons of D7 that belong to the parser: all but the bare
  line feed, which the reader finds. Verify: tests for every scenario of the requirements "Line structure",
  "Number" and "String" of `line-format` pass against the checker's parser; and it
  agrees with the naive parser on every line of content generated with `--edge-cases all`.
- [ ] 3.3 Implement the comparison of two lines (D3). Verify: tests for every scenario of `sort-order` pass; and it
  agrees with the naive comparison on generated lines and on lines with Numbers above 64 bits.
- [ ] 3.4 Implement the reading of a stream as lines (D11), with the byte order mark (D6), the bare line feed of
  D5 and the limit on the length of a line (D7). Verify: tests for every scenario of the requirements "Line
  terminator" and "File structure" of `line-format` pass against the reader together with the parser of 3.2 (in
  the scenario "Line feed without a carriage return" the reader rejects line 2 at its bare line feed, without
  gathering the rest of it); the tests of D14 about buffer
  boundaries pass; a line above a small limit ends the reading, and D7's rule about a line that is both too long
  and invalid holds.
- [ ] 3.5 Implement the digest of a set of lines (D4). Verify: the tests of D14 about the digest pass.
- [ ] 3.6 Implement the choice of the line ending (D5), the scans of the two files and the check of a pair with its
  result (D1, D7, D11). Verify: tests on streams in memory pass for the scenarios of "Valid files", "Order", "Same
  lines", "Line ending" and "Byte order mark", as far as the result of the check goes; the correct pairs of D14
  give `CORRECT`; every row of the catalogue of D14 gives its expected result; and for a pair with an invalid line in
  each file the check records both, the same on a hundred runs.
- [ ] 3.7 Add the benchmarks of M1 and M2 to `FileSort.Benchmarks`, with the in-memory sort that D17 describes
  (the experiment `sorted-copy` of 5.1 reuses it), and run them. Create `FileSort.Checker/README.md`, starting
  with its Performance section, and record the two tables there. Verify: the
  BenchmarkDotNet report shows no bytes allocated per line in M2; the README numbers match the report; and if the
  hash takes the share of a scan that D4 names, the author is told so with the numbers before anything changes.

## 4. Command line

- [ ] 4.1 Replace the placeholder `Program.cs` with the command on `System.CommandLine`: the options of D13, the
  opening of the files, the exit codes of D9 and error messages without a stack trace. Verify: tests for the
  scenarios of "Command line", "Files are only read" and "Exit codes and error messages" pass; a test confirms that
  `--help` shows the default of `--line-ending`; and a test that lowers the limit of D7 in the settings of the
  application confirms the exit code and the message of the scenario "Line longer than supported".
- [ ] 4.2 Implement what the checker prints (D7, D8, D13): the violations as they are found, the report of an
  invalid line, the summary, the settings before the check, the progress line and the handling of a console that
  cannot be written to. Verify: tests for the scenarios of "Report of order violations", "Summary and result" and
  "Settings and progress" pass; so does everything in the scenarios of "Valid files", "Order", "Same lines",
  "Line ending" and "Byte order mark" that is about what is printed or about the exit code; a test of a pair in
  which both scans end early finds what D7 says about the report and the exit code; and tests cover the two cases
  of a failed console write that D8 names.
- [ ] 4.3 Wire Ctrl+C and the termination signals to the cancellation of the check. Verify: a test cancels, through
  the token, a check of a stream that never ends and finds that it stops within the time that "Cancellation"
  allows; and an in-process test of the application, run with a token that is already cancelled, finds the exit
  code and the message that "Cancellation" requires.
- [ ] 4.4 Write the rest of `FileSort.Checker/README.md`: usage with examples, the table of options typed by hand,
  why the input file is needed, with the example that D1 asks for, what is checked and how a miss can happen (D4), the report, the exit codes, design decisions, tests, and
  limitations, among them the longest line of D7. Add the test of the options table, with the table reader shared
  by both programs (D13). Update `README.md`: the status note, Quick start (a command that checks a pair of files),
  Verifying correctness, and the three places that say the checker builds the HTML report (its row in Repository
  layout, the Checker part of How it works, the decision about reports in Design decisions), so that they describe
  the report as a later change; the Assumptions rows
  about line endings and malformed lines, Design decisions (the wording of the checker's independence, exit codes
  shared by the programs), Limitations. Add the commands of the checker to `CLAUDE.md`. Verify: every command of
  the checker in the two READMEs runs as written, on a pair of small files put at the paths of the examples; the
  table has the options, values and defaults that `--help` prints; the table test
  fails with a row removed and with a default changed (try both, then restore the table).

## 5. Measurements

- [ ] 5.1 Add the experiments `sorted-copy` and `check` to `FileSort.Benchmarks` (D17) and their usage text. State
  the total that D17 gives before writing anything, then make the three pairs. Verify: the checker reports every
  pair as `CORRECT`, and `data/` holds these six files and nothing else from this change.
- [ ] 5.2 Run M3 and set the read buffer from it. Record the table and the choice in the Performance section of
  `FileSort.Checker/README.md` together with the command that reproduces it. Verify: the documented command runs as
  written and the table matches its output.
- [ ] 5.3 Run `dotnet publish FileSort.Checker -c Release -r win-x64 -o publish/checker` and the check publish with
  trimming, then run M4. Record wall time, throughput and peak working set in `FileSort.Checker/README.md` and the
  headline figure in the Performance section of `README.md`. Verify: the scenarios "Publishing for Windows" and
  "Trim analysis of the whole application" hold, and so do the scenarios "Peak memory for a pair of 1 GB" and
  "Memory does not follow the size".
- [ ] 5.4 Apply the decision rule of D11 to the result of M4. If the rule does not ask for the parallel scan,
  record in `FileSort.Checker/README.md` that one thread per file was enough, with the numbers. If it does,
  implement the parallel scan with its `--threads` option, add the option to the spec `output-checker` and to the
  options table, add a test that the result does not depend on the number of threads or on the size of a chunk, run
  M3 and M4 again and replace the numbers recorded in 5.2 and 5.3; if the scenario named below does not hold even
  then, stop and report to the author. Verify: the scenario "A pair of 1 GB" of "Throughput" holds, and every test
  of group 3 still passes.
- [ ] 5.5 Check the scenario "Ctrl+C during a check" once on the published executable: a script sends a real
  console Ctrl+C event while the default pair is being checked. Verify: the exit code is the one the scenario
  requires and the checker prints the cancellation, not a result. The check of 1 GB is too short to show the time
  limit on a real run; the test of 4.3 shows it.

## 6. Continuous integration and publishing

- [ ] 6.1 Replace `ci.yml` with the three workflows of D16 and give `coverage-summary.ps1` its parameter. Verify:
  locally, for each program, the commands that its workflow runs give this: the filtered tests pass, the coverage script
  passes for the assemblies of that program, and the tests selected by the two filters together are all the tests
  of the project.
- [ ] 6.2 Give `publish.yml` the program as an input and the check of the published checker (D16). Verify: the
  commands of that check, run locally on `publish/checker`, give the two exit codes that the scenario "Same results
  as the regular build" requires.
- [ ] 6.3 Update `README.md` (Continuous integration, and the row of `.github/` in Repository layout) and the
  Workflow section and the commands of `CLAUDE.md`. Verify: what the two files say the workflows run is what the
  workflow files run.

## 7. Final check

- [ ] 7.1 Check the whole change. Verify: `dotnet build FileSort.slnx -c Release` and
  `dotnet test --solution FileSort.slnx -c Release` pass with no warnings; the coverage is measured, is at or above
  the rule of `CLAUDE.md` for every program, and the Code coverage table of `README.md` shows it, with a reason for
  every class under the threshold; `openspec validate add-output-checker --strict` passes; `README.md`,
  `FileSort.Checker/README.md`, `FileSort.Generator/README.md` and `CLAUDE.md` contain no statement that this
  change made false, and no hardware model name or path of the development machine; the files left in `data/` are listed for
  the author; `git status --short` lists only files that belong to this change.

## Workflow follow-up

- The author reviews the implementation. On the author's command it is committed together with the archive of this
  change, as one commit after the commit of the plan: a change has two commits, the plan and the implementation.
- Archive the change with `/openspec-archive-change` after the review, before that commit.
- The first push runs the new workflows for the first time; expect to fix something there.
- The check of 100 GB is measured in `add-external-sorter`, on the output of the sorter's own 100 GB run, and the
  spec `output-checker` gets its scenario then.
