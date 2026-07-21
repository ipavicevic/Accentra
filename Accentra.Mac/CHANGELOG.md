# Changelog — Accentra for Mac

Notable changes to the macOS app. Versioned independently of the Windows app
(see `../CHANGELOG.md`); the shared compatibility contract is the
`accent-maps.json` schema version, not the app version.

## [1.1.0]

### Added
- Accentra now alerts you when `accent-maps.json` is reloaded after an edit,
  fails to parse, or gets migrated to a newer schema on startup — matching the
  Windows app.
- When a load or reload fails, Accentra now offers **Revert** (restore your
  last working configuration) or **Keep** (leave the file as-is) instead of
  just reporting the error. A snapshot of your accent maps is kept up to date
  (`accent-maps.lastgood.json`, alongside `accent-maps.json`) every time a
  load succeeds, so a broken edit is always one click away from being undone.
  Error alerts use a plain, non-technical message and a more prominent
  (critical-style) presentation; the exact parse error is still in the log.

### Fixed
- The reload watcher now detects edits saved by editors that write atomically
  (VS Code, Xcode, and most others) instead of in place. Previously only a
  direct in-place write was detected; an atomic save — write a temp file, then
  rename it over the original — was invisible to the watcher.

## [1.0.7]

### Changed
- Start at Login now uses Apple's `SMAppService` login-item API (macOS 13+) instead
  of a hand-written LaunchAgent. The old LaunchAgent launched the raw binary, which
  bypassed LaunchServices and defeated the app's menu-bar-agent (`LSUIElement`) status
  — the reason Accentra was landing in the Dock's "recent applications" list. macOS now
  launches the app bundle as a proper login item, so it stays a menu-bar-only agent.
  Existing installs are migrated automatically, and Start at Login is now visible and
  toggleable in System Settings → General → Login Items.

## [1.0.6]

### Fixed
- Accentra no longer appears in the Dock or its "recent applications" list. On
  launch — especially a cold boot, where startup can take several seconds — the
  app ran as a regular (Dock) app until it set its menu-bar-agent policy late in
  startup; during that window macOS put it in the Dock and recorded it in recents,
  where it lingered afterward. The agent policy is now set as the very first thing
  at startup, so Accentra is never a Dock app.

## [1.0.5]

### Changed
- Removed the redundant first-run "Accessibility Permission Required" alert. It
  duplicated the system permission prompt and the dimmed "waiting" tray state, and
  showing it briefly promoted the app to a regular (Dock) app. (This removed one
  cause of the Dock appearance; the remaining cause is fixed in 1.0.6.)

## [1.0.4]

### Changed
- Accentra now enables **Start at Login** on first run, so an always-on utility is
  actually running after a reboot (parity with the Windows startup task). It is only
  set on first run — if you turn it off, it stays off — and it is announced in the
  first-run welcome and toggleable from the menu. Accentra registers only its own
  login item; it does not change any system-wide setting.

## [1.0.3]

### Fixed
- Accenting a key that macOS also offers in its own press-and-hold picker
  (e.g. `c`, `s`) no longer eats a neighbouring character or leaves the picker
  popping up on later presses. Accentra now intercepts the held key before the
  system sees it and re-types the base character itself, so the picker never
  engages on keys Accentra handles. Single-letter app shortcuts on those keys
  keep working, and the native picker still appears for keys Accentra does not map.

## [1.0.2]

### Changed
- Removed keystroke diagnostics from the local log. Earlier builds recorded every
  key-down to `accentra.log`; the log now contains only startup, permission, and
  error events, and never the text you type.

## [1.0.1]

### Changed
- Switched distribution to a signed, notarized drag-to-Applications DMG.

## [1.0.0]

### Added
- First macOS release: menu-bar app bringing Accentra's hold-to-accent, tap-to-cycle
  input to any Mac app.
