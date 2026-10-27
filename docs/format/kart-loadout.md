# Global kart loadouts and Garage

## Controlled in-game findings

Arcade captures made with one change between saves identify the active kart in
`COctaneGlobalSaveData` word 0: a `COctaneCustomizationSaveDataList` with four
object references. Exactly one ten-word `COctaneCustomizationSaveData` is active.
Adventure slots retain their own configuration; Garage changes only Global.

| Word | Serialized value | Confirmed capture observations |
|---|---|---|
| 0–1 | Driver handle: name hash, namespace hash | `_5` = Komodo Joe; authored data corrects `SystemData_availableDrivers_58` to Iron Checkpoint Crate (`DriverHereIAm`), not Crunch |
| 2 | Driver skin index | 0 default; 5 Komodo Joe Wizard |
| 3–4 | Chassis handle: name hash, namespace hash | `SystemData_availableKarts_0` Classic; `_7` Team Bandicoot; `_15` Retro Blue; `_32` Firehawk |
| 5 | Wheels index | 0 Classic; 15 Cabrio |
| 6 | Paint index | 0 blue; 6 violet |
| 7 | Unknown signed word | -1 normal/Retro Blue; 0 Firehawk |
| 8 | Sticker index | 0 none; 57 Zero black |
| 9 | Unknown signed word | -1 normal; 0 Retro Blue; 8 Firehawk |

There are eight native fields: two eight-byte handles and **six scalar ints**, or
ten serialized 32-bit words. Both handles use namespace `SystemData`, hash
`0x6236AC84`. Empty records in the examined captures are exactly
`0,0,-1,0,0,-1,0,-1,-1,-1` (word 6 is zero). Other empty patterns and ambiguous
active records are refused rather than normalized.

Special chassis impose a complete tuple. Retro Blue uses wheels 9, paint 16,
word 7 = -1, sticker 0, word 9 = 0. Firehawk uses wheels 0, paint 99,
word 7 = 0, sticker 0, word 9 = 8. Garage copies the full tuple without attempting
to interpret words 7/9 or recombine special parts. Their exact meanings remain
unknown. The captured mappings are confirmed in game. An editor export that applied
a full preset (Komodo Joe Wizard, Team Bandicoot, Cabrio, violet, Zero black) to a
different profile, adding two missing names to the name table (3 to 5 entries),
loaded in game with that exact loadout and the profile intact.

## Embedded names and reproducible extraction

`src/CtrNfSave.Core/Data/loadouts.json` contains factual names and IDs from PS4
1.21 `systemData.igz`, `systemData_en.lng`, `systemData_fr.lng` and the corresponding
`MarketplaceData_en.lng` / `MarketplaceData_fr.lng`. No archives, artwork or private
saves are embedded. The generator uses the user's external `igz_file.py` (`Igz`)
reader; that reader is not copied into this repository. Input files are read-only.

```powershell
python scripts/build-loadout-catalogue.py --input <extracted-catalogue-directory> --reader-dir <igz-reader-directory> --output src/CtrNfSave.Core/Data/loadouts.json
```

The output must be outside the input directory. Regeneration is deterministic;
controlled name checks fail generation on disagreement instead of substituting
hand-written labels. The application uses the selected EN/FR translation and
falls back to the exact handle or scalar value when a translation is missing.
`garage show <global.sav> --lang fr` selects French; the CLI defaults to English
and retains raw preset JSON after the readable names.

| List | Mapping method | Catalogue coverage |
|---|---|---|
| Drivers | Pair ONAM `igNameList` names with the root `igObjectList`; `availableDrivers_N` suffix identifies the saved handle, not the existing unlock catalogue ordinal. Read native identity at `CRacerDriverData +0x58`. | 67 numeric handles plus four active named handles; 71 total |
| Skins | Follow each driver's `+0x98` skin-list reference; use the actual zero-based list position, then localize that object's handle. | 407 entries, including unused/default placeholders |
| Chassis | Resolve named `CRacerKartData` roots, with the same distinction between numeric suffix and list position. | 48 numeric handles plus seven active named handles; 55 total |
| Wheels | Actual position in `kartWheelsCustomizationData`, not its handle suffix. Position 15 is handle `_6` (Cabrio). | 111 entries; the earlier estimate of 57 was incomplete |
| Paints | Actual position in `materialOverrideValuesList`; color/material groups are UI groupings, not save indices. | 165 entries |
| Stickers | Actual position in `stickers`, including `noSticker` at position 0. Position 57 is `Sticker_0_BlackOnWhite`. | 250 entries |
| Special presets | Position in `customizationPresets`; follow `CKartCustomizationPresetItem +0x58` to the component handles. | 10 authored compositions |

`index` is the numeric handle suffix for drivers/chassis (null for independently
named handles), or the actual list position for parts/skins/presets. `listIndex`
also records driver/chassis list position where present, without conflating it
with the handle suffix. Active names such as `DriverSpyro` and `kartChampion`
are accepted only through exact catalogue membership. Unknown canonical numeric
handles remain representable, with raw display and unknown ownership.

Driver/chassis localization prefers `CRacerBaseData::_displayName`, then
`CMarketplaceItem::_storeDisplayName`; parts, skins and special presets use the
store display name. This matters for chassis 15: the chassis label is **Retro /
Rétro**, while the complete controlled special tuple uses preset 0's **Blue Retro
Kart / Kart rétro bleu**. Garage uses the latter only when the entire controlled
tuple matches the authored composition. An incomplete tuple keeps the chassis
label. The French localization's accents/case are retained exactly.

### Ground-truth checks

These tests compare the embedded catalogue with the supplied controlled captures;
they do not claim fresh in-game validation of the full catalogue.

| Saved value | EN from localization | FR from localization | Result |
|---|---|---|---|
| Driver 5 | Komodo Joe | Komodo Joe | Match |
| Komodo Joe skin 0 | Komodo Joe | Komodo Joe | Default skin, match |
| Komodo Joe skin 5 | Wizard Komodo Joe | Komodo Joe Sorcier | Match |
| Chassis 0 | Classic | Classique | Match |
| Chassis 7 | Team Bandicoot | Team Bandicoot | Match |
| Chassis 15, complete special tuple | Blue Retro Kart | Kart rétro bleu | Match (localized accents/case) |
| Chassis 32, complete special tuple | Firehawk | Faucon de feu | Match |
| Wheels 0 / 15 | Classic / Cabrio | Classique / Cabrio | Match |
| Paint 0 / 6 | Blue / Purple | Bleu / Violet | Match |
| Sticker 0 / 57 | No Sticker / Zero Black | Pas d'autocollant / Zéro noir | Match |
| Driver 48, authored identity | King Chicken | Roi Poulet | `DriverKCN`; static cross-check |
| Driver 58, authored identity | Iron Checkpoint Crate | Caisse Point de contrôle | `DriverHereIAm`; previous Crunch claim corrected |

### Words 7/9 and missing translations

The data supports **word 7 as a candidate per-kart decal position** and **word 9
as a candidate customization-preset position**, rather than two decal positions.
The `KartLimited3` decal list has one entry (position 0); Firehawk's word 9 = 8
cannot index that list. Authored preset 8 instead references chassis 32, paint 99
and `decals_KartLimited3_0`, with default wheels/sticker. Preset 0 references
chassis 15, paint 16 and wheels-list position 9, with no decal. These compositions
reproduce both controlled tuples. There is no named `decals_KartLegacy` root in
the inspected SystemData directory. Save-field/native-code evidence linking the
scalar words to these lists is still missing. No independent decal API or decal
labels are exposed; words 7/9 remain signed, raw and preserved.

Unlocalized entries retain raw fallback: driver handle 41; chassis handles
18, 19, 24–30; paint position 99; sticker positions 1 and 2. Eleven skin-0 entries
(driver handles 37 and 60–69) also lack translations. These include virtual or
placeholder content; their internal strings are not promoted to in-game labels.
In particular, Firehawk's paint 99 has no localized name in these files. Its
special chassis label is known, but a paint label is not fabricated. No provided
ground-truth name pair failed; exhaustive in-game verification remains pending.

## Names and safe rewriting

The `graph_names` section is `u32 size, u32 version, u32 count`, followed by all
name hashes and then the corresponding NUL-terminated strings. It is not the
graph string table. Lowercase ASCII FNV-1a (initial 2166136261, multiplier
16777619, wrapping u32) matches the captured pairs, including:

| Name | Hash |
|---|---|
| `SystemData` | `6236AC84` |
| `SystemData_availableDrivers_5` | `A2F77BC3` |
| `SystemData_availableDrivers_58` | `86982C21` |
| `SystemData_availableKarts_0` | `A40D14D4` |
| `SystemData_availableKarts_7` | `A30D1341` |
| `SystemData_availableKarts_15` | `259A2316` |
| `SystemData_availableKarts_32` | `B89F87AD` |

Applying resolves names by hash and refuses collisions, duplicate/ambiguous
entries or existing hashes outside this verified algorithm. Missing hashes are
inserted at the end of the hash array, and their names appended after existing
strings, retaining existing pairs and the section version. Only size/count change.
Every following section moves together into verified trailing zero padding,
following the existing marketplace/string growth strategy. Physical file size,
allocation IDs, object IDs and section lengths other than `graph_names` remain
unchanged. References use graph IDs rather than absolute file offsets. All
following serialized bytes are copied verbatim, and offsets for the active record
are obtained from a fresh parse. Nonzero trailers or insufficient capacity refuse
growth. A final parse and loadout equality check must succeed before returning a
new in-memory `Save`; Core performs no disk writes.

## Part editing

`KartLoadout.Edit` validates supplied choices against the embedded catalogue,
composes a full `KartPreset` with `PrepareEdit`, then reuses `Apply`, including
safe graph-name additions. Omitted fields remain unchanged. A changed driver
resets skin to 0 unless an explicit valid skin for that driver is supplied.
Pickable drivers require active `availableDrivers` list membership before the
developer entries (list positions 56 onward); virtual and inactive placeholder
roots are excluded. Normal chassis require active list membership and must not
be referenced by an authored special composition. Named active handles are
supported by Core and GUI; the CLI's numeric driver/chassis options use handle
suffixes. Wheels, paints, stickers and skins use their actual catalogue indices.

Only special catalogue entries with explicit `word7`, `word9` and `evidence` are
pickable. Currently these are controlled Blue Retro Kart and Firehawk; the
generator checks their authored components before attaching captured word values.
Other preset list positions and decal handles never imply unknown save words.
Special selection copies all six chassis-related fields exactly and refuses
simultaneous part overrides. While a special chassis is active, part-only edits
are refused. Choosing a normal chassis sets words 7/9 to -1. Leaving a special
kart also resets wheels, paint and sticker to 0 (its own parts, such as Firehawk paint 99,
must not stay on a normal chassis) unless explicitly replaced; between normal chassis
they are retained. Driver/skin edits preserve the
complete special tuple. Existing unknown values are displayed and preserved when
omitted, rather than promoted to new catalogue choices.

Synthetic tests exercise each part, driver-dependent skin validation, both
special tuples, return to normal, rejection and unrelated-byte preservation.
The private regression also compares all ten words after g07_skin to Firehawk
and g09_firehawk to Team Bandicoot/Cabrio/Purple/Zero Black edits. This is capture
regression evidence, not fresh in-game confirmation of arbitrary combinations.

## Presets and ownership limits

Format version 1 JSON stores `name`, `driver`, `skin`, `chassis`, `wheels`, `paint`,
`word7`, `sticker`, `word9`, `formatVersion`. Every member is required. Names are
canonical SystemData driver/chassis names, known indices are nonnegative signed
32-bit ints, and words 7/9 retain all signed bits. Unknown members, duplicate keys,
missing/null values, wrong types and unsupported versions are rejected. This
validates structure; it does not prove that an arbitrary imported combination
exists or is usable in game.

The authored native driver identity provides a verified crosswalk to existing
local driver ownership predicates, independently of catalogue ordinal. Driver 58
therefore uses Iron Checkpoint Crate's challenge 70, and driver 48 uses King
Chicken's challenge 69. Missing identities retain unknown ownership. Chassis
ownership is explicitly unchecked. Skin,
wheels, paint and sticker ownership is also unchecked. Locked driver warnings
do not prevent applying; Garage grants no unlocks and does not alter server data.

Synthetic tests verify hash pairs, strict JSON, safe growth/refusal, record
ambiguity and preservation outside the active record/name table. The optional
private regression applies the wheels capture's preset to the Firehawk capture,
checks the complete tuple and compares all unrelated serialized graph bytes after
accounting for section relocation. Private saves remain read-only and excluded
from source.
