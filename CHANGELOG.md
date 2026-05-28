# Changelog

All notable changes to GraceKeeper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.1] - 2026-05-28

### Fixed
- **"Run cleaner now" actually deletes files.** The dashboard's button invoked `RnlCleaner` in-process as the interactive user. The `.rnl` files in `C:\ProgramData\Rockwell Automation\FactoryTalk Activation\` are deletable only by `NT AUTHORITY\SYSTEM` and `BUILTIN\Administrators` (Users get ReadAndExecute + Write but no Delete). `File.Delete` threw `UnauthorizedAccessException`, which `TryDeleteWithRetries` swallowed silently — files landed on the "locked" list, bounce was either skipped or failed too, and the button still went green. The dashboard now triggers a new SYSTEM-context scheduled task `GraceKeeper - Manual Cleanup` (no triggers, fired by `schtasks /Run`), watches `cleaner.log` for the result, and shows real outcomes: green only when files were actually refreshed, red when the cleaner reports `still-locked=N` or the task never wrote a result.
- **Tray-menu "Run cleaner now" was firing the wrong task.** Previously called `GraceKeeper - Cleanup RNL` (`--mode safety-net`), which short-circuits if any cleanup happened in the last 11 hours. Now fires `GraceKeeper - Manual Cleanup` (no gate).

### Added
- **`--mode manual` for `GraceKeeper.Cleaner.exe`.** Force-bounce semantics like `Boot`, but flagged separately in the log so triage can distinguish manual triggers from boot triggers.
- **Startup banner in `cleaner.log`.** Every Cleaner.exe run now writes a line at the top with the target path, whether it exists, the .rnl file count, and whether the process is running as SYSTEM. Lets remote triage tell apart "ran but no files to clean" from "ran but couldn't see the directory" from "ran as the wrong user."
- **Diagnostics panel on the dashboard.** New "Open log folder" and "Copy diagnostics to clipboard" buttons. The clipboard copy is a markdown report with version info, elevation status, sentinel state, RNL target directory listing, raw `schtasks /Query` output for all GraceKeeper tasks, and tails of cleaner / dismisser / supervisor logs — designed to paste straight into a chat message when remote access isn't available.
- **`GraceKeeper - Manual Cleanup` scheduled task.** Registered at install, no triggers, runs as SYSTEM. Removed cleanly on uninstall via new `RemoveManualCleanupTask` WiX custom action.

## [0.4.0.0] - 2026-05-26

### Fixed
- **Auto-cleanup actually runs now.** The v0.3.x scheduled task was created via `schtasks /Create /SC HOURLY` which bakes in CLI defaults that silently dropped missed runs: `DisallowStartIfOnBatteries=true`, `StopIfGoingOnBatteries=true`, and no `StartWhenAvailable`. On any workstation that sleeps overnight or goes mobile, the task could go days without firing. The new tasks are registered from inline XML with all three of those reversed plus `<StartWhenAvailable>true</StartWhenAvailable>` so missed runs catch up when the machine wakes.
- **In-place upgrade from v0.3.x now works.** Historical releases shipped with non-numeric version labels (`0.3.0.3-logontask`, `0.2.1-singleinst`) which WiX warned about as undefined MSI behavior — MSI's `MajorUpgrade` couldn't compare those reliably, which is why users had to manually uninstall before upgrading. `scripts/build-local.ps1` now rejects any non-numeric version at build time so this can't recur.
- **Dashboard version label updates with the release.** Previously hardcoded to `v0.3.0` and never bumped; now binds to `Assembly.GetEntryAssembly().GetName().Version`, fed by `-p:Version=$Version` on the SDK build.

### Added
- **Three independent cleanup triggers, one code path.** A new `GraceKeeper.Cleaner.exe` console (mode `boot` or `safety-net`) is invoked by two SYSTEM-context scheduled tasks: `GraceKeeper - Boot Cleanup` (fires on Windows boot before user logon) and `GraceKeeper - Cleanup RNL` (fires every 12h regardless of tray state). The tray app drives in-process cleanup via a `DispatcherTimer` that ticks at app-launch + every `IntervalHours` (default 12). The safety-net schtask self-suppresses if the tray already cleaned within the last 11 hours.
- **Echo-busy guard.** `EchoControllerProbe` enumerates child processes of `EmulateService.exe` to detect active emulated controllers. In `Runtime` and `SafetyNet` modes, if any controller is running, the cleaner defers locked files rather than bouncing services (which would fault the controller). `Boot` and `ManualForce` modes bypass the guard. The dashboard's "Run cleaner now" button shows a confirm modal when Echo is busy.
- **Service-bounce fallback.** When the lock-retry loop (3 attempts at 200ms backoff) can't free a file and the mode allows it, `ServiceBouncer` stops Echo Message Broker → Echo Service → FT Activation Service → FT Activation Helper, retries the deletion, then starts them back up in reverse order (Helper-before-Service per Rockwell's documented restart sequence).
- **`GraceKeeper.Cleaner.exe`.** New ~50-line headless .NET 4.8 console binary, shipped beside the tray app, used by both SYSTEM-context scheduled tasks. Top-level try/catch always logs `failed: <type>: <message>` so cron-style failures are visible from the dashboard activity panel.

### Changed
- **`rnl-cleaner.ps1` retired.** The PowerShell cleaner script and its Pester test file are gone; xUnit (against `GraceKeeper.Core`) is the only test layer now. Pester step removed from `scripts/build-local.ps1`.
- **Dashboard counter renamed.** "RNL files deleted" → "RNL files refreshed" (more accurate — the activation service regenerates the .rnl on the next client poll; the deletion is a *refresh*, not a removal).
- **`cleaner.log` format.** New structured line: `refreshed=N | freed-by-bounce=M | deferred=K | still-locked=L | duration=Xms`. The dashboard's regex accepts both new and legacy `deleted=N` formats so counters survive the upgrade.
- **Settings/interval routing.** Cleaner interval (now in hours, default 12) persists to `config.json` under `Cleaner.IntervalHours`. The in-process scheduler re-reads it on every tick — no schtask mutation required.

### Removed
- `src/rnl-cleaner.ps1` (replaced by C# `RnlCleaner` in Core)
- `tests/rnl-cleaner.Tests.ps1` (Pester suite — no PowerShell to test)
- `[1/7] Pester tests` step in `scripts/build-local.ps1` (renumbered to 6 steps)
- The old `CreateScheduledTask` + `RemoveScheduledTask` WiX CustomActions; replaced with PowerShell-driven `CreateCleanerTasks` + `RemoveCleanupTask` + `RemoveBootCleanupTask`.

## [0.2.1] - 2026-05-17

### Fixed
- **Finish-page launch did nothing on real installs.** The bundle's `InstallFolder` variable stores `[ProgramFiles64Folder]GraceKeeper\` as literal text; `IEngine.GetVariableString` does not expand `[X]` references — only `IEngine.FormatString` does. The bootstrapper now calls `FormatString("[InstallFolder]")` so the dashboard launch path resolves to a real filesystem path instead of throwing inside `Process.Start`. (v0.2.0 worked only with the cached install path; first-time installs from a freshly-downloaded EXE produced no launch.)

## [0.2.0] - 2026-05-17

### Changed
- **Installer rework.** Distribution is now a Burn bootstrapper EXE (`GraceKeeper-0.2.0.exe`) with a custom WPF UI instead of the WixUI_Minimal MSI dialog set. Same dark crimson visual language across welcome, progress, finish, and uninstall pages.
- **Uninstall UX.** Uninstalling from Apps & Features now opens the same custom UI for confirmation, with an optional "Remove all data" checkbox to also delete `%ProgramData%\GraceKeeper\`.

### Fixed
- **Dismisser fails to start at install time.** rc3 attempted a `schtasks /Create /IT` trampoline in a custom action; it did not reliably produce a running `AutoHotkey64.exe`. The bootstrapper now launches the dashboard directly from its user-context process on the Finish page, and the dashboard owns the dismisser lifecycle from that point on. No logout+login required.
- **Dead dismisser stays dead.** The supervisor in `GraceKeeper.Core` is now wired up: a 30-second timer in the dashboard process detects a missing or wrong-process `AutoHotkey64.exe` and respawns it, with full disambiguation via install-path and command-line match so unrelated AHK scripts the user runs are never touched.

### Removed
- `LaunchDismisser`, `LaunchDashboard`, and `StopDashboard` custom actions in the MSI (no longer needed — the bootstrapper owns post-install launch, dashboard owns dismisser).
- Duplicate `GraceKeeperDismisser` HKLM Run entry (dashboard now spawns dismisser).

## [0.1.0] - 2026-05-16

### Fixed
- Dismisser crashed with "Target window not found" when no window had focus at the moment a popup was dismissed. Wrap `WinGetID("A")` in `try`.
- Dismisser now starts automatically at install completion instead of waiting for next user login. (rc2 required logout+login or manual launch.)
- Sidebar/banner artwork redesigned with a left-strip layout — crimson + GK mark on the left ~100px, clean light surface on the right where WiX widgets (title text, EULA scrollbox, buttons) sit, so widget text is now legible.

### Changed
- Installer wizard switched from `WixUI_InstallDir` (7 dialogs, license + install-dir picker) to `WixUI_Minimal` (3 dialogs: combined Welcome+EULA, Progress, Finish). Shorter flow, no awkward install-dir page.
- Sidebar + banner artwork reworked: 2x supersampled rendering, multi-stop gradient with warm highlight, subtle diagonal texture, drop-shadowed brand mark, tagline added.
- Install adds custom actions to launch GraceKeeper.exe + AutoHotkey64.exe (dismisser) in the user's session post-InstallFinalize. Uninstall taskkills both before file removal.

### Added
- Initial release: MSI installer, WPF dashboard, popup dismisser, scheduled cleaner.
