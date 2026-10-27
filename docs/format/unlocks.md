# Native unlock catalogue

Evidence comes from independent static inspection of PS4 1.21 native predicates,
authored IGZ metadata and EN/FR localization, plus controlled save comparisons.
The game executable, archives and capture files are not distributed. Addresses
below describe that examined image only; they are not universal runtime offsets.

The Unlock Everything XML locates reads of a CBuildData bypass byte. Those
patches bypass normal predicates; they do not themselves write an inventory.
Following the ordinary predicates and authored references provides the mappings.

## Drivers and challenges

Global field 16 is `_unlockedChallenges`, a 512-bit challenge mask. It is not a
512-character roster. The GUI lists 56 active authored drivers. Historical CLI
keys `global.character.bit_N` remain aliases for challenge bits.

| Driver | Local profile condition |
|---|---|
| Ripper Roo | Challenge 4 |
| Papu Papu | Challenge 10 |
| Komodo Joe | Challenge 15 |
| Pinstripe | Challenge 20 |
| Fake Crash | Challenge 65 |
| Nitros Oxide | Challenge 66 |
| King Chicken | Challenge 69, controlled capture |
| Iron Checkpoint Crate | Challenge 70 |
| Penta Penguin | Global bool 3, controlled code capture |
| N. Tropy | Global bool 6, `_beatNefariousTimes` |

The N. Tropy predicate at VA 0xFA39A0 reads global-data byte +0x2B. The named
metadata and packed booleans identify bool 6. Fake Crash and N. Tropy were
subsequently confirmed selectable after restarting in local progression mode.
Changing a reward flag does not create time-trial records or replays.

## Local Pit Stop inventory

`LocalMarketplaceData`, namespace `Marketplace`, is a named root containing an
`igUnsignedShortList`. It stores local item IDs. CMarketplaceData has base ID 100
and 856 item handles: a local ID is **100 + index in that handle list**. Handle
suffixes are not necessarily array indices.

A controlled Koala Kong purchase appended only ID **618**. Handle index 518
resolves to `SystemData_availableDrivers_45`, DriverKKG, Koala Kong; its authored
price is 1500. The mapping independently predicts the observed ID. Baby N. Tropy
is ID **926** and was confirmed selectable after a local-inventory export/restart.

The 856 entries include 39 driver purchases, 159 skins, 39 karts, 202 decals,
185 stickers, 127 paints, 103 customization/wheel items and two presets. These
are marketplace entries, not the entire game content. Default and reward skins
use other conditions. Factual names, IDs and recognized conditions are embedded
in `src/CtrNfSave.Core/Data`; original game archives are not included.

The writer resizes only the recognized ushort allocation, updates its directory
and list counts, preserves the remaining memory and consumes/releases zero
padding while keeping physical file length. Ambiguous roots, shared descriptors,
insufficient/nonzero padding and incomplete graphs are refused. Unknown existing
IDs survive edits. Tests cannot prove every catalogue grant works in-game.

Local inventory and local coin fields can differ from server state and the
amount displayed by the PC port. No server purchase or balance is modified.

## Adventure fields

Slot bool 18 is `_hasWonOxideBossFirst`; bool 19 is `_hasWonOxideBossSecond`.
A controlled rematch set the second flag while track collectible bits 16/18
stayed unchanged. The UI exposes both victories, prerequisites and cascades.
Difficulty is Easy=0, Medium=1, Hard=2, synchronized in header and slot enum 21.

Native slot metadata identifies gem counters 2..6 as red/blue/purple/green/yellow,
field 15 as `_hintsCollected`, 16 as `_cutsceneViewed` and 17 as
`_lastHubZoneIndex`. Advanced door progression remains partially decoded.

Token counter words 10..14 are red/blue/purple/green/yellow. The native
EZoneCollectibleType value array at VA 0x1DADE00 maps red=0x40, blue=0x80,
purple=0x100, green=0x200 and yellow=0x400. Gem-yellow=0x10000 and
Oxide-star=0x40000 are collectible values, distinct from slot victory booleans.

Gem cups won are track bits 12..16 = red/blue/purple/green/yellow, each set on two
tracks: red Crash Cove + Papu's Pyramid, blue Tiger Temple + N. Gin Labs, purple
Roo's Tubes + Hot Air Skyway, green Roo's Tubes + Cortex Castle, yellow Dingo Canyon
+ Oxide Station. A red cup won in game set bit 12 with the red gem word; two exports
with bits {13,14} then {13,15} assigned the others by emptied pedestals. The gem
words alone trigger the Oxide rematch but leave the gems on their pedestals, so the
editor writes both and reports a mismatch.

## Skins and driver-win counters

The 392-skin catalogue covers 56 active drivers: 159 win conditions, 145 Pit Stop
conditions, 65 unconditional skins, 14 challenges, eight generic purchase-linked
conditions and one BeatOxideTimes reward (Digital N. Tropy). The counts classify
skin predicates; they need not equal the marketplace's skin-product count.

The driver-win table is `igStringIntHashTable` referenced by global word 18.
The native hash (VA 0xFA3710) is case-sensitive ASCII FNV-1a; lookup uses modulo
and linear probing. The writer can insert a missing driver key and graph string
without renumbering existing strings or changing physical file size. Full tables
and insufficient padding are refused. Resetting a counter keeps its inserted key.
Thresholds 1/10/25 share a driver's counter, so removing one skin can lock other
skins; the preview shows those related effects.

Digital N. Tropy uses global bool 7, distinct from the driver's bool 6. Editing
reward flags does not create a time record or replay. Serialization/preservation
tests cover the supported edits; exhaustive in-game skin validation is pending.
See [time attack](time-attack.md) and [Champion rewards](champion-rewards.md).
