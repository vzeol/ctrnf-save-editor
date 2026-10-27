# Time attack and customization — 27 October 2026

## Evidence boundary

Independent static inspection of the shipped PS4 1.21 ELF, authored IGZ data and
captured saves. No REA provider was connected; Python and Capstone supplied the
instruction observations. This is not a runtime trace. No game file or live save
was written during this work.

## Ghost progression

Native global field `_zoneInfoGhost` (serialized word 1) points to
`CZoneInfoGhostHashTable`, a string-to-object table. Values are `CGhostSavedInfo`
with two fields: `unlockedChallengeGhostIndex` (word 0) and `_ghost` (word 1).
Native field-name pointer file offset 0x2561420 references the index name at
VA 0x1D01485; the next field is `_ghost`.

In native function VA 0xFCC430, compare/increment at 0xFCC890–0xFCC89F advances
the saved index only when it equals the challenge index just defeated.
The switch at 0xFCC6E6–0xFCC702 maps indices to event names:

| Index | Event | Saved meaning after that win |
|---|---|---|
| 0 | beat_ntropy_ghost | 1: Oxide available |
| 1 | beat_oxide_ghost | 2: Velo available |
| 2 | beat_velo_ghost | 3: developer available |
| 3 | beat_dev_ghost | 4: all four beaten |

The jump table at VA 0x1CC1AE8 targets 0xFCC704, 0xFCC832, 0xFCC795 and
0xFCC7E5 respectively. A supplied developer-complete save has 40 entries all at
4; the earlier user capture has three entries with indices 0, 0 and 1.
These correlate with the static control flow; a new controlled in-game edit
is still required for end-to-end validation.

Only existing saved entries with indices 0–4 are editable. No new track object,
new ghost trajectory or missing challenge record is fabricated. Unsupported
values are preserved. Editing a challenge index changes exactly its four bytes.
Character reward flags are separate and remain unchanged by this edit.

## Records and recorded ghosts

Native map metadata names the three `COctaneMapTimeTrialData` members:
`_mapTimeTrialTimes`, `_mapRelicRaceTimes` and `_mapBestLap` (name table at
file offset 0x25E6C20). The first two are lists, the third is a single record.
Each `COctaneTimeTrialData` contains seconds, driver string index and username
string index. Legacy `global.time_trial.*` read-only aliases remain compatible;
new `global.time_attack.TRACK.CATEGORY.RANK` keys edit the existing time float.

Positive finite times only; float.MaxValue is the empty-record sentinel and
is not exposed as a record. Inputs accept seconds, m:ss.cc and m:ss:cc. List
ranking must remain ordered. No name, driver, stored `CGhost` time or trajectory
is rewritten. The GUI displays the recorded player ghost time separately.

## Customization conditions

`Data/customization-rewards.json` contains 708 unique conditional authored
customization objects, localized EN/FR, including marketplace links and 72
challenge conditions. The UI also retains catalogue marketplace entries not
represented by those authored objects.

Supported grants use the authored predicate: challenge bits (including shared
Adventure rewards), the N. Tropy reward flag, or verified local marketplace
ownership. Champion/developer/Velo reward bytes are now mapped in
[Champion rewards](champion-rewards.md). Unknown cheat/dependency conditions are
shown as unknown/read-only rather than assigned a guessed flag. Bulk grants
skip unknown entries. Known challenge conditions take priority over an item ID
also present in the marketplace catalogue; the same correction applies to skins.
Shared reward removal previews affected characters, skins and customization.

## Verification

Selftest covers all 19 existing samples: byte-identical undo, four-byte isolation
of ghost/time edits, preservation of replays and other records, malformed levels,
non-finite/negative/zero times, formatted times, ranking violations, missing-track
refusal, mixed marketplace/challenge batches and unchanged sample hashes.
20 PASS, 0 FAIL. New exports and GUI layout require user verification in game;
the agent does not launch GUI test windows.
