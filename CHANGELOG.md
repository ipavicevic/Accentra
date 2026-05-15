# Changelog

All notable changes are documented here. Entries are written when the PR is merged — copy the relevant section into Partner Center's "What's new in this version" field when submitting a Store update.

## [1.4.1] - 2026-05-15

### Fixed
- Accent mode no longer activates when a non-Latin keyboard layout is active (e.g. Serbian Cyrillic, Greek, Arabic). Accentra now passes all keystrokes through unchanged when the foreground window's input language uses a non-Latin script.

## [1.4.0] - 2026-05-12

### Added
- New tray menu item: **Pause accent input** — temporarily disables accent mode without quitting Accentra. Click again to resume. Useful when you need normal key auto-repeat.
- Auto-repeat is now suppressed for all letter keys (A–Z) while Accentra is running, not just keys that have accent variants defined. This makes the behavior consistent across the keyboard.

## [1.3.0] - 2026-05-07

### Changed
- App icon updated: rounded square shape (matching Microsoft Store style), blue background, and a new `ā` glyph replacing `ã`.
- Landing page hero icon updated to match.

## [1.2.3] - 2026-05-06

### Changed
- Accentra now shows a balloon tip on every startup confirming it is running. This gives immediate feedback when launching via the Microsoft Store's Open button, which provides no other visual response for a tray-only app.

## [1.2.2] - 2026-05-06

### Fixed
- `accent-maps.json` changes now take effect immediately without restarting Accentra. The file is watched for changes and hot-reloaded within ~400 ms.
- A balloon tip warns when `accent-maps.json` cannot be parsed, and keeps the previous maps active. Previously the error was silent and Accentra fell back to built-in defaults without any indication.
- Fixed a malformed entry in the built-in `accent-maps.json` (missing comma) that caused silent fallback to built-in defaults on first install.

## [1.2.1] - 2026-05-05

### Added
- Accentra shows a balloon tip when it restarts elevated ("Now running elevated — accent mode works in admin apps."), confirming the takeover succeeded.

## [1.2.0] - 2026-05-05

### Added
- New tray menu item: **Edit accent maps...** opens the accent maps folder in Explorer, making it easy to customize which accented characters are available for each key.

## [1.1.3] - 2026-05-05

### Fixed
- Accent maps folder now correctly resolves to the MSIX package data folder. Previously, the "Edit accent maps..." folder opened the wrong location due to MSIX file system virtualization.

## [1.1.1] - 2026-05-05

### Added
- App execution alias: `Accentra.exe` is now accessible from any command prompt, including elevated ones, without knowing the install path.

## [1.1.0] - 2026-05-05

### Changed
- Accentra is now distributed via the **Microsoft Store** as an MSIX package. Benefits: automatic updates, no SmartScreen warning, no manual PATH setup.
- Startup toggle ("Start with Windows") now uses the Windows startup task API instead of a registry Run key.

## [1.0.10] and earlier

Initial releases distributed as a self-installing EXE via GitHub Releases.
