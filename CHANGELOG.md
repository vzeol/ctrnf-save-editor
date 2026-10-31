# Changelog

## 1.1.3 — 2026-10-31

- Open fresh global profiles that have a version-only header and no serialized name-hash table (`0xB000000C`). Identify them through a fully decoded global root instead of requiring an Adventure slot summary.
- Preserve the original envelope when editing coins or local unlocks. Garage remains unavailable when its active loadout names cannot be resolved safely.
- Recognize an empty, unallocated local marketplace list and allocate its first purchase block while preserving existing object and memory IDs. Bulk unlocks can now include Pit Stop purchases on these profiles.
- Accept the game's `systemData` namespace spelling in Garage, validating its case-folded hash and preserving the original spelling.
- Add synthetic regression coverage for parsing, isolated edits, marketplace resizing, bulk unlocks and malformed variant rejection (44 tests total).
- In-game check on a fresh PC-port profile: edited unlocks were usable, a driver/kart configuration was changed in-game, and 843 local purchases plus the tracked unlock flags survived the game's subsequent save. This does not validate every catalogue entry individually.

## 1.1.2 — 2026-10-28

- Explain the normal and persistent mod save profiles on the welcome screen, in English and French.
- Show both relative paths for Adventure slots and the global profile.
- Document session-copy protection, normal-exit synchronization and closing the game before editing.
- No save-format or save-writing behavior changes.

## 1.1.1 — 2026-10-27

Display fixes for high-DPI screens (seen at 150 % Windows scaling), plus screenshots in the README.

- About dialog sizes to its content: the update and Close buttons are no longer cut off.
- Section menu widens to fit every English and French label instead of truncating them.
- Summary tiles show the full value (16 / 16, 56 / 56…).
- Adventure table: the first track of each hub shows its trophy and CTR token checkboxes again.
- Adventure settings boxes scale with the display; "Consistency and dependencies" title shows correctly.
- Header no longer leaves a ghost title after the window is resized.

## 1.1.0 — 2026-10-27

Garage, About and in-app updates.

**Garage** (Global profile mode; the game keeps only one kart loadout)
- Shows the active loadout with the game's own EN/FR names: driver, skin, chassis, wheels, paint and sticker.
- Edit it part by part, or pick one of the ten special karts (eight Retro colours, Firehawk, Xfinity Flash Kart), which apply their complete configuration.
- Save named presets, apply, rename, delete, import and export them as JSON files to share.
- CLI: `garage show`, `garage export`, `garage apply` and `garage set`.
- Mapped from controlled in-game captures. Confirmed in game: a preset applied to another profile (adding two missing names to the save's name table) and an editor-applied Green Retro Kart. Driver ownership is checked where known; other parts show a warning.

**About and updates**
- About dialog with version, licence, credits and project links.
- Optional update check at startup (contacts api.github.com only; can be disabled in About) and manual check.
- From an extracted release folder: download, SHA-256 verification, install and restart. The previous version is kept in `%LOCALAPPDATA%\ctrnf-save-editor\previous\`, and a failed copy restores it automatically. Save files are never touched.

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
