## Version 1.1.0

**Garage.** The game keeps only one kart loadout. Open your **global profile** (savedata0) and select **Garage** to:
- see your current loadout with the game's own names (driver, skin, chassis, wheels, paint, sticker), in English or French;
- change it part by part, or pick one of the **ten special karts** (eight Retro colours, Firehawk, Xfinity Flash Kart);
- save it as a named **preset**, re-apply it in one click, and share presets as JSON files.

Click **Save** afterwards: a backup is made as usual. Every mapping comes from controlled in-game captures, and editor-applied loadouts were confirmed in game. Driver ownership is checked where known; other parts show a warning instead.

**About and automatic updates.** A new About dialog shows the version, credits and links. From this version on, the editor can check GitHub for new releases at startup (api.github.com only; can be turned off in About), then download, verify (SHA-256), install and restart. The previous version is kept so you can roll back. This release must still be downloaded manually once.

Extract the Windows x64 ZIP and run `ctrnf-save-gui.exe`; the .NET runtime is included. `ctrnf-save.exe` provides the CLI. A SHA-256 checksum accompanies the archive.

Close the game and keep an independent backup before testing an edit. Decrypted saves only; server accounts are separate. See the [changelog](https://github.com/vzeol/ctrnf-save-editor/blob/main/CHANGELOG.md), [credits](https://github.com/vzeol/ctrnf-save-editor/blob/main/CREDITS.md) and [known limits](https://github.com/vzeol/ctrnf-save-editor/blob/main/ROADMAP.md).

[Guide français](https://github.com/vzeol/ctrnf-save-editor/blob/main/docs/fr/README.md). Licence PolyForm Noncommercial 1.0.0; commercial use is not granted.
