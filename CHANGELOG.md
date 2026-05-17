# Changelog

All notable changes to GraceKeeper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
