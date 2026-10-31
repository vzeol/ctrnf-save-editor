## Version 1.1.4

**Unlock only Pit Stop items** is a new global-profile action, in English and French. It adds only the **787 catalogue entries explicitly marked as Pit Stop purchases**, with a purchase-by-purchase preview. Confirm to apply in memory, then Save separately.

- **Preserve progression rewards:** Adventure, challenge and time-trial reward flags, driver-win counters, other purchases, loadouts, coins, eggs and records remain unchanged. Existing ownership is retained; this action adds Pit Stop purchases only.
- **Local inventory only:** it does not add server purchases or change the coin balance on a private server.
- **CLI:** `set <global.sav> global.unlock_pit_stop true -o <edited.sav>`. Use `false` to remove only those purchases while preserving other ownership.
- Includes the fresh-save and Garage compatibility fixes from 1.1.3.

**Validation:** 45 synthetic tests cover the exact filter, complete preview, preservation of existing objects and memory, fresh unallocated inventory, repeated operations and invalid inputs. Release build and source-data audit pass. The new filtered action has not yet received a separate in-game check; earlier local unlock compatibility was checked in 1.1.3.

**Already using 1.1.x?** Open **About → Check for updates** to install this version in-app, or use the startup update check.

Extract the Windows x64 ZIP and run `ctrnf-save-gui.exe`; the .NET runtime is included. `ctrnf-save.exe` provides the CLI. A SHA-256 checksum accompanies the archive.

**Credits:** the GUI was inspired by the [OCTR Nitro-Fueled PC launcher](https://github.com/Ryu7w7/OCTR-NitroFueled-PC). Thanks to Ryu7w7 and everyone acknowledged in the [full credits](https://github.com/vzeol/ctrnf-save-editor/blob/main/CREDITS.md), also included in the archive.

Decrypted saves only; keep an independent backup. Some mappings still need in-game validation: see the [feature table](https://github.com/vzeol/ctrnf-save-editor#what-is-available). Contributions, issues and PRs are welcome.

[Guide français](https://github.com/vzeol/ctrnf-save-editor/blob/main/docs/fr/README.md). PolyForm Noncommercial 1.0.0; font/runtime licenses remain separate.
