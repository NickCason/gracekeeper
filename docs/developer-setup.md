# Developer Setup

How to build GraceKeeper from source on a Windows workstation.

## Prerequisites

| Tool | Version | Install |
|---|---|---|
| Visual Studio Build Tools | 2022 | `winget install Microsoft.VisualStudio.2022.BuildTools --override "--add Microsoft.VisualStudio.Workload.NetCoreBuildTools --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools"` |
| .NET SDK | 8.0+ | `winget install Microsoft.DotNet.SDK.8` |
| .NET Framework 4.8 Developer Pack | latest | `winget install Microsoft.DotNet.Framework.DeveloperPack_4` |
| AutoHotkey v2 | 2.0+ | `winget install AutoHotkey.AutoHotkey` |
| Ahk2Exe | latest | Download from https://www.autohotkey.com/download/ahk2exe.zip; extract to `C:\Program Files\AutoHotkey\Compiler\` |
| WiX Toolset | v5 | `dotnet tool install --global wix` |
| PowerShell 5.1 or 7 | — | preinstalled on Windows |

## Local build

From the repo root:

```powershell
.\scripts\build-local.ps1 -Version 0.4.0.0
```

The `-Version` parameter must be a clean numeric `N.N.N.N` value; the script rejects labels like `-dev` or `-logontask` because non-numeric suffixes break MSI MajorUpgrade comparisons.

This:
1. `[1/6]` `dotnet build` (Release) — builds the full solution including `GraceKeeper.Cleaner`
2. `[2/6]` xUnit tests against `src/GraceKeeper.Core.Tests/` (via `dotnet test`, no separate install required)
3. `[3/6]` Stages `AutoHotkey64.exe` into `installer/staging/` from a system AHK v2 install
4. `[4/6]` Builds the inner MSI to `installer/bin/dev/GraceKeeper-<version>.msi`
5. `[5/6]` Builds the Burn bundle EXE at the repo root: `GraceKeeper-<version>.exe`
6. `[6/6]` Prints a summary with bundle path and size

## Manual test on local VM

After building, run the bundle EXE (`GraceKeeper-<version>.exe` at the repo root) on a clean VM snapshot per `docs/release-checklist.md`.
