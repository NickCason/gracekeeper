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
| Pester | 5.x | `Install-Module Pester -Force -SkipPublisherCheck` |

## Local build

From the repo root:

```powershell
.\scripts\build-local.ps1
```

This:
1. Restores NuGet packages
2. Builds `GraceKeeper.UI` and `GraceKeeper.Core` in Release mode
3. Runs `GraceKeeper.Core.Tests`
4. Runs the Pester suite (`tests/`)
5. Compiles `popup-dismisser.ahk` with Ahk2Exe
6. Packages the MSI with WiX
7. Outputs `GraceKeeper-local.msi` in the repo root

## Manual test on local VM

After building, install the MSI on a clean VM snapshot per `docs/release-checklist.md`.
