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
Write-Host "`n[1/7] Pester tests..." -ForegroundColor Cyan
Invoke-Pester tests/ -CI
if ($LASTEXITCODE -ne 0) { throw "Pester tests failed" }

# 2. dotnet build (solution includes Bootstrapper now)
Write-Host "`n[2/7] dotnet build (Release)..." -ForegroundColor Cyan
dotnet build src/GraceKeeper.sln -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

# 3. xUnit tests
Write-Host "`n[3/7] xUnit tests..." -ForegroundColor Cyan
dotnet test src/GraceKeeper.Core.Tests/GraceKeeper.Core.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "xUnit tests failed" }

# 4. Stage the AHK runtime
Write-Host "`n[4/7] Staging AutoHotkey64.exe..." -ForegroundColor Cyan
$ahkSrc = "C:\Program Files\AutoHotkey\v2\AutoHotkey64.exe"
$staging = "$repoRoot\installer\staging"
if (-not (Test-Path $ahkSrc)) {
    throw "AutoHotkey64.exe not found at $ahkSrc. Install AutoHotkey v2 (see docs/developer-setup.md)."
}
if (-not (Test-Path $staging)) { New-Item -ItemType Directory -Path $staging -Force | Out-Null }
Copy-Item $ahkSrc "$staging\AutoHotkey64.exe" -Force

# 5. Build the inner MSI to bin/dev/
Write-Host "`n[5/7] Building inner MSI..." -ForegroundColor Cyan
$msiStaging = "$repoRoot\installer\bin\dev"
if (-not (Test-Path $msiStaging)) { New-Item -ItemType Directory -Path $msiStaging -Force | Out-Null }
$msi = "$msiStaging\GraceKeeper-$Version.msi"
Push-Location "$repoRoot\installer"
try {
    wix build Product.wxs -d Version=$Version -ext WixToolset.Util.wixext -arch x64 -o $msi
    if ($LASTEXITCODE -ne 0) { throw "MSI build failed" }
} finally { Pop-Location }

# 6. Build the bundle
Write-Host "`n[6/7] Building Burn bundle..." -ForegroundColor Cyan
$exe = "$repoRoot\GraceKeeper-$Version.exe"
# Locate mbanative.dll (managed BA native host) from NuGet cache
$mbanative = "$env:USERPROFILE\.nuget\packages\wixtoolset.mba.core\4.0.6\runtimes\win-x64\native\mbanative.dll"
if (-not (Test-Path $mbanative)) {
    throw "mbanative.dll not found at '$mbanative'. Ensure WixToolset.Mba.Core 4.0.6 is restored (dotnet restore src\GraceKeeper.sln)."
}
Push-Location "$repoRoot\installer"
try {
    wix build Bundle.wxs -d Version=$Version -d mbanative=$mbanative -ext WixToolset.Util.wixext -ext WixToolset.Bal.wixext -arch x64 -o $exe
    if ($LASTEXITCODE -ne 0) { throw "Bundle build failed" }
} finally { Pop-Location }

# 7. Summary
Write-Host "`n[7/7] Build complete." -ForegroundColor Green
$exeInfo = Get-Item $exe
Write-Host "Bundle: $($exeInfo.FullName)"
Write-Host "Size:   $([math]::Round($exeInfo.Length / 1MB, 2)) MB"
Write-Host "`nTo install on this machine:"
Write-Host "  .\GraceKeeper-$Version.exe"
