# Tasks

Decision ids (D1 to D21), measurement ids (M1 to M9) and planning measurements (P1 to P5) refer to `design.md`.
Names in quotes are requirements and scenarios: of the specs of this change, or of the main specs `line-format`,
`sort-order` and `test-file-generator` where the task names that spec. A name that both `external-sorter` and the
generator's delta use ("Line ending") belongs to `external-sorter` unless the task names the generator.
A task says what to check and points to where the
values are; it does not repeat them. Large files go to the ignored `data/` directory.

## 1. Shared command-line pieces

- [ ] 1.1 Move into `FileSort.Cli` the parser of sizes, the parser of whole numbers and the overwrite question with
  its result type, as D2 lists them; the generator uses them from there, and the checker parses `--threads` with the
  shared parser. Move their tests to `FileSort.Tests/Cli`. Verify: `dotnet build FileSort.slnx -c Release` succeeds
  with no warnings; the tests of the generator and the checker pass with no change other than namespaces, or with a
  message whose new wording is named in the task's report; the scenario "Pinned reference files" of
  `test-file-generator` passes; the check publishes with trimming of the generator and the checker report no
  warnings.
- [ ] 1.2 Update the row of `FileSort.Cli` in Repository layout of `README.md`, the Architecture rules of
  `CLAUDE.md`, and the Design decisions of `FileSort.Generator/README.md`, which call the size parser the
  generator's own. Verify: none of the three files has a statement about these parsers or `FileSort.Cli` that the
  move made false.

## 2. Generator: new edge cases

- [ ] 2.1 Implement the edge cases of D18, with the generator's own limit on the digits of a Number, its longer
  longest line and the new comment of `LineFormat.MaxNumberDigits`, and raise the content version; record the
  reference hashes of version 2 next to those of version 1. Verify: tests for every scenario of the modified
  requirements "Edge cases" and "Line ending" pass; the plain reference hash of version 2 equals that of version 1;
  every line of content generated with `--edge-cases all` and either line ending is valid for the naive parser of
  the tests and no longer than the generator's longest line; a generated pair of such content and its naive sort is
  `CORRECT` for the checker, in memory.
- [ ] 2.2 Update `FileSort.Generator/README.md` (the table of edge cases, the content version, the options table),
  the generator's part of How it works in `README.md`, and the two places of `FileSort.Checker/README.md` that say
  the generator cannot write such lines (Tests, and the section on line endings). Verify: the options table test
  passes; every category of the code has its row in the table; none of the three files says that the generator
  cannot write Numbers above 64 bits or Strings that end with a carriage return.

## 3. Engine and lines

- [ ] 3.1 Create `FileSort.Engine` as D2 describes, add it to `FileSort.slnx`, make its internals visible to the
  tests and the benchmarks, and add the test that the engine references no console code. Verify: the solution builds
  with no warnings and that test passes.
- [ ] 3.2 Give the naive sort of the tests the tie-break of D9. Verify: the scenario "Equal keys, different bytes"
  passes against the naive sort, and the tests of the checker still pass.
- [ ] 3.3 Implement the parsing of a line (D5). Verify: tests for every scenario of the requirements "Line
  structure", "Number" and "String" of `line-format` pass against the engine's parser, and it agrees with the naive
  parser on every line of content generated with `--edge-cases all` of version 2.
- [ ] 3.4 Implement the index entry and the comparison (D6), the bit operations as named methods. Verify: tests of
  the packing with worked examples and the long Number of D19; the comparison agrees with the naive comparison and
  the tie-break of D9 on the lines D19 lists; every scenario of `sort-order` passes; the JIT listing shows the named
  methods inlined in the comparison.
- [ ] 3.5 Add the benchmarks of M1 to `FileSort.Benchmarks`, run them, choose the sort routine by the rule of D7 and
  set the size of a segment. Create `FileSort.Sorter/README.md`, starting with its Performance section, and record
  the table and the choices there with the command that reproduces them. Verify: the README numbers match the
  BenchmarkDotNet report, the choice follows the rule, and one segment of the chosen size meets the bound of D7.

## 4. Chunks

- [ ] 4.1 Implement the chunk buffer and the reading of D4, with the line ending of D11 and the limit of D12.
  Describe the chunk buffer and the reading in How it works of `FileSort.Sorter/README.md`. Verify: tests for every
  scenario of the requirements "Line terminator" and "File structure" of `line-format` pass against the engine's
  reading; the tests of D19 about chunks pass, a chunk boundary at every position of a small input included; an
  input that is a device is refused.
- [ ] 4.2 Parse the ranges of a chunk on all threads and report the first problem of the file (D5), invalid line or
  line too long. Describe what is reported in `FileSort.Sorter/README.md`. Verify: an invalid line placed at every
  position of a small input, across chunk and range boundaries, is reported with the number and the offset that the
  naive parser gives it; the scenarios "Line without a separator", "Empty line", "Line longer than supported" and
  "Invalid line before a long line" of `external-sorter` hold for the engine.
- [ ] 4.3 Sort the segments of a chunk (D7) with the routine and the segment size chosen in 3.5, checking the
  cancellation token between segments. Verify: for the thread counts of D19 every sorted segment is in the order of
  the naive comparison with the tie-break of D9; a test finds the starts of the segments aligned as D7 says; the
  test of D19 that cancels a sort with one thread passes.

## 5. Writing a chunk

- [ ] 5.1 Implement the run storage of D17: the interface, the file version in the temporary folder of D14, and the
  version in memory in the tests. Verify: tests of both versions pass, and the temporary folder appears and
  disappears as the requirement "Temporary files" says.
- [ ] 5.2 Implement the block merge of D8: splitters with the strict order, ranges, blocks started strictly in
  order, the merge of a block, the writer in order, the window limited in bytes and the large blocks written by
  their worker. Describe it in How it works of `FileSort.Sorter/README.md`, with the example of D8. Verify: the
  tests of D19 about blocks pass; for every input of those tests the run equals the naive sort; a test that counts
  the bytes in flight never finds more than the window of D13.
- [ ] 5.3 Add the experiment of M2, run it and set the block size. Record the table and the choice in
  `FileSort.Sorter/README.md`. Verify: the documented command runs as written and the table matches its output.

## 6. Merge

- [ ] 6.1 Implement the run reader, the loser tree and the passes of D10, with the sizes of the groups, the deletion
  of merged runs and the check of free space before each group (D14).
  Describe the merge and its passes in How it works of
  `FileSort.Sorter/README.md`. Verify: the tests of D19 about the merge pass; the output of every case equals the
  naive sort; after every group only the runs that D10 keeps are left in the storage, and the group sizes are the
  ones D10 gives.

## 7. The whole sort

- [ ] 7.1 Implement the sort as D3 describes it: the two phases, the single chunk written to the output, the counters
  and the result of D17, cancellation and cleanup (D16), the free space check and the output file (D14). Verify: the
  tests of D19 about the whole sort in memory and about files pass; in-process tests of the engine pass for the
  scenarios of `external-sorter` in "Sorted output", "Lines with equal keys", "Line ending", "Byte order mark",
  "Invalid input", "Line
  length", "Temporary files", "Free space" and "Output file", as far as the engine is concerned; the checker,
  in-process, finds the outputs of the generated inputs `CORRECT`.
- [ ] 7.2 Run the mutation check of D19. Verify: each fault makes at least one test fail; the faults and the tests
  that caught them are listed in the Tests section of `FileSort.Sorter/README.md`.
- [ ] 7.3 Add the experiment of M3 and run it. Record the table in `FileSort.Sorter/README.md`. Verify: the
  documented command runs as written and the table matches its output.

## 8. Command line

- [ ] 8.1 Replace the placeholder of `FileSort.Sorter` with the command of D15 over the engine: the options, the
  same-file check of D14, the reserve of D13, the settings, the summary, the exit codes and messages of D16, the
  overwrite question and the cancellation. Verify: in-process tests pass for the scenarios of "Command line",
  "Parallelism", "Exit codes and error messages", "Cancellation", "Output file" and "Settings, progress and summary",
  except the drawing of the progress; and for "Lines with equal keys" the scenario "Same output for every setting"
  through the command line.
- [ ] 8.2 Add Spectre.Console to `Directory.Packages.props` and to `FileSort.Sorter` only, and draw the progress as
  D15 says. Verify: the scenario "Redirected error stream" passes; a run in a console shows the bars of both phases;
  `dotnet list FileSort.slnx package` shows Spectre.Console in `FileSort.Sorter` and nowhere else; the check
  publishes with trimming of the generator and the checker still report no warnings.
- [ ] 8.3 Write the rest of `FileSort.Sorter/README.md`: usage with examples, the options table typed by hand, what
  the output holds (lines kept byte for byte, the order of equal keys with the example of D9, line endings, the byte
  order mark), invalid input and the line limit, temporary files and free space, why the same file cannot be input
  and output (D14), memory, exit codes, how it works, design decisions, tests and limitations;
  add the options table test with the helper of the other programs.
  Verify: every command of the README runs as written on small files at the paths of its examples; the table test
  fails with a row removed and with a default changed (try both, then restore the table).

## 9. Measurements

- [ ] 9.1 State the total of D21 to the author and wait for the go-ahead. Publish the sorter as a
  framework-dependent build into `publish/sorter`, then run M4, M5, M6 and M7 with pauses between the runs, and check
  every output with the checker. Record the tables in `FileSort.Sorter/README.md` and the headline figures in the
  Performance section of `README.md`, and apply the decision rules of D21. Verify: the checker finds every output
  `CORRECT`; the scenarios "A file of 1 GB", "Peak memory for a file of 1 GB", "Input many times larger than the
  memory", "Many threads on few processors" and "Long lines" hold; the README numbers match the summaries of the
  runs.
- [ ] 9.2 Add the experiment of M8 and run it. Record the cost in `FileSort.Sorter/README.md`. Verify: the
  documented command runs as written and the number matches its output.
- [ ] 9.3 Run M9: generate the 5 GB file once, sort it, check the pair, and record the time of the sort in
  `FileSort.Sorter/README.md` and the time of the check in `FileSort.Checker/README.md`. Delete the 5 GB files
  afterwards. Verify: the scenario "A file of 5 GB" holds, and `data/` no longer holds
  the files of M9.
- [ ] 9.4 Check the scenario "Ctrl+C during the merge" once on the published sorter: a script sends a real console
  Ctrl+C event while the runs of a large input are merged. Verify: the exit code is the one the scenario requires,
  the cancellation is reported, and neither the output, nor its temporary file, nor the temporary folder is left.

## 10. Continuous integration and documentation

- [ ] 10.1 Add `sorter.yml` and update the path filters of `generator.yml` and `checker.yml` as D20 says. Verify:
  locally, the commands that `sorter.yml` runs pass: the filtered tests and the coverage check of the sorter's
  assemblies; the tests selected by the three filters together are all the tests of the project; a list of changed
  paths that touches only the sorter's tests matches the filters of `sorter.yml` and of neither other workflow.
- [ ] 10.2 Update `README.md`: the status note, Quick start step 3 and Build and run from source (the sorter's
  publish line and command), Repository layout (`FileSort.Engine`, `FileSort.Sorter`), the Assumptions rows about
  duplicate lines and malformed lines and a row for the line limit, How it works (Sorter), Verifying correctness,
  Code coverage, Design decisions (the engine library and where the line parser now lives, the exit code 3 that the
  sorter shares with the checker, Spectre.Console only in the sorter, what `--memory` limits), Continuous
  integration and Limitations. Update the Limitations of `FileSort.Checker/README.md`, which list the sorter's
  decisions as open, and the Design decisions and Limitations of `FileSort.Generator/README.md`, which leave the
  console framework and the policy on malformed input to the sorter. Update the Commands, the Architecture rules
  and the Workflow paragraph of `CLAUDE.md`. Verify: every command of the sorter in `README.md` and `CLAUDE.md` runs
  as written; none of the four files has a statement that this change made false.

## 11. Final check

- [ ] 11.1 Have the implementation reviewed by a reviewer without the context of its writing, against the specs of
  this change and this design; fix what it finds, and record every departure from this design in a section "Changes
  during implementation" at its end. Verify: every finding is fixed or answered in the report to the author.
- [ ] 11.2 Check the whole change. Verify: `dotnet build FileSort.slnx -c Release` and
  `dotnet test --solution FileSort.slnx -c Release` pass with no warnings; the coverage is measured, is at or above
  the rule of `CLAUDE.md` for every program, and the Code coverage table of `README.md` shows it, with a reason for
  every class under the threshold; `openspec validate add-external-sorter --strict` passes; the READMEs and
  `CLAUDE.md` contain no hardware model name or path of the development machine; the files left in `data/` are
  listed for the author; `git status --short` lists only files that belong to this change.

## Workflow follow-up

- The author reviews the implementation. On the author's command it is committed together with the archive of this
  change, as one commit after the commit of the plan.
- Archive the change with `/openspec-archive-change` after the review, before that commit.
- The first push runs `sorter.yml` for the first time; expect to fix something there.
- The second sorter change takes what D1 gives it. The check of 100 GB, which the plan of the checker left to the
  sorter's 100 GB run, moves with that run to the second change, and the spec `output-checker` gets its scenario
  then.
