# Save format overview

Supported inputs are decrypted PS4 CTR Nitro-Fueled binary saves. Adventure slots are 153,600 bytes and global profiles 7,340,032 bytes in the examined layouts. Size alone is not sufficient: graph structure and recognized class layouts are validated before editing.

The container holds a summary header followed by an object graph: directory entries, allocation order, objects, typed memory, strings, metadata and class records. References are graph indices rather than runtime pointers. Serialization preserves uninterpreted bytes and rebuilds supported resized lists while keeping the fixed physical container size.

Adventure `COctaneSaveSlotData` stores summary/progression values; per-zone session objects hold event flags. The first and second Oxide wins are distinct slot fields (18 and 19 in the supported layout). Difficulty is synchronized with the header. Many door/progression fields remain incompletely mapped.

Global `COctaneGlobalSaveData` contains coin, challenge, driver-win and ghost-table references. `_unlockedChallenges` is a 512-bit challenge mask. Named marketplace inventory is a list of local ushort IDs. Recognized PatchTU1 bytes represent eggs and later reward conditions; patch layout is checked before mutations.

Read [unlocks](unlocks.md), [time attack](time-attack.md) and [Champion rewards](champion-rewards.md) for exact evidence boundaries. Source code is the authority for accepted layouts. Do not reuse executable addresses across unexamined versions: the documented addresses refer to the inspected PS4 1.21 image, not a universal ABI.

The repository contains synthetic fixtures only. Historical controlled captures supported individual findings but are not distributed. Unknown conditions remain preserved/read-only; a native predicate mapping is not equivalent to exhaustive in-game validation.
