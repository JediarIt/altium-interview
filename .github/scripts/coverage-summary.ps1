# Turns a Cobertura coverage report into a Markdown summary for the page of a GitHub Actions run, and fails when the
# line coverage of the whole solution is below the threshold. Run it locally the same way:
#
#   ./.github/scripts/coverage-summary.ps1 -Report TestResults/coverage.cobertura.xml
#
# Without GITHUB_STEP_SUMMARY in the environment the summary goes to the console.
param(
    [Parameter(Mandatory = $true)][string]$Report,
    [double]$Threshold = 80
)

$ErrorActionPreference = 'Stop'

function Format-Rate([double]$rate) {
    # The invariant culture: the same text on a runner and on a machine with another decimal mark.
    return ($rate * 100).ToString('N1', [cultureinfo]::InvariantCulture) + '%'
}

[xml]$xml = Get-Content -LiteralPath $Report -Raw
$coverage = $xml.coverage

$lines = @()
$lines += '### Code coverage'
$lines += ''
$lines += '| Assembly | Lines | Branches |'
$lines += '|---|---|---|'
foreach ($package in $coverage.packages.package) {
    $lines += ('| `{0}` | {1} | {2} |' -f $package.name, (Format-Rate $package.'line-rate'), (Format-Rate $package.'branch-rate'))
}
$lines += ('| **All** | **{0}** ({1} of {2}) | **{3}** ({4} of {5}) |' -f `
    (Format-Rate $coverage.'line-rate'), $coverage.'lines-covered', $coverage.'lines-valid', `
    (Format-Rate $coverage.'branch-rate'), $coverage.'branches-covered', $coverage.'branches-valid')

# Classes under the threshold are listed, not enforced: the README says why each of them is not covered by tests.
# Compiler-generated classes (closures, state machines) have '<' in their names and are left out.
$low = @($coverage.packages.package.classes.class |
    Where-Object { [double]$_.'line-rate' * 100 -lt $Threshold -and $_.name -notmatch '<' } |
    Sort-Object name)
if ($low.Count -gt 0) {
    $lines += ''
    $lines += ('Classes under {0}% of lines (each needs a reason in the README):' -f $Threshold)
    $lines += ''
    foreach ($class in $low) {
        $lines += ('- `{0}`: {1}' -f $class.name, (Format-Rate $class.'line-rate'))
    }
}

$total = [double]$coverage.'line-rate' * 100
$passed = $total -ge $Threshold
$lines += ''
$lines += if ($passed) {
    ('The line coverage is at or above the threshold of {0}%.' -f $Threshold)
} else {
    ('**The line coverage is below the threshold of {0}%.**' -f $Threshold)
}

$text = $lines -join "`n"
if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $text -Encoding utf8
} else {
    Write-Output $text
}

if (-not $passed) {
    # The "::error::" form makes GitHub show the message as an annotation of the run.
    Write-Output ('::error title=Code coverage::Line coverage {0} is below the threshold of {1}%.' -f (Format-Rate ($total / 100)), $Threshold)
    exit 1
}
