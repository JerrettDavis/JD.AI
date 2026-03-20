param(
    [string]$CoberturaPath = "coverage-report/Cobertura.xml",
    [int]$Top = 25
)

if (-not (Test-Path $CoberturaPath)) {
    Write-Error "Coverage file not found: $CoberturaPath"
    exit 1
}

$xml = [xml](Get-Content $CoberturaPath)

$rows = foreach ($package in $xml.coverage.packages.package) {
    foreach ($class in $package.classes.class) {
        $lines = @($class.lines.line)
        if ($lines.Count -eq 0) { continue }

        $valid = $lines.Count
        $covered = ($lines | Where-Object { [int]$_.hits -gt 0 }).Count
        $uncovered = $valid - $covered
        $pct = [math]::Round(($covered / $valid) * 100, 1)

        [pscustomobject]@{
            Assembly     = $package.name
            Class        = $class.name
            File         = $class.filename
            LineCoverage = $pct
            Uncovered    = $uncovered
            Valid        = $valid
        }
    }
}

Write-Host "Top $Top low-coverage classes by line coverage:"
$rows |
    Sort-Object @{ Expression = "LineCoverage"; Ascending = $true }, @{ Expression = "Uncovered"; Descending = $true } |
    Select-Object -First $Top |
    Format-Table -AutoSize

Write-Host ""
Write-Host "Assembly coverage:"
($rows |
    Group-Object Assembly |
    ForEach-Object {
        $valid = ($_.Group | Measure-Object -Property Valid -Sum).Sum
        $uncovered = ($_.Group | Measure-Object -Property Uncovered -Sum).Sum
        $covered = $valid - $uncovered
        [pscustomobject]@{
            Assembly = $_.Name
            LineCoverage = if ($valid -gt 0) { [math]::Round(($covered / $valid) * 100, 1) } else { 0.0 }
            Covered = $covered
            Valid = $valid
            Uncovered = $uncovered
        }
    } |
    Sort-Object LineCoverage |
    Format-Table -AutoSize)

