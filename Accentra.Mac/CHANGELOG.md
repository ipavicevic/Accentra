# Changelog — Accentra for Mac

Notable changes to the macOS app. Versioned independently of the Windows app
(see `../CHANGELOG.md`); the shared compatibility contract is the
`accent-maps.json` schema version, not the app version.

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
