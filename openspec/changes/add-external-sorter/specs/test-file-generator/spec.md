# Spec Delta

## MODIFIED Requirements

### Requirement: Edge cases
`--edge-cases <list>` SHALL add valid but unusual lines to the output. The list names categories, separated by
commas: `unicode`, `separator`, `case`, `big-numbers`, `leading-zeros`, `empty` and `carriage-return`; `all` enables
every one. With the default `--words` and `--duplicates`, each enabled category SHALL affect at least 1% of the lines
of any output of 1 MB or more. A category that is not enabled SHALL NOT appear in the output.

#### Scenario: Category unicode
- **WHEN** a file of 1 MB is generated with `--edge-cases unicode`
- **THEN** its Strings contain non-ASCII characters that UTF-8 encodes with 2, 3 and 4 bytes. Among them are U+FF21
  and U+1F600, a pair that orders differently by UTF-8 bytes and by UTF-16 code units, and the letter U+0439 in both
  forms, precomposed and decomposed (U+0438 U+0306)

#### Scenario: Category separator
- **WHEN** a file of 1 MB is generated with `--edge-cases separator`
- **THEN** some Strings contain `. `, some begin with digits followed by `. `, and some begin with a space

#### Scenario: Category case
- **WHEN** a file of 1 MB is generated with `--edge-cases case`
- **THEN** dictionary words occur in lower case, capitalized and in upper case, in any position: some Strings begin
  with a lower-case ASCII letter, and some have an upper-case ASCII letter after the first word

#### Scenario: Category big-numbers
- **WHEN** a file of 1 MB is generated with `--edge-cases big-numbers`
- **THEN** Numbers greater than 4294967295 occur, among them 4294967296, 9223372036854775807, 9223372036854775808,
  18446744073709551615 and 18446744073709551616, and so do Numbers of more than 20 digits

#### Scenario: Category leading-zeros
- **WHEN** a file of 1 MB is generated with `--edge-cases leading-zeros`
- **THEN** some Numbers have two or more digits and begin with `0`

#### Scenario: Category empty
- **WHEN** a file of 1 MB is generated with `--edge-cases empty`
- **THEN** some lines end right after the separator

#### Scenario: Category carriage-return
- **WHEN** a file of 1 MB is generated with `--edge-cases carriage-return`
- **THEN** some Strings end with a carriage return, and the String of the first line does not

#### Scenario: All categories
- **WHEN** a file of 1 MB is generated with `--edge-cases all`
- **THEN** lines of every category occur, each category on at least 1% of the lines

#### Scenario: Selected categories only
- **WHEN** a file of 1 MB is generated with `--edge-cases empty,leading-zeros`
- **THEN** lines of both categories occur, and no line of any other category occurs

### Requirement: Line ending
`--line-ending` SHALL accept `lf` (the default on every operating system) or `crlf` and SHALL set the line ending of
the output, `\n` or `\r\n`, in the sense of the `line-format` capability. A carriage return SHALL appear in the output
only as part of a `\r\n` terminator or as the last byte of a String of the category `carriage-return`, which the
first line never has, so the line ending of a generated file can be told from its first line.

#### Scenario: Default on Windows
- **WHEN** a file is generated on Windows without `--line-ending` and without the category `carriage-return`
- **THEN** the output contains no carriage return

#### Scenario: Carriage return and line feed
- **WHEN** a file is generated with `--line-ending crlf` and without the category `carriage-return`
- **THEN** every line is followed by `\r\n`, no line feed appears without a carriage return before it, and no
  carriage return appears without a line feed after it

#### Scenario: Line ending of the first line
- **WHEN** a file is generated with `--edge-cases carriage-return`, once with `--line-ending lf` and once with
  `--line-ending crlf`
- **THEN** the first line of the first file ends with a line feed that has no carriage return before it, and the
  first line of the second file ends with `\r\n` preceded by a byte that is not a carriage return
