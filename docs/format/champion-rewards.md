# Champion rewards and bulk unlocks — 27 October 2026

## Native evidence

Independent static ELF inspection with Capstone, not an execution trace or REA
output. The game archives, executable and real saves were not changed.

The `COctaneChampionKartUnlockable` vtable at VA 0x259C3B0 has its predicate at
0xFA4A10. That function calls the local-marketplace predicate 0xFA4610, then ORs
its result with CGameState byte +0xD0C. The authored kart, wheels and paint all
reference this condition (local IDs 411, 412, 413). The same byte is read by
`COctaneBeatAllOxideTimesUnlockable` at VA 0xFA4110 for Oxide's Champion decal.

The patch loader at VA 0xC1C420 copies up to 0xD18 bytes from the PatchTU1
igCharList. Its caller at VA 0xF9DD22 supplies `CGameState + 0xC`, so the serialized
offset is the runtime field offset minus the 12-byte object header.

| Reward | Runtime offset | PatchTU1 offset | Predicate |
|---|---|---|---|
| All Oxide times, CTR/CNK/bonus; Champion set | 0xD0C | 0xD00 (3328) | 0xFA4110 / 0xFA4A10 |
| All Velo times; Velo's decal | 0xD0D | 0xD01 (3329) | 0xFA4390 |
| Developer times; Master wheels | 0xD00 | 0xCF4 (3316) | 0xFA3EA0 |

The current user profile was copied once while authorized for read-only access;
source hashes before/after the copy matched the captured file. All three bytes
are 0, and IDs 411–413 are absent. The developer-complete reference has all three
bytes at 1 while those local IDs are also absent. This corroborates the native
reward path independently of purchases. A new in-game edit remains unverified.

Only a named, unique 3352-byte PatchTU1 char list is accepted. Older 60-byte
patches keep these conditions read-only. Writes change only the meaningful byte.
Disabling an individual Champion item clears the shared reward and removes that
item's alternate local ownership ID if present; otherwise it could stay unlocked.
Shared removals are shown in the GUI preview.

## Bulk scope and preview

The Global profile page has Unlock all / Lock all for known editable characters,
skins and customization. Character actions cover that section; skin actions cover
the selected driver; customization actions respect the displayed search/category.
Base characters/skins, unknown conditions and inventory IDs, coins, eggs, records,
ghost progress/replays and Adventure slots are excluded. Shared known reward
conditions can affect several items or a character, all shown before application.

The operation prepares a new in-memory save and commits it only after the user
accepts the preview. Saving to disk remains a separate explicit action with the
normal backup behavior. Marketplace growth/shrink occurs once; driver-win
thresholds are aggregated per driver. Structural changes finish before direct
byte edits, so offsets are read from the final graph. Race-win counters are listed
in the preview, including reductions needed to lock win-based skins.

The game may award a cleared reward again after another race if its existing
challenge progress still meets the original requirement. Bulk cosmetic actions
deliberately preserve those race results and ghost states.

CLI: `set INPUT global.unlock_all true -o OUTPUT` or `false` to lock. Individual
reward keys: `global.reward.all_oxide_times`, `global.reward.velo_times`,
`global.reward.developer_times`.

## Validation

20 PASS, 0 FAIL over the 19 existing samples, including Champion shared/alternate
ownership paths, one-byte isolation and undo, older-patch refusal, full unlock
then lock, base content and unknown-data preservation, idempotent lock and
unchanged source bytes. Published CLI also completed unlock/lock exports from
the current read-only user capture; semantic differences were restricted to
known conditions/counters and their raw aliases. The capture hash stayed intact.
No exported test file was installed in the game.
