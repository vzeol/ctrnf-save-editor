# Changelog

## Unreleased

- Bilingual About dialog with version, license, credits and project links.
- Optional startup and manual update checks, cancellable verified downloads, installation/restart and a previous-installation rollback copy.

## 1.0.1 — 2026-10-27

Gem cup fix. Checking a gem only set the slot gem counter: the game showed the
Oxide rematch and 100 %, but the gem stayed on its Gem Stone Valley pedestal.

- Each gem now also writes its gem cup flag, one track bit on two tracks:
  red 12, blue 13, purple 14, green 15, yellow 16. Confirmed in game for all five colours.
- Removing a gem clears that flag; the checker reports a gem without its flag.
- Saves edited with 1.0.0: open the Adventure slot and use **Normalize** to repair them.

## 1.0.0 — 2026-10-27

First regular release of the existing community editor feature set. This release
promotes the public preview without adding new save-editing behavior.

- Adventure progression, global unlocks, skins, customization and existing time-attack editing.
- Windows x64 GUI and CLI, English/French interface, bundled .NET runtime and checksum.
- Original mascot icon for both executables and the GUI window; prominent credit to the OCTR launcher for the GUI's visual inspiration.
- Future fixes and improvements will follow through issues, pull requests and versioned releases.

The format remains partially decoded and some mappings still need in-game
validation; see the feature table and roadmap.

## 0.1.0-preview.1 — 2026-10-27

First public community preview, with a fresh source-only repository history.

- Separate Core, CLI and Windows GUI projects with English/French interface.
- Adventure progression, prerequisites, two Oxide victories and difficulty editing.
- Named driver/skin/customization catalogues, win counters and previewed bulk actions.
- Existing time-trial record and ghost-progress editing, plus recognized Champion reward conditions.
- Backup and external-change protection, generated public tests and optional private regression runner.
- Bilingual user documentation, contribution templates and Windows CI/release packaging.

The format remains partially decoded. Native/static mappings and in-game confirmations are distinguished in the documentation.
