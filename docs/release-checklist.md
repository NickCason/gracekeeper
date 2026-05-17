# Release Checklist

Before cutting a release tag, verify on a clean VM snapshot.

## Pre-release

- [ ] All tests pass locally: `.\scripts\build-local.ps1 -Version <ver>`
- [ ] CI is green on `master`
- [ ] `CHANGELOG.md` updated with notable changes under a new version section
- [ ] No uncommitted changes (`git status` clean)

## Build verification

- [ ] Build bundle on clean checkout: `.\scripts\build-local.ps1 -Version <ver>`
- [ ] Bundle size in 2-7 MB range (around 2.1 MB for v0.2.0; flag drift >50%)
- [ ] Install on Windows 10 VM snapshot: double-click `GraceKeeper-<ver>.exe`. SmartScreen warning expected — click "More info" → "Run anyway."
- [ ] Install on Windows 11 VM snapshot

## Post-install behavior

- [ ] Within 5 seconds of clicking Finish on the Bootstrapper page (with "Launch GraceKeeper now" checked), both `GraceKeeper.exe` and `AutoHotkey64.exe` appear in Task Manager. No logout+login required.
- [ ] Tray icon (red `GK` mark) appears in system tray within 5 seconds
- [ ] Bootstrapper UI renders the three pages (Welcome+EULA, Progress, Finish) with legible text at 100%, 125%, and 150% display scaling
- [ ] Double-click tray icon → dashboard window opens
- [ ] Window shows hero metrics with zero counts initially
- [ ] Activity log empty initially
- [ ] **Real popup test:** open FactoryTalk LogixDesigner, wait for or trigger the "Product Activation Failed" popup, confirm it's dismissed within ~1 second and focus restores to the prior window
- [ ] **Real cleaner test:** seed a hidden `.rnl` file:

      ```powershell
      $f = New-Item -ItemType File -Path 'C:\ProgramData\Rockwell Automation\FactoryTalk Activation\test.rnl' -Force
      $f.Attributes = 'Hidden'
      ```
      Then click "Run cleaner now" in the dashboard. Confirm:
      - The file is deleted
      - The counter increments
      - The activity log shows a new `clean` event

- [ ] **SYSTEM-context verification:** confirm `schtasks /Query /TN "GraceKeeper - Cleanup RNL" /FO LIST /V` shows `Run As User: SYSTEM`. Then trigger the task and inspect `cleaner.log` for a successful run against real `.rnl` files. If SYSTEM cannot delete real `.rnl` files (permission denied in the log), open `installer/Product.wxs` and change `/RU SYSTEM` to `/RU "BUILTIN\Users"` — the legacy `rockwell-grace` install ran as the interactive user successfully, so user-context is the safe fallback.
- [ ] Settings: change interval to e.g. 60 minutes, save. Confirm `schtasks /Query` shows the new `/RI 60`
- [ ] Pause: click pause in tray menu. Confirm `%ProgramData%\GraceKeeper\DISABLED` exists, and that a fresh popup is NOT dismissed (open LogixDesigner and confirm popup stays). Unpause and re-verify dismissal works
- [ ] Theme switch: toggle Windows theme via `Settings → Personalization → Colors`. Confirm dashboard follows within ~1 second (greyer surfaces in light, dark surfaces in dark — Rockwell-style crimson accent in both)
- [ ] **Supervisor respawn:** kill `AutoHotkey64.exe` via Task Manager. Within 30 seconds, a new `AutoHotkey64.exe` instance appears. `%ProgramData%\GraceKeeper\logs\supervisor.log` records a new `spawned dismisser pid=...` entry.
- [ ] **Detached-child survival:** kill `GraceKeeper.exe` via Task Manager. `AutoHotkey64.exe` stays alive (it's a detached child, not killed with the dashboard). Relaunch the dashboard from the Start Menu. `supervisor.log` records `adopted existing dismisser pid=...`.
- [ ] `%ProgramData%\GraceKeeper\dismisser-pid.json` exists and contains a JSON record with the current AHK PID, exe path, script path, and start time

## Uninstall behavior

- [ ] Settings → Apps → GraceKeeper → Uninstall (or run `GraceKeeper-<ver>.exe /uninstall` directly). The same custom UI opens asking for confirmation.
- [ ] Click Uninstall in the bootstrapper UI. Wait for the success page.
- [ ] Confirm `C:\Program Files\GraceKeeper\` is gone
- [ ] Confirm `schtasks /Query /TN "GraceKeeper - Cleanup RNL"` reports "not found"
- [ ] Confirm `reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v GraceKeeper` reports "not found"
- [ ] Confirm Start Menu shortcut is gone
- [ ] Confirm tray icon is gone (relaunch the dashboard wouldn't recreate it after uninstall)
- [ ] `%ProgramData%\GraceKeeper\` persists by default (intentional, for upgrades). If "Remove all data" was checked in the uninstall UI, confirm it is gone instead.

## Upgrade flow (when releasing a 0.1.x → 0.1.(x+1) bump)

- [ ] Install previous version on clean VM
- [ ] Drop a marker file in `%ProgramData%\GraceKeeper\upgrade-marker.txt`
- [ ] Install new version over it via `.\GraceKeeper-<ver>.exe` (no need to uninstall first — MajorUpgrade handles it)
- [ ] Verify marker file survived (ProgramData was preserved)
- [ ] Verify scheduled task still exists with original schedule (unchanged on upgrade)

## Tagging the release

- [ ] `git tag -a vX.Y.Z -m "Release X.Y.Z"`
- [ ] `git push --tags`
- [ ] Wait for `.github/workflows/release.yml` to complete (~3–5 minutes)
- [ ] Verify the published MSI on GitHub matches the locally built one (size, smoke-install)
- [ ] Update `README.md` if any user-visible behavior changed
- [ ] Announce in etechgroup channels

## Known limitations (current)

- **Unsigned bundle**: users will see SmartScreen "Windows protected your PC" on first install. Click "More info" → "Run anyway." This goes away once Microsoft's reputation system catches up (or after code signing is added in v0.3.0).
- **Code signing**: not done. The EXE's publisher field shows "Nick Cason" (text only, unverified). SignPath.io Foundation application is the planned free path for v0.3.0.
