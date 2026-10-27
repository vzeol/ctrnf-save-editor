## Version 1.1.2

The welcome screen now explains **normal saves versus saves used with mods**, in English and French, with both relative paths for Adventure slots and the global profile.

- Normal saves: `user\savedata\1\CUSA13795`.
- Persistent mod profile: `mod-state\user\savedata\1\CUSA13795`.
- Each mod launch creates a session copy; after a normal exit, it is committed to the persistent mod profile. Close the game before editing that profile. Normal and mod profiles are not automatically synchronized with each other.

This update only clarifies the interface and documentation. It does not copy your saves or change save-editing behavior. The Garage, About dialog, in-app updater and high-DPI improvements from 1.1.1 are retained.

**Already using 1.1.x?** Open **About → Check for updates** to install this version in-app, or use the startup update check.

Extract the Windows x64 ZIP and run `ctrnf-save-gui.exe`; the .NET runtime is included. `ctrnf-save.exe` provides the CLI. A SHA-256 checksum accompanies the archive.

**Credits:** the GUI was inspired by the [OCTR Nitro-Fueled PC launcher](https://github.com/Ryu7w7/OCTR-NitroFueled-PC). Thanks to Ryu7w7 and everyone acknowledged in the [full credits](https://github.com/vzeol/ctrnf-save-editor/blob/main/CREDITS.md), also included in the archive.

Decrypted saves only; keep an independent backup. Some mappings still need in-game validation: see the [feature table](https://github.com/vzeol/ctrnf-save-editor#what-is-available). Contributions, issues and PRs are welcome.

[Guide français](https://github.com/vzeol/ctrnf-save-editor/blob/main/docs/fr/README.md). PolyForm Noncommercial 1.0.0; font/runtime licenses remain separate.
