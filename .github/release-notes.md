## Version 1.1.6

**Xfinity Flash Kart confirmed in game.** On the PC port, with no unlock mod, the save alone is enough: tick **Xfinity Flash Kart** in the global profile's **Kart customization** list (under **Kart**), or use **Unlock only Pit Stop items**, then Save. Without that purchase the kart is not shown at all; with it, the kart is listed with the special karts and selectable.

- The Kart customization page now states this, in English and French. No game-data mod is needed for this kart.
- Includes the special-kart naming fix from 1.1.5.

**Validation:** in-game removal and restoration of this one purchase on a real profile; only the two Xfinity fields changed out of 2,778 decoded fields. Release build without warnings and 45 synthetic tests pass. Other promotional items were not checked individually.

**Already using 1.1.x?** Open **About → Check for updates** to install this version in-app, or use the startup update check.

Extract the Windows x64 ZIP and run `ctrnf-save-gui.exe`; the .NET runtime is included. `ctrnf-save.exe` provides the CLI. A SHA-256 checksum accompanies the archive.

**Credits:** the GUI was inspired by the [OCTR Nitro-Fueled PC launcher](https://github.com/Ryu7w7/OCTR-NitroFueled-PC). Thanks to Ryu7w7 and everyone acknowledged in the [full credits](https://github.com/vzeol/ctrnf-save-editor/blob/main/CREDITS.md), also included in the archive.

Decrypted saves only; keep an independent backup. Some mappings still need in-game validation: see the [feature table](https://github.com/vzeol/ctrnf-save-editor#what-is-available). Contributions, issues and PRs are welcome.

[Guide français](https://github.com/vzeol/ctrnf-save-editor/blob/main/docs/fr/README.md). PolyForm Noncommercial 1.0.0; font/runtime licenses remain separate.
