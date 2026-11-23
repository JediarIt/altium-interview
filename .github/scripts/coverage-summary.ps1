# Turns a Cobertura coverage report into a Markdown summary for the page of a GitHub Actions run, and fails when the
# line coverage of the counted assemblies, taken together, is below the threshold. Run it locally the same way:
#
#   ./.github/scripts/coverage-summary.ps1 -Report TestResults/coverage.cobertura.xml
#   ./.github/scripts/coverage-summary.ps1 -Report TestResults/coverage.cobertura.xml -Assemblies FileSort.Checker,FileSort.Cli
#
# -Assemblies names the assemblies to count: the ones of the program whose tests were run. Without it every assembly
# of the report is counted. Without GITHUB_STEP_SUMMARY in the environment the summary goes to the console.
param(
    [Parameter(Mandatory = $true)][string]$Report,
    [double]$Threshold = 80,
    [string[]]$Assemblies = @()
)

$ErrorActionPreference = 'Stop'

function Format-Rate([double]$covered, [double]$valid) {
    # The invariant culture: the same text on a runner and on a machine with another decimal mark.
    $rate = if ($valid -gt 0) { $covered / $valid } else { 1 }
    return ($rate * 100).ToString('N1', [cultureinfo]::InvariantCulture) + '%'
}

# The lines and the branches of one assembly, counted from the lines of its classes: the report gives totals only
# for all assemblies together.
function Measure-Package($package) {
    $lines = @($package.classes.class | ForEach-Object { $_.lines.line } | Where-Object { $_ })
    $branchesCovered = 0
    $branchesValid = 0
    foreach ($line in $lines) {
        # A line with branches says how many of them ran: condition-coverage="50% (1/2)".
        if ($line.'condition-coverage' -match '\((\d+)/(\d+)\)') {
            $branchesCovered += [int]$Matches[1]
            $branchesValid += [int]$Matches[2]
        }
    }

    return [pscustomobject]@{
        Name            = $package.name
        LinesCovered    = @($lines | Where-Object { [int]$_.hits -gt 0 }).Count
        LinesValid      = $lines.Count
        BranchesCovered = $branchesCovered
        BranchesValid   = $branchesValid
    }
}

[xml]$xml = Get-Content -LiteralPath $Report -Raw
$packages = @($xml.coverage.packages.package)

# The names may arrive as one argument with commas: -Assemblies A,B from a workflow is one string.
$counted = @($Assemblies | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($counted.Count -gt 0) {
    $missing = @($counted | Where-Object { $_ -notin $packages.name })
    if ($missing.Count -gt 0) {
        Write-Output ('::error title=Code coverage::The report has no assembly named {0}.' -f ($missing -join ', '))
        exit 1
    }

    $packages = @($packages | Where-Object { $_.name -in $counted })
}

$measured = @($packages | ForEach-Object { Measure-Package $_ })
$linesCovered = ($measured | Measure-Object LinesCovered -Sum).Sum
$linesValid = ($measured | Measure-Object LinesValid -Sum).Sum
$branchesCovered = ($measured | Measure-Object BranchesCovered -Sum).Sum
$branchesValid = ($measured | Measure-Object BranchesValid -Sum).Sum

$lines = @()
$lines += '### Code coverage'
$lines += ''
$lines += '| Assembly | Lines | Branches |'
$lines += '|---|---|---|'
foreach ($package in $measured) {
    $lines += ('| `{0}` | {1} | {2} |' -f $package.Name,
        (Format-Rate $package.LinesCovered $package.LinesValid),
        (Format-Rate $package.BranchesCovered $package.BranchesValid))
}
$lines += ('| **All** | **{0}** ({1} of {2}) | **{3}** ({4} of {5}) |' -f `
    (Format-Rate $linesCovered $linesValid), $linesCovered, $linesValid, `
    (Format-Rate $branchesCovered $branchesValid), $branchesCovered, $branchesValid)

# Classes under the threshold are listed, not enforced: the README says why each of them is not covered by tests.
# Compiler-generated classes (closures, state machines) have '<' in their names and are left out.
$low = @($packages.classes.class |
    Where-Object { [double]$_.'line-rate' * 100 -lt $Threshold -and $_.name -notmatch '<' } |
    Sort-Object name)
if ($low.Count -gt 0) {
    $lines += ''
    $lines += ('Classes under {0}% of lines (each needs a reason in the README):' -f $Threshold)
    $lines += ''
    foreach ($class in $low) {
        $rate = ([double]$class.'line-rate' * 100).ToString('N1', [cultureinfo]::InvariantCulture) + '%'
        $lines += ('- `{0}`: {1}' -f $class.name, $rate)
    }
}

$total = if ($linesValid -gt 0) { $linesCovered / $linesValid * 100 } else { 100 }
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
    Write-Output ('::error title=Code coverage::Line coverage {0} is below the threshold of {1}%.' -f (Format-Rate $linesCovered $linesValid), $Threshold)
    exit 1
}
