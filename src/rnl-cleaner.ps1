[CmdletBinding()]
param(
    [string]$TargetDir = "C:\ProgramData\Rockwell Automation\FactoryTalk Activation",
    [string]$LogFile = "",
    [string]$DisabledFile = "",
    [int]$LogMaxLines = 500
)

$ErrorActionPreference = 'Stop'

# Resolve defaults in the body, NOT in the param block.
# $PSScriptRoot is unreliable at param-default-evaluation time when the script
# is invoked via `& powershell.exe -File ...` (see legacy commit c0895ac).
# Defaults now point at %ProgramData%\GraceKeeper\ so the dashboard and scheduled
# task share the same paths regardless of where the script lives on disk.
$ProgramData = [Environment]::GetFolderPath('CommonApplicationData')
$GkRoot = Join-Path $ProgramData 'GraceKeeper'

if ([string]::IsNullOrEmpty($LogFile)) {
    $LogDir = Join-Path $GkRoot 'logs'
    if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
    $LogFile = Join-Path $LogDir 'cleaner.log'
}
if ([string]::IsNullOrEmpty($DisabledFile)) {
    $DisabledFile = Join-Path $GkRoot 'DISABLED'
}

function Write-Log {
    param([string]$Message)
    $stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -Path $LogFile -Value "$stamp | $Message"
}

function Rotate-Log {
    if (-not (Test-Path $LogFile)) { return }
    $lines = Get-Content $LogFile
    if ($lines.Count -gt $LogMaxLines) {
        $lines | Select-Object -Last $LogMaxLines | Set-Content $LogFile
    }
}

if (Test-Path $DisabledFile) {
    Write-Log "skipped (disabled)"
    Rotate-Log
    exit 0
}

if (-not (Test-Path $TargetDir)) {
    Write-Log "target dir missing: $TargetDir"
    Rotate-Log
    exit 0
}

$start = Get-Date
$deleted = 0
$lockedNames = @()

Get-ChildItem -Path $TargetDir -Filter "*.rnl" -Force -ErrorAction SilentlyContinue | ForEach-Object {
    $name = $_.Name
    $full = $_.FullName
    try {
        Remove-Item $full -Force -ErrorAction Stop
        $deleted++
    } catch {
        $lockedNames += $name
    }
}

$durationMs = [int]((Get-Date) - $start).TotalMilliseconds
$lockedSummary = if ($lockedNames.Count -gt 0) { " ($($lockedNames -join ','))" } else { "" }
Write-Log "deleted=$deleted | locked=$($lockedNames.Count)$lockedSummary | duration=${durationMs}ms"

Rotate-Log
exit 0
