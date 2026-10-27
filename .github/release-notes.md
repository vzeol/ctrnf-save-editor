## Version 1.1.1

**Display fixes for high-DPI screens.** At 150 % or 200 % Windows scaling, some controls kept their 100 % pixel sizes:
- the About dialog cut off its **Check for updates** and **Close** buttons;
- section names were truncated ("Kart customi…", "Skins by dri…");
- summary tiles showed "16 /" instead of "16 / 16";
- the first track of each Adventure hub hid its trophy and CTR token checkboxes;
- Adventure settings boxes clipped their buttons, and the header could leave a ghost title after a resize.

All of these now scale with the display. No save-editing behaviour changed. The README now has screenshots.

**Using 1.1.0?** Open **About → Check for updates** (or wait for the startup check) to install this version in-app. This is the first release delivered through the built-in updater.

Extract the Windows x64 ZIP and run `ctrnf-save-gui.exe`; the .NET runtime is included. `ctrnf-save.exe` provides the CLI. A SHA-256 checksum accompanies the archive.

See the [changelog](https://github.com/vzeol/ctrnf-save-editor/blob/main/CHANGELOG.md), [credits](https://github.com/vzeol/ctrnf-save-editor/blob/main/CREDITS.md) and [known limits](https://github.com/vzeol/ctrnf-save-editor/blob/main/ROADMAP.md). [Guide français](https://github.com/vzeol/ctrnf-save-editor/blob/main/docs/fr/README.md). Licence PolyForm Noncommercial 1.0.0; commercial use is not granted.
