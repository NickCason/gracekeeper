#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Version = "0.0.0-local"
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "=== GraceKeeper local build ===" -ForegroundColor Cyan
Write-Host "Version: $Version"

# 1. Pester tests
Write-Host "`n[1/6] Pester tests..." -ForegroundColor Cyan
Invoke-Pester tests/ -CI
if ($LASTEXITCODE -ne 0) { throw "Pester tests failed" }

# 2. dotnet build
Write-Host "`n[2/6] dotnet build (Release)..." -ForegroundColor Cyan
dotnet build src/GraceKeeper.sln -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

# 3. xUnit tests
Write-Host "`n[3/6] xUnit tests..." -ForegroundColor Cyan
dotnet test src/GraceKeeper.Core.Tests/GraceKeeper.Core.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "xUnit tests failed" }

# 4. Stage the AHK runtime
Write-Host "`n[4/6] Staging AutoHotkey64.exe..." -ForegroundColor Cyan
$ahkSrc = "C:\Program Files\AutoHotkey\v2\AutoHotkey64.exe"
$staging = "$repoRoot\installer\staging"
if (-not (Test-Path $ahkSrc)) {
    throw "AutoHotkey64.exe not found at $ahkSrc. Install AutoHotkey v2 (see docs/developer-setup.md)."
}
if (-not (Test-Path $staging)) { New-Item -ItemType Directory -Path $staging -Force | Out-Null }
Copy-Item $ahkSrc "$staging\AutoHotkey64.exe" -Force

# 5. Package the MSI
Write-Host "`n[5/6] Packaging MSI..." -ForegroundColor Cyan
$msi = "$repoRoot\GraceKeeper-$Version.msi"
Push-Location "$repoRoot\installer"
try {
    wix build Product.wxs -d Version=$Version -ext WixToolset.Util.wixext -ext WixToolset.UI.wixext -arch x64 -o $msi
    if ($LASTEXITCODE -ne 0) { throw "WiX build failed" }
} finally {
    Pop-Location
}

# 6. Summary
Write-Host "`n[6/6] Build complete." -ForegroundColor Green
$msiInfo = Get-Item $msi
Write-Host "MSI:    $($msiInfo.FullName)"
Write-Host "Size:   $([math]::Round($msiInfo.Length / 1MB, 2)) MB"
Write-Host "`nTo install on this machine:"
Write-Host "  msiexec /i `"$($msiInfo.FullName)`" /L*v install.log"
