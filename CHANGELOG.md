# Changelog

All notable changes to GraceKeeper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
