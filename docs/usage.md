# Usage

## Windows interface

Close the game, make an independent backup and open a decrypted save copy. Choose Adventure for `savedata1` through `savedata4`, or global for `savedata0`. Language is selectable in the toolbar.

Adventure pages separate story progression from advanced fields. Trophy, token and relic edits can trigger prerequisite/removal cascades. Inspect the summary and consistency checker before saving. Oxide has two victory flags; completing only one shows `1 / 2`.

The global view provides drivers, skins, customization and time attack. Filters narrow catalogues. Unlock all and Lock all show affected entries before applying. Base content stays available; shared counters/conditions can affect multiple items, which the preview explains. Coin and egg values are separate.

Use **Save as** to export a first test, then install it with the game closed. In-place save creates a backup and rejects an externally changed file. Do not delete older backups as part of routine editing.

Server progression is separate from local-save progression. Use the game's local inventory mode to inspect local edits. This editor cannot grant server purchases or synchronize an online account.

## Time attack

Only existing records and ghost entries are editable. Index `0` means N. Tropy is available; `1` makes Oxide available, `2` Velo, `3` the developer, and `4` means all those challenges beaten for that existing entry. A changed index does not generate a replay or fill missing circuits. Shared reward flags are separate named controls.

Times accept seconds, `m:ss.cc` or `m:ss:cc`. A modified displayed time leaves the recorded trajectory unchanged. Do not treat an edited record as proof of a driven time.

## Command line

```powershell
./ctrnf-save.exe dump ./copy.sav --json
./ctrnf-save.exe set ./copy.sav global.wumpa_coins 1500 -o ./edited.sav
./ctrnf-save.exe diff ./copy.sav ./edited.sav --json
./ctrnf-save.exe check ./adventure-copy.sav
```

Use the supported keys from `dump --json`; unknown/raw fields require particular care. `--in-place` explicitly replaces the opened file after creating a backup. `-o` refuses to replace an existing output. Global exports keep an original-source backup too.

Exit codes: `0` success, `1` consistency errors from `check`, `2` unsupported input or command/file errors. Run `--help` for the complete key families and syntax.

## Artwork

The app works with its built-in drawn header and bundled licensed font. No official game artwork is distributed. If you already possess artwork you may use locally, the header can discover `art/ctr-hero.png` and optional `art/ctr-logo.png` beside the app or an ancestor directory. Do not submit these assets to the repository.
