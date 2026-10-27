# Global kart loadouts and Garage

## Controlled in-game findings

Arcade captures made with one change between saves identify the active kart in
`COctaneGlobalSaveData` word 0: a `COctaneCustomizationSaveDataList` with four
object references. Exactly one ten-word `COctaneCustomizationSaveData` is active.
Adventure slots retain their own configuration; Garage changes only Global.

| Word | Serialized value | Confirmed capture observations |
|---|---|---|
| 0–1 | Driver handle: name hash, namespace hash | `SystemData_availableDrivers_58` = Crunch; `_5` = Komodo Joe |
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
unknown. The captured mappings are confirmed in game; new editor exports still
require in-game validation.

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

## Presets and ownership limits

Format version 1 JSON stores `name`, `driver`, `skin`, `chassis`, `wheels`, `paint`,
`word7`, `sticker`, `word9`, `formatVersion`. Every member is required. Names are
canonical SystemData driver/chassis names, known indices are nonnegative signed
32-bit ints, and words 7/9 retain all signed bits. Unknown members, duplicate keys,
missing/null values, wrong types and unsupported versions are rejected. This
validates structure; it does not prove that an arbitrary imported combination
exists or is usable in game.

Catalogue driver ordinals and authored skin handles do **not** match the captured
loadout indices. Only capture-confirmed 5/58 are linked to the catalogue identities
for Komodo Joe/Crunch, localized labels and existing local ownership predicates.
Other drivers keep their raw name and unknown ownership. There is no verified
general chassis crosswalk, so chassis ownership is explicitly unchecked. Skin,
wheels, paint and sticker ownership is also unchecked. Locked driver warnings
do not prevent applying; Garage grants no unlocks and does not alter server data.

Synthetic tests verify hash pairs, strict JSON, safe growth/refusal, record
ambiguity and preservation outside the active record/name table. The optional
private regression applies the wheels capture's preset to the Firehawk capture,
checks the complete tuple and compares all unrelated serialized graph bytes after
accounting for section relocation. Private saves remain read-only and excluded
from source.
