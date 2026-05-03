# Testing the MSIX Build

## Prerequisites

Enable Developer Mode so Windows accepts packages signed with a test certificate:

**Windows Settings → System → For developers → Developer Mode → On**

## Install

1. Download `Accentra.msix` from the GitHub Release for the beta tag being tested
2. Double-click `Accentra.msix`
3. Click **Install** in the App Installer dialog (Windows will warn about the test certificate — this is expected for beta builds)

## Verification checklist

- [ ] Tray icon appears with the **ã** glyph
- [ ] Tooltip shows the correct version (e.g. `Accentra 1.0.11`)
- [ ] **Start with Windows** toggle reflects current state and can be toggled on/off
- [ ] **About Accentra...** opens the landing page in the default browser
- [ ] **Exit** closes the app and removes the tray icon
- [ ] Accent typing works — hold a key (e.g. `e`) past the long-press threshold, character cycles through variants (`é`, `è`, `ê`, `ë`)
- [ ] Pressing a different key confirms the accented character and exits accent mode
- [ ] App appears in **Settings → Apps → Installed apps** as `Accentra`

## Uninstall

**Settings → Apps → Installed apps → Accentra → Uninstall**

The app and all registry entries are removed by Windows automatically.

## Upgrade

Install a newer MSIX over an existing installation — the installer should replace the old version without requiring a manual uninstall first. Verify that `accent-maps.json` in `%LOCALAPPDATA%\Accentra\` is preserved (not overwritten) across upgrades.
