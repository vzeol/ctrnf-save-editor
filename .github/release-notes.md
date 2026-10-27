## Version 1.0.1

**Gem cup fix.** In 1.0.0, checking a gem only changed the slot's gem counter. The game then offered the Oxide rematch and showed 100 %, but the gems stayed on their pedestals in Gem Stone Valley, as if the cups were not won.

Each gem now also writes its gem cup flag on its two tracks (red, blue, purple, green and yellow, all confirmed in game). Removing a gem clears it, and the checker reports a gem without its flag.

**Already edited a save with 1.0.0?** Open the Adventure slot, click **Normalize**, then save.

Extract the Windows x64 ZIP and run `ctrnf-save-gui.exe`; the .NET runtime is included. `ctrnf-save.exe` provides the CLI. A SHA-256 checksum accompanies the archive.

Close the game and keep an independent backup before testing an edit. Decrypted saves only; server accounts are separate. See the [changelog](https://github.com/vzeol/ctrnf-save-editor/blob/main/CHANGELOG.md), [credits](https://github.com/vzeol/ctrnf-save-editor/blob/main/CREDITS.md) and [known limits](https://github.com/vzeol/ctrnf-save-editor/blob/main/ROADMAP.md).

[Guide français](https://github.com/vzeol/ctrnf-save-editor/blob/main/docs/fr/README.md). Licence PolyForm Noncommercial 1.0.0; commercial use is not granted.
