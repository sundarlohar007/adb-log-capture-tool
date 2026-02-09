$ErrorActionPreference = 'Stop'

Write-Host 'Checking working tree status...' -ForegroundColor Cyan
git status --short
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Checking theme directory...' -ForegroundColor Cyan
if (-not (Test-Path 'MobileDebugTool/UI/Theme')) {
    Write-Error 'Missing MobileDebugTool/UI/Theme directory.'
    exit 1
}
Get-ChildItem -Path 'MobileDebugTool/UI/Theme' -File | Select-Object -ExpandProperty FullName

Write-Host 'Previewing MainWindow.xaml...' -ForegroundColor Cyan
Get-Content 'MobileDebugTool/UI/MainWindow.xaml' -TotalCount 220

Write-Host 'Previewing App.xaml...' -ForegroundColor Cyan
Get-Content 'MobileDebugTool/App.xaml' -TotalCount 220

Write-Host 'Validation checks completed.' -ForegroundColor Green
