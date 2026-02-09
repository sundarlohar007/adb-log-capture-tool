$ErrorActionPreference = 'Stop'

Write-Host 'Git status...' -ForegroundColor Cyan
git status --short
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Searching docs/scripts references...' -ForegroundColor Cyan
$targets = @('README.md', 'scripts/Validate-UiScaffold.ps1')
$pattern = 'Windows PowerShell Validation|Developer PowerShell|Validate-UiScaffold'
foreach ($t in $targets) {
    if (-not (Test-Path $t)) {
        Write-Error "Missing file: $t"
        exit 1
    }
    Select-String -Path $t -Pattern $pattern | ForEach-Object {
        "{0}:{1}:{2}" -f $_.Path, $_.LineNumber, $_.Line.Trim()
    }
}

Write-Host 'README preview lines 30-120...' -ForegroundColor Cyan
$readme = Get-Content 'README.md'
$start = 30
$end = [Math]::Min(120, $readme.Count)
for ($i = $start; $i -le $end; $i++) {
    "{0,4}: {1}" -f $i, $readme[$i - 1]
}

Write-Host 'Git status (post-check)...' -ForegroundColor Cyan
git status --short
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Developer PowerShell validation completed.' -ForegroundColor Green
