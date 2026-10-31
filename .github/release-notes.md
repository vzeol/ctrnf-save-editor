## Version 1.1.3

**Fresh PC-port profiles now open correctly.** Some newly created global saves were rejected with "Unrecognized format signature" even though they were decrypted. The editor now recognizes their version-only header and the envelope without a name-hash table, verifying the decoded global root before enabling edits.

- **Unlocks on fresh saves:** an empty local Pit Stop inventory without allocated storage is now recognized. Adding purchases or using Unlock all allocates its first block while preserving existing object and memory IDs.
- **Garage:** accept the game's `systemData` namespace spelling while validating its case-folded hash. Profiles without resolvable active loadout names remain read-only in Garage.
- **Preservation:** retain the original save envelope, unrelated data and file-write/backup protections.

**Validation:** 44 synthetic tests, a warning-free Release build, and a source-data audit. In an in-game check on a fresh PC-port profile, edited unlocks were usable; after changing the driver/kart and saving normally, all 843 granted local purchases and the tracked unlock flags were still present. This confirms the tested case, not every item individually. Server inventory remains separate.

**Already using 1.1.x?** Open **About → Check for updates** to install this version in-app, or use the startup update check.

Extract the Windows x64 ZIP and run `ctrnf-save-gui.exe`; the .NET runtime is included. `ctrnf-save.exe` provides the CLI. A SHA-256 checksum accompanies the archive.

**Credits:** the GUI was inspired by the [OCTR Nitro-Fueled PC launcher](https://github.com/Ryu7w7/OCTR-NitroFueled-PC). Thanks to Ryu7w7 and everyone acknowledged in the [full credits](https://github.com/vzeol/ctrnf-save-editor/blob/main/CREDITS.md), also included in the archive.

Decrypted saves only; keep an independent backup. Some mappings still need in-game validation: see the [feature table](https://github.com/vzeol/ctrnf-save-editor#what-is-available). Contributions, issues and PRs are welcome.

[Guide français](https://github.com/vzeol/ctrnf-save-editor/blob/main/docs/fr/README.md). PolyForm Noncommercial 1.0.0; font/runtime licenses remain separate.
