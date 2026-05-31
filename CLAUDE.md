# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Accentra** is a Windows system tray app that enables accented character input across all Windows applications (Discord, Messenger, browsers, etc.) by intercepting keystrokes globally via a Win32 low-level keyboard hook.

## UX Behavior

- Normal typing passes through untouched
- **Long press** a key → the character already inserted is replaced with the first accented variant (e.g., `e` → `é`)
- **Repeat press of the same key** → cycles to the next variant (e.g., `é` → `è` → `ê` → `ë` → back to `e`)
- **Press a different key** → confirms the current accented character, exits accent mode, and the new key goes through normally
- The "replace" mechanic works by sending a backspace followed by the new accented character via `SendInput`
- No popup window, no overlay — purely keyboard-driven

## Stack

- **.NET 8 + WinForms** — WinForms is used only for the system tray icon (`NotifyIcon`) and message loop; there is no application window
- **Win32 API via P/Invoke** for the global keyboard hook and character injection
- **WinRT APIs** (`Windows.ApplicationModel`, `Windows.Storage`) for MSIX startup task and data folder access

## Architecture

### Global Keyboard Hook

Registered with `SetWindowsHookEx(WH_KEYBOARD_LL, ...)` using thread ID `0` (system-wide). The hook fires for all keystrokes across all applications running at the same privilege level. The hook is driven by the WinForms message loop.

```
Physical key press → Windows input system → Hook callback → Target application
```

The callback either calls `CallNextHookEx` (pass through) or returns without it (suppress).

### Accent Mode State Machine

- **Idle**: all keystrokes pass through normally; track key-down timing
- **Accent mode**: entered when a key is held past the long-press threshold; same key = cycle, different key = confirm + exit

### Character Injection

Uses `SendInput` to inject Unicode characters. To replace the current character: send backspace, then send the accented character.

### Accent Maps

Each base character (e, a, o, u, n, c, etc.) maps to an ordered `char[]` of variants. Cycling wraps back to the plain character at the end of the list. The maps are loaded from `accent-maps.json` in the app's data folder (`Installer.AccentMapsDir`), falling back to the embedded default if the file is absent or malformed.

## Key Win32 APIs

- `SetWindowsHookEx` / `UnhookWindowsHookEx` — hook lifecycle
- `CallNextHookEx` — pass keystroke through
- `SendInput` — inject backspace and accented characters

## Deployment & Lifecycle

Distributed via the **Windows Store** as an MSIX package. The `Package.appxmanifest` declares:
- `runFullTrust` capability — required for `WH_KEYBOARD_LL`
- `windows.startupTask` — starts Accentra automatically on login
- `windows.appExecutionAlias` — makes `Accentra.exe` runnable from the command line (including elevated prompts)

### Data folder

`Installer.AccentMapsDir` returns the correct writable data folder in both contexts:
- **MSIX**: `Windows.Storage.ApplicationData.Current.LocalFolder.Path` (package-private, virtualized)
- **Unpackaged (dev)**: `%LOCALAPPDATA%\Accentra\`

`EnsureAccentMapsJson()` extracts the embedded `accent-maps.json` to this folder on first run only, preserving any user edits across upgrades.

### Tray Menu

```
Start with Windows  ✓
─────────────────────
Edit accent maps...
About Accentra...
─────────────────────
Exit
```

### Startup task toggle

Uses `Windows.ApplicationModel.StartupTask` API when running as MSIX, falls back to the `HKCU\...\Run` registry key when running unpackaged (dev/testing).

## Limitations

- The hook does not intercept keystrokes in windows running at a higher privilege level (UAC elevated processes). Workaround: run `Accentra.exe` from an elevated command prompt — the app execution alias makes this possible without knowing the install path.
- Does not work with the Windows touch/soft keyboard — touch input bypasses `WH_KEYBOARD_LL` entirely.

## Versioning Policy

Versions follow `Major.Minor.Revision` for releases, with an optional 4th part during development:

| Part | When to bump | Examples |
|------|-------------|---------|
| **Major** | Breaking change to user-facing behavior or config format | `accent-maps.json` schema change, fundamental UX change, dropping a Windows version |
| **Minor** | New user-visible functionality | New tray menu item, new accent keys, new distribution channel |
| **Revision** | Bug fixes and internal changes with no new features | Workflow fixes, manifest corrections, path bug fixes |
| **4th part** | Dev/test iterations within a feature branch | Increment each push for testing; dropped at release |

When the minor version bumps, reset revision to 0. When the major version bumps, reset both minor and revision to 0.

**Development workflow:**
- Start a new feature branch: bump Minor (or Revision for a fix), set 4th part to 1 (e.g. `2.1.0.1`)
- Each test push: increment the 4th part (`2.1.0.2`, `2.1.0.3`, …)
- PR / release: amend the version to drop the 4th part (`2.1.0`) and force-push before merging

The 4th part is hidden from users via `DisplayVersion` in `TrayApp.cs`.

## Release Process

1. Work on a feature branch — bump version in `Accentra.csproj` and add a CHANGELOG.md entry as part of the PR
2. Open a PR to `main` — CI build must pass
3. Test the MSIX artifact produced by the feature branch workflow before merging
4. Merge PR → CI automatically builds, signs, and publishes the GitHub Release
5. Upload the MSIX to Partner Center for Store submission

If the version was not bumped, the release step skips silently (tag already exists).

### After Store certification

Once Microsoft certifies the new version and it is live in the Store, open a follow-up PR that updates `docs/index.html`:
- Update the **"What's new in X.Y.Z"** section heading and bullet(s) to reflect the newly certified version
- Copy the bullet text from the corresponding CHANGELOG.md entry

Do **not** update the web page "What's new" section in the release PR — the Store version lags behind by days and the page would show a version users cannot yet download.
