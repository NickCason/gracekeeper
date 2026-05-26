#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Version = "0.0.0-local"
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

# Reject any version that isn't a clean 4-octet numeric. MSI MajorUpgrade
# can't compare versions like "0.3.0.3-logontask" reliably (WiX warns it's
# undefined behavior), and that mismatch is what forces users to uninstall
# manually before upgrading. Bumping a release with a label silently breaks
# in-place upgrades; this guard makes that mistake impossible at build time.
if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "Version '$Version' must be N.N.N or N.N.N.N (digits only). Labels like '-logontask' break MSI MajorUpgrade. Use a clean numeric version for shippable builds."
}

Set-Location $repoRoot

Write-Host "=== GraceKeeper local build ===" -ForegroundColor Cyan
Write-Host "Version: $Version"

# 1. dotnet build (solution includes Bootstrapper now)
# -p:Version flows $Version into AssemblyVersion + FileVersion on every project,
# so the dashboard's UI binding (DashboardViewModel.Version) and the file's
# Get-Item VersionInfo both report the release version instead of the SDK
# default 1.0.0.0.
Write-Host "`n[1/6] dotnet build (Release)..." -ForegroundColor Cyan
dotnet build src/GraceKeeper.sln -c Release -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

# 2. xUnit tests
Write-Host "`n[2/6] xUnit tests..." -ForegroundColor Cyan
dotnet test src/GraceKeeper.Core.Tests/GraceKeeper.Core.Tests.csproj -c Release --no-build
if ($LASTEXITCODE -ne 0) { throw "xUnit tests failed" }

# 3. Stage the AHK runtime
Write-Host "`n[3/6] Staging AutoHotkey64.exe..." -ForegroundColor Cyan
$staging = "$repoRoot\installer\staging"
if (-not (Test-Path $staging)) { New-Item -ItemType Directory -Path $staging -Force | Out-Null }
$staged = "$staging\AutoHotkey64.exe"
$ahkCandidates = @(
    "C:\Program Files\AutoHotkey\v2\AutoHotkey64.exe",
    "C:\Program Files\AutoHotkey\AutoHotkey64.exe"
)
$ahkSrc = $ahkCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($ahkSrc) {
    Copy-Item $ahkSrc $staged -Force
} elseif (-not (Test-Path $staged)) {
    throw "AutoHotkey64.exe not found in standard paths and no prior staged copy at $staged. Install AutoHotkey v2 (see docs/developer-setup.md)."
} else {
    Write-Host "  (using existing staged copy at $staged)"
}

# 4. Build the inner MSI to bin/dev/
Write-Host "`n[4/6] Building inner MSI..." -ForegroundColor Cyan
$msiStaging = "$repoRoot\installer\bin\dev"
if (-not (Test-Path $msiStaging)) { New-Item -ItemType Directory -Path $msiStaging -Force | Out-Null }
$msi = "$msiStaging\GraceKeeper-$Version.msi"
Push-Location "$repoRoot\installer"
try {
    wix build Product.wxs -d Version=$Version -ext WixToolset.Util.wixext -arch x64 -o $msi
    if ($LASTEXITCODE -ne 0) { throw "MSI build failed" }
} finally { Pop-Location }

# 5. Build the bundle
Write-Host "`n[5/6] Building Burn bundle..." -ForegroundColor Cyan
$exe = "$repoRoot\GraceKeeper-$Version.exe"
# Locate mbanative.dll (managed BA native shim) in the NuGet cache; payloaded into the bundle.
$mbanative = "$env:USERPROFILE\.nuget\packages\wixtoolset.mba.core\4.0.6\runtimes\win-x64\native\mbanative.dll"
if (-not (Test-Path $mbanative)) {
    throw "mbanative.dll not found at '$mbanative'. Ensure WixToolset.Mba.Core 4.0.6 is restored (dotnet restore src\GraceKeeper.sln)."
}
Push-Location "$repoRoot\installer"
try {
    wix build Bundle.wxs -d Version=$Version -d mbanative=$mbanative -ext WixToolset.Util.wixext -ext WixToolset.Bal.wixext -ext WixToolset.NetFx.wixext -arch x64 -o $exe
    if ($LASTEXITCODE -ne 0) { throw "Bundle build failed" }
} finally { Pop-Location }

# 6. Summary
Write-Host "`n[6/6] Build complete." -ForegroundColor Green
$exeInfo = Get-Item $exe
Write-Host "Bundle: $($exeInfo.FullName)"
Write-Host "Size:   $([math]::Round($exeInfo.Length / 1MB, 2)) MB"
Write-Host "`nTo install on this machine:"
Write-Host "  .\GraceKeeper-$Version.exe"
