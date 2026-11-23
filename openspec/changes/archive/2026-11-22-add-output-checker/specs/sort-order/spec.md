# Spec Delta

## Purpose

Defines the order of lines that the sorter produces and the checker verifies: lines of the `line-format` format are
ordered by String first and by Number second. A sequence of lines is sorted when, for every two neighbouring lines,
the second one does not come before the first one in this order.

## ADDED Requirements

### Requirement: Order by String
Lines SHALL be ordered by their Strings first. Two Strings are compared byte by byte, as unsigned bytes of their
UTF-8 encoding: at the first byte that differs, the String with the smaller byte comes first, and a String that is
the beginning of a longer one comes first. Nothing else takes part in the comparison: no culture, no letter case
folding, no Unicode normalization and no trimming.

#### Scenario: Different Strings
- **WHEN** the lines `30432. Something something something`, `32. Cherry is the best` and `2. Banana is yellow` are
  sorted
- **THEN** their order is `2. Banana is yellow`, `32. Cherry is the best`, `30432. Something something something`

#### Scenario: Beginning of a longer String
- **WHEN** two lines are `5. Apple pie` and `9. Apple`
- **THEN** `9. Apple` comes first

#### Scenario: Empty String
- **WHEN** two lines are `5. Apple` and `9. `, whose String is empty
- **THEN** `9. ` comes first

#### Scenario: Letter case
- **WHEN** two lines are `1. apple` and `1. Banana`
- **THEN** `1. Banana` comes first, because the byte of `B` is smaller than the byte of `a`

#### Scenario: Trailing space
- **WHEN** two lines are `1. Apple ` with a space at the end and `1. Apple`
- **THEN** `1. Apple` comes first, and the two Strings are not equal

#### Scenario: UTF-8 bytes, not UTF-16 code units
- **WHEN** the String of one line is the character U+FF21 (bytes `EF BC A1`) and the String of another is the
  character U+1F600 (bytes `F0 9F 98 80`)
- **THEN** the line with U+FF21 comes first, although the UTF-16 code units of U+1F600 are smaller

#### Scenario: No normalization
- **WHEN** the String of one line is U+0439 (bytes `D0 B9`) and the String of another is U+0438 U+0306 (bytes
  `D0 B8 CC 86`), which looks the same on screen
- **THEN** the Strings are not equal, and the line with U+0438 U+0306 comes first

### Requirement: Order by Number
Lines with equal Strings SHALL be ordered by the numeric value of their Numbers, the smaller value first. The
Number takes part only when the Strings are equal. Leading zeros do not change the value, and neither the count of
digits nor the value is limited.

#### Scenario: Value, not text
- **WHEN** two lines are `10. Apple` and `9. Apple`
- **THEN** `9. Apple` comes first

#### Scenario: Leading zeros
- **WHEN** three lines are `8. Apple`, `007. Apple` and `6. Apple`
- **THEN** their order is `6. Apple`, `007. Apple`, `8. Apple`

#### Scenario: Numbers above 64 bits
- **WHEN** three lines are `18446744073709551616. Apple`, `100000000000000000000000000000. Apple` and
  `18446744073709551615. Apple`
- **THEN** their order is `18446744073709551615. Apple`, `18446744073709551616. Apple`,
  `100000000000000000000000000000. Apple`

#### Scenario: String first, then Number
- **WHEN** two lines are `1. Banana` and `999. Apple`
- **THEN** `999. Apple` comes first

#### Scenario: Example of the task
- **WHEN** the lines `415. Apple`, `30432. Something something something`, `1. Apple`, `32. Cherry is the best` and
  `2. Banana is yellow` are sorted
- **THEN** their order is `1. Apple`, `415. Apple`, `2. Banana is yellow`, `32. Cherry is the best`,
  `30432. Something something something`

### Requirement: Lines with equal keys
Two lines have equal keys when their Strings are equal and their Numbers have the same value. Such lines are either
identical or differ only in the leading zeros of their Numbers. A sequence of lines SHALL count as sorted whatever
the order of lines with equal keys is.

#### Scenario: Identical lines
- **WHEN** the line `7. Apple` occurs three times
- **THEN** in a sorted sequence no line with a different key stands between them

#### Scenario: Equal keys, different bytes
- **WHEN** two lines are `007. Apple` and `7. Apple`
- **THEN** both `007. Apple`, `7. Apple` and `7. Apple`, `007. Apple` are sorted
