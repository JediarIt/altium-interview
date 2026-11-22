# Spec Delta

## MODIFIED Requirements

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
