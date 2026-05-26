#Requires -Version 5.1
# Registers the "GraceKeeper - Dashboard Logon" scheduled task with a LogonTrigger.
#
# Why this exists: an HKLM\...\Run entry is subject to Windows 11 Startup Apps
# deferral, which can delay tray-app launch by 1-3 minutes after user logon.
# A Task Scheduler LogonTrigger fires at session start with no impact-manager
# throttling, so the dashboard appears immediately.
#
# The script discovers its own install dir via $PSScriptRoot so the WiX
# CustomAction only needs to invoke it (no [INSTALLDIR] argument quoting).
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

$installDir = $PSScriptRoot
$exePath    = Join-Path $installDir 'GraceKeeper.exe'

# GroupId S-1-5-32-545 = BUILTIN\Users. Combined with a LogonTrigger that has
# no UserId, the task fires for any interactive user at their own logon and
# runs in that user's session (InteractiveToken is the default LogonType for
# a group principal). RunLevel=LeastPrivilege keeps the dashboard
# unelevated — same as the prior HKLM Run-key behavior.
$xml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <GroupId>S-1-5-32-545</GroupId>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$exePath</Command>
    </Exec>
  </Actions>
</Task>
"@

# Task Scheduler's XML importer requires UTF-16 LE with BOM.
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) 'gk-dashboard-logon.xml'
[System.IO.File]::WriteAllText($tmp, $xml, [System.Text.Encoding]::Unicode)
try {
    & schtasks.exe /Create /F /TN 'GraceKeeper - Dashboard Logon' /XML $tmp
    $exit = $LASTEXITCODE
} finally {
    Remove-Item -Force -ErrorAction SilentlyContinue $tmp
}
exit $exit
