#Requires -Version 5.1
# Registers GraceKeeper's cleanup scheduled tasks from inline XML.
#
# Why XML and not /SC HOURLY /MO 12 /ST 03:00: schtasks.exe CLI cannot
# set StartWhenAvailable, DisallowStartIfOnBatteries=false, or other XML
# fields that determine whether missed runs are caught up. The CLI
# defaults dropped missed runs forever — see v0.4.0 design spec.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

$installDir = $PSScriptRoot
$cleanerExe = Join-Path $installDir 'GraceKeeper.Cleaner.exe'

function New-TaskXml {
    param(
        [Parameter(Mandatory)][string]$Trigger,
        [Parameter(Mandatory)][string]$Arguments
    )
@"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <Principals>
    <Principal id="Author">
      <UserId>S-1-5-18</UserId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <StartWhenAvailable>true</StartWhenAvailable>
    <WakeToRun>false</WakeToRun>
    <AllowHardTerminate>false</AllowHardTerminate>
    <ExecutionTimeLimit>PT15M</ExecutionTimeLimit>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <Priority>7</Priority>
  </Settings>
  <Triggers>
$Trigger
  </Triggers>
  <Actions Context="Author">
    <Exec>
      <Command>"$cleanerExe"</Command>
      <Arguments>$Arguments</Arguments>
    </Exec>
  </Actions>
</Task>
"@
}

function Register-Task {
    param([string]$Name, [string]$Xml)
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("gk-" + [Guid]::NewGuid().ToString('N') + ".xml")
    [System.IO.File]::WriteAllText($tmp, $Xml, [System.Text.Encoding]::Unicode)
    try {
        & schtasks.exe /Create /F /TN $Name /XML $tmp
        if ($LASTEXITCODE -ne 0) { throw "schtasks failed for $Name (exit $LASTEXITCODE)" }
    } finally {
        Remove-Item -Force -ErrorAction SilentlyContinue $tmp
    }
}

$bootTrigger = @'
    <BootTrigger>
      <Delay>PT0S</Delay>
      <Enabled>true</Enabled>
    </BootTrigger>
'@

$intervalTrigger = @'
    <TimeTrigger>
      <StartBoundary>2026-01-01T03:00:00</StartBoundary>
      <Enabled>true</Enabled>
      <Repetition>
        <Interval>PT12H</Interval>
        <StopAtDurationEnd>false</StopAtDurationEnd>
      </Repetition>
    </TimeTrigger>
'@

Register-Task -Name 'GraceKeeper - Boot Cleanup' -Xml (New-TaskXml -Trigger $bootTrigger  -Arguments '--mode boot')
Register-Task -Name 'GraceKeeper - Cleanup RNL'  -Xml (New-TaskXml -Trigger $intervalTrigger -Arguments '--mode safety-net')

exit 0
