# GraceKeeper

FactoryTalk grace-period automation for Rockwell SIs.

GraceKeeper keeps the FactoryTalk Activation grace period alive on workstations and VMs without requiring manual intervention. It dismisses the "Product Activation Failed" popup when it appears, and periodically deletes the `.rnl` refreshment files in `C:\ProgramData\Rockwell Automation\FactoryTalk Activation\`.

## Install

Download `GraceKeeper-<version>.exe` from [Releases](../../releases/latest) and double-click. Windows will show a SmartScreen warning the first time — click "More info" → "Run anyway." (Code signing is planned for a future release.) Requires admin rights; installs per-machine to `C:\Program Files\GraceKeeper\`.

## What it does

- **Dismisses popups**: when `LogixDesigner.Exe` shows the "Product Activation Failed" dialog, GraceKeeper closes it within milliseconds and restores your previous window focus.
- **Cleans refreshment files**: deletes `.rnl` files every 12 hours (configurable) — resets the activation grace period without requiring a service restart.
- **Stays out of the way**: lives in the system tray; click for the dashboard.

## Dashboard

The tray icon opens a dashboard showing:
- Lifetime + today's count of popups dismissed
- Lifetime + today's count of `.rnl` files deleted
- Time since last clean and next scheduled run
- Recent activity log
- Manual "Run cleaner now" trigger
- Pause/resume toggle
- Schedule editor
- Health indicators

The window follows Windows light/dark theme automatically.

## Uninstall

Standard Windows uninstall via `Settings → Apps`. The same Setup UI opens, asks for confirmation, and optionally removes your local data (`%ProgramData%\GraceKeeper\`).

## Building from source

See `docs/developer-setup.md`.

## License

MIT. See `LICENSE`.
