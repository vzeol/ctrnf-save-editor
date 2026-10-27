# Usage

## Windows interface

Close the game, make an independent backup and open a decrypted save copy. Choose Adventure for `savedata1` through `savedata4`, or global for `savedata0`. Language is selectable in the toolbar.

Adventure pages separate story progression from advanced fields. Trophy, token and relic edits can trigger prerequisite/removal cascades. Inspect the summary and consistency checker before saving. Oxide has two victory flags; completing only one shows `1 / 2`.

The global view provides drivers, skins, customization and time attack. Filters narrow catalogues. Unlock all and Lock all show affected entries before applying. Base content stays available; shared counters/conditions can affect multiple items, which the preview explains. Coin and egg values are separate.

Use **Save as** to export a first test, then install it with the game closed. In-place save creates a backup and rejects an externally changed file. Do not delete older backups as part of routine editing.

Server progression is separate from local-save progression. Use the game's local inventory mode to inspect local edits. This editor cannot grant server purchases or synchronize an online account.

## Saves with mods

The launcher protects normal saves by using a separate persistent mod profile:
`mod-state\user\savedata\1\CUSA13795`. Use `savedata0\SYS-DATA.sav` for its
global profile or `savedata1…4\SYS-DATA.sav` for its Adventure slots. The user
number and title ID may differ in another installation.

Each mod launch creates a session copy under
`mod-state\sessions\<session>\user\savedata`. On a normal exit, the launcher
commits that session's saves to the persistent mod profile. Close the game before
editing the persistent profile so a running session cannot overwrite your edit.
Normal and mod profiles do not automatically synchronize with each other.
The editor explains both locations; it does not copy or synchronize saves for you.

## Time attack

Only existing records and ghost entries are editable. Index `0` means N. Tropy is available; `1` makes Oxide available, `2` Velo, `3` the developer, and `4` means all those challenges beaten for that existing entry. A changed index does not generate a replay or fill missing circuits. Shared reward flags are separate named controls.

Times accept seconds, `m:ss.cc` or `m:ss:cc`. A modified displayed time leaves the recorded trajectory unchanged. Do not treat an edited record as proof of a driven time.

## Garage

In **Current loadout**, choose each part from the catalogue, then **Apply changes**
to stage the edit in the open profile. **Reset** restores the current saved-in-memory
loadout and discards pending picker changes. Choosing another driver resets the
skin to 0 and shows that driver's skin list. Names follow the selected EN/FR
language; missing translations and existing unknown values use raw fallback.
Virtual, developer and inactive driver entries are excluded from new choices.

The chassis picker also offers the ten special karts (eight Retro colours, **Firehawk**
and the **Xfinity Flash Kart**). Each copies its complete tuple; wheels, paint and sticker pickers are disabled while
it is selected. Choose a normal chassis to enable them again: words 7 and 9 return
to -1, and the special kart's wheels, paint and sticker reset to the defaults. No independent word-7/9 controls
are exposed.

In the global profile, select **Garage**, then **Save current as preset** and enter
a name. Select a preset to **Apply**, **Rename**, **Delete**, or **Export JSON**.
**Import JSON** validates a preset before adding it to the local list. Duplicate
display names are allowed; internal filenames are generated independently of
names. Invalid/unreadable preset files are skipped with a visible count. Presets
live in `%LOCALAPPDATA%\ctrnf-save-editor\garage\` and contain loadout fields only.

Applying changes the open save in memory. Use **Save** or **Save as** to write it
with the normal backup and external-change protections. Saving/renaming/importing
a preset changes only the Garage library, not the open save. Special chassis copy
their entire captured settings together, including the two unknown words.

Known locked drivers show a warning without blocking Apply. Driver ownership uses
the authored identity and existing local ownership predicates where known;
otherwise it is marked unchecked. Chassis, skin and other parts ownership is not
checked; Garage does not unlock anything. Server inventory remains separate.
Unknown/ambiguous save layouts and unsafe name-table growth are refused. Imported
JSON is structurally validated, not a guarantee of a usable in-game combination.

```powershell
./ctrnf-save.exe garage show ./global-copy.sav
./ctrnf-save.exe garage export ./global-copy.sav ./wizard.json --name "Wizard kart"
./ctrnf-save.exe garage apply ./global-copy.sav ./wizard.json -o ./garage-edited.sav
./ctrnf-save.exe garage set ./global-copy.sav -o ./parts.sav --driver 5 --skin 5 --chassis 7 --wheels 15 --paint 6 --sticker 57
./ctrnf-save.exe garage set ./global-copy.sav -o ./firehawk.sav --special "Firehawk"
```

`garage set <global.sav> -o <out.sav>` requires at least one of `--driver N`,
`--skin N`, `--chassis N`, `--wheels N`, `--paint N`, `--sticker N` or
`--special NAME`. Numeric values are catalogue save IDs, not localized labels or
list ordinals. Skin is validated against the selected driver's list; an explicit
`--skin` can select a skin after a driver change. Special names accept the exact
EN/FR name or catalogue handle, case-insensitively, and cannot be combined with
chassis/wheels/paint/sticker options. To edit a special kart's parts, provide a
normal `--chassis` in the same command. Unknown values, inactive drivers and
incomplete specials are rejected before writing an output.

CLI exports refuse existing outputs. Apply and set create a timestamped original-source
backup beside the output. Details: [kart loadout format](format/kart-loadout.md).

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
