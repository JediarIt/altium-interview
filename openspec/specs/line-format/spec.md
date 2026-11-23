# line-format Specification

## Purpose

Defines the `Number. String` text format that the generator writes and that the sorter and the checker read: what
makes a line and a file valid, and how a new line is written. A scenario that does not name a line ending is about
a file read with the line ending `\n`. How a program learns the line ending of a file it reads is defined by the
spec of that program.

## Requirements

### Requirement: Line structure
A line SHALL be valid only if it consists of a Number, the two-byte separator `. ` (a period followed by one space)
and a String, in that order. The separator is the first occurrence of `. ` in the line. Everything after it, up to
the line terminator, is the String.

#### Scenario: Plain line
- **WHEN** a line is `415. Apple`
- **THEN** it is valid, its Number is 415 and its String is `Apple`

#### Scenario: Separator inside the String
- **WHEN** a line is `12. 3. Apple`
- **THEN** it is valid, its Number is 12 and its String is `3. Apple`

#### Scenario: Extra space after the separator
- **WHEN** a line is `5.  Apple` with two spaces after the period
- **THEN** it is valid and its String is ` Apple`, starting with a space

#### Scenario: No separator
- **WHEN** a line is `Apple`, `12.Apple` or `12 Apple`
- **THEN** it is not valid

### Requirement: Number
The Number SHALL be one or more ASCII decimal digits. Neither the count of digits nor the value is limited. Leading
zeros SHALL be valid and SHALL NOT change the value. A sign, a space or any other character in the Number makes the
line invalid.

#### Scenario: Zero
- **WHEN** a line is `0. Apple`
- **THEN** it is valid and its Number is 0

#### Scenario: Largest Number
- **WHEN** a line is `18446744073709551615. Apple`, whose Number is the largest that fits in 64 bits
- **THEN** it is valid

#### Scenario: Number above the range
- **WHEN** a line is `18446744073709551616. Apple`, whose Number is above the range of 64 bits
- **THEN** it is valid, and its Number is greater by one than the Number of `18446744073709551615. Apple`

#### Scenario: Leading zeros
- **WHEN** a line is `007. Apple`
- **THEN** it is valid and its Number is 7, the same value as in `7. Apple`

#### Scenario: More than 20 digits
- **WHEN** a line is `000000000000000000007. Apple`, whose Number has 21 digits, or
  `1234567890123456789012345678901234567890. Apple`, whose Number has 40 digits
- **THEN** both are valid, and the Number of the first is 7

#### Scenario: Sign, space or missing digits
- **WHEN** a line is `-5. Apple`, `+5. Apple`, ` 5. Apple` or `. Apple`
- **THEN** it is not valid

### Requirement: String
The String SHALL be zero or more bytes of well-formed UTF-8 that contain no line feed. An empty String SHALL be
valid, and so SHALL a String that contains `. `. The bytes of the String are significant exactly as written: letter
case and leading or trailing spaces are part of it.

#### Scenario: Empty String
- **WHEN** a line is `5. ` and the line terminator follows the separator immediately
- **THEN** it is valid and its String has zero bytes

#### Scenario: Non-ASCII String
- **WHEN** a line is `5. Ёлка 日本 😀`
- **THEN** it is valid

#### Scenario: Case and spaces are significant
- **WHEN** three lines are `5. Apple`, `5. apple` and `5. Apple ` with a space at the end
- **THEN** their three Strings are different

#### Scenario: Ill-formed UTF-8
- **WHEN** the String of a line contains the bytes `C3 28`, which are not well-formed UTF-8
- **THEN** the line is not valid

### Requirement: Line terminator
A file SHALL be read with one line ending for all its lines: a line feed (`\n`), or a carriage return followed by a
line feed (`\r\n`). Only that terminator ends a line; it follows the line and is not part of it, and the last line
of a file SHALL be valid without it. Whether a line or a file is valid is judged for that line ending. A carriage
return that is not part of a terminator gets no special treatment: in a String it is one of its bytes, also at its
end.

#### Scenario: Line feed
- **WHEN** a file read with the line ending `\n` contains the bytes `1. Apple\n2. Banana\n`
- **THEN** it has two lines whose Strings are `Apple` and `Banana`

#### Scenario: Carriage return and line feed
- **WHEN** a file read with the line ending `\r\n` contains the bytes `1. Apple\r\n2. Banana\r\n`
- **THEN** it has two lines whose Strings are `Apple` and `Banana`, without the carriage return

#### Scenario: Last line without a terminator
- **WHEN** a file contains the bytes `1. Apple\n2. Banana`
- **THEN** it has two valid lines

#### Scenario: Carriage return inside a String
- **WHEN** a file contains the bytes `1. A\rB\n`
- **THEN** it has one line whose String is three bytes: `A`, a carriage return and `B`

#### Scenario: Carriage return at the end of a String
- **WHEN** a file read with the line ending `\n` contains the bytes `1. Apple\r\n`
- **THEN** it has one valid line whose String is six bytes: `Apple` and a carriage return

#### Scenario: Carriage return before the terminator
- **WHEN** a file read with the line ending `\r\n` contains the bytes `1. Apple\r\r\n`
- **THEN** it has one valid line whose String is six bytes: `Apple` and a carriage return

#### Scenario: Line feed without a carriage return
- **WHEN** a file read with the line ending `\r\n` contains the bytes `1. Apple\r\n2. Banana\n3. Cherry\r\n`
- **THEN** its second line is `2. Banana\n3. Cherry`, which is not valid because its String contains a line feed

### Requirement: File structure
A valid file SHALL be a sequence of zero or more valid lines encoded as UTF-8. It MAY start with the UTF-8 byte order
mark, the bytes `EF BB BF`, which is not part of the first line. Anywhere else these bytes get no special treatment.

#### Scenario: Empty file
- **WHEN** a file has zero bytes
- **THEN** it is a valid file with zero lines

#### Scenario: Empty line
- **WHEN** a file contains the bytes `1. Apple\n\n2. Banana\n`
- **THEN** its second line is empty and therefore not valid

#### Scenario: Byte order mark at the start
- **WHEN** a file contains the bytes `EF BB BF` followed by `1. Apple\n`
- **THEN** it is a valid file with one line whose Number is 1 and whose String is `Apple`

#### Scenario: Byte order mark and nothing else
- **WHEN** a file contains only the bytes `EF BB BF`
- **THEN** it is a valid file with zero lines

#### Scenario: Byte order mark at the start of a later line
- **WHEN** a file contains `1. Apple\n` followed by the bytes `EF BB BF` and `2. Banana\n`
- **THEN** its second line is not valid, because its Number does not start with a digit

### Requirement: Canonical form of a written line
A program that creates a line from a Number value and a String, rather than copying an existing line, SHALL write it
in canonical form unless another requirement states otherwise: the Number as decimal digits with no leading zeros,
the separator, the bytes of the String unchanged, and the terminator, which is the same for every line of a file.

#### Scenario: Writing a line
- **WHEN** a line is written from the Number 415 and the String `Apple` with the terminator `\n`
- **THEN** the bytes written are `34 31 35 2E 20 41 70 70 6C 65 0A`

#### Scenario: Writing zero
- **WHEN** a line is written from the Number 0
- **THEN** the Number is written as the single digit `0`

#### Scenario: Writing the largest Number
- **WHEN** a line is written from the Number 18446744073709551615, the largest that fits in 64 bits
- **THEN** the Number is written as those 20 digits
