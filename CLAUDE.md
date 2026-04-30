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

Each base character (e, a, o, u, n, c, etc.) maps to an ordered `char[]` of variants. Cycling wraps back to the plain character at the end of the list.

## Key Win32 APIs

- `SetWindowsHookEx` / `UnhookWindowsHookEx` — hook lifecycle
- `CallNextHookEx` — pass keystroke through
- `SendInput` — inject backspace and accented characters

## Deployment & Lifecycle

No installer. The EXE is self-installing on first run.

### First Run (from Downloads or wherever)

1. Detects it is not running from `%LOCALAPPDATA%\Accentra\`
2. Copies itself to `%LOCALAPPDATA%\Accentra\Accentra.exe`
3. Registers in `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Accentra` → appears in Settings → Apps → Installed apps
4. Adds itself to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → starts with Windows on every login
5. Launches the new copy and exits

### Normal Run (auto-started by Windows on login)

Already installed — hook registers, app sits in system tray, does its job.

### Tray Menu

```
Start with Windows  ✓
─────────────────────
Exit
```

### Uninstall (user clicks Uninstall in Settings → Apps)

Windows calls `Accentra.exe --uninstall`, which:

1. Removes the `Run` key → no longer starts with Windows
2. Removes the `Uninstall` registry entry → disappears from Installed apps
3. Deletes `%LOCALAPPDATA%\Accentra\` folder including the EXE (self-delete via `cmd /c` scheduled deletion)

No admin rights required at any point — all registry keys are HKCU (user-level).

## Limitation

The hook does not intercept keystrokes in windows running at a higher privilege level (UAC elevated processes). Chat apps (Discord, Messenger) and browsers always run at normal user level, so this is not a practical issue.
