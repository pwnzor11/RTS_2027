# aoc-mgz: how an Age of Empires II replay parser reads a command stream

**Summary**

- aoc-mgz (Python package `mgz`, MIT, v1.8.51) parses AoE II recorded games. It does not simulate the game. It reads a header snapshot of the starting state and then a linear stream of player commands, sync records and chat.
- The body is a lockstep command stream. Each command names the player, the selected unit IDs and a target (coordinates or object ID). Nothing in the file says where units end up.
- Sync records carry a time increment and, periodically, a checksum. Summary duration is the sum of sync increments. Pre-DE checksums are only collected (the first four, hashed for match identity), never verified against a simulation.
- Winner, completion, eAPM and age-up times are derived from command counts, resignations, postgame blocks and in-game chat text. Resources over time exist only for DE, from a sync field whose meaning the authors call "just guesses".
- Anything about positions, HP, resource totals or combat needs a re-simulation. The README says so directly: you cannot know resources or kills at a point in time "without replaying the match in-game".
- Command-only replays are bound to the exact simulation build and rules. The code is full of per-version branches that show how often the format changed.
- For RTS_2027: store a real state hash (not a sum of stats) with a build hash in the header, keep the body minimal, add a footer index, and derive statistics with a headless run instead of in the parser.

## 1. What it is and its status

- Licence: MIT, copyright 2021 happyleavesaoc (`/home/claude/ref/aoc-mgz/LICENSE` line 1). MIT code can be included in a GPL v3 project, so borrowing from it is legally simple.
- Version 1.8.51 (`setup.py:6`). Its only hard dependency is `construct==2.8.16` (`setup.py:15`), an old pin.
- Supported files (README "Supported Versions"): Age of Kings `.mgl`, The Conquerors `.mgx`, UserPatch 1.4 and 1.5 `.mgz`, HD Edition 4.6 and later, and Definitive Edition `.aoe2record`.
- The README support matrix shows the limits. Full-body parsing stops at UP 1.5, HD 5.8 and DE up to build 26.21. Later DE files get only the summary and fast body paths. The high-level `model` API only covers UP 1.5, HD 5.8 and DE up to 26.21. This is a moving target that the project has chased for 15 years.
- Two parsers are offered: `fast` (hand-written `struct` code) and `full` (declarative `construct` schemas). The `summary` layer tries `fast` first and falls back to `full` (README "Parsers").
- Tests exist (`tests/test_fast.py`, `tests/test_files.py`, `tests/test_model.py`) and use binary `.mgz`/`.aoe2record` fixtures under `tests/recs`. I did not open or run any of them, per the brief.

## 2. Anatomy of a recorded game

### 2.1 Header: a snapshot, not a seed-and-replay

The file starts with `u32 header_length`, an optional chapter address, then a raw-deflate (`wbits=-15`) block (`mgz/__init__.py:38-53`, `mgz/util.py:61-68`). The decompressed header holds, in order:

- Game version string (for example `VER 9.4`) and save version float, which is `-1` plus a `u32` for newer saves (`mgz/util.py:71-107`).
- AI block, then `replay` (`mgz/header/replay.py:11-40`): game time, speed, `random_seed` and `random_seed_2`, record owner, player count, game mode, campaign fields.
- `map_info` (`mgz/header/map_info.py:35-64`): map size, then one terrain type and elevation byte per tile.
- `initial` (`mgz/header/initial.py:94-101`): restore time, and for each player (Gaia included) the full list of starting objects, serialised one by one with `existing_object`. The starting map is written out, not regenerated.
- `achievements`, `scenario` and `lobby` (`mgz/header/lobby.py:12-67`): teams, reveal-map, fog, map size, population limit, game type, and pre-game lobby chat. DE adds a GUID, lobby name and a `map_seed` (`lobby.py:57-66`).

The README FAQ puts it plainly: "The first portion (the header) is a snapshot of the initial game state. The second portion (the body) is a list of moves made by players."

### 2.2 Body: operations

The body begins with a `meta` block: log version, `checksum_interval`, multiplayer flag, record owner, reveal-map flag, `use_sequence_numbers`, and chapter count (`mgz/fast/__init__.py:331-345`, `mgz/body/__init__.py:140-156`). Then comes a loop of operations, each starting with a `u32` op id (`mgz/fast/__init__.py:347-367`, enum in `mgz/fast/enums.py:5-13`):

| Op id | Name | Payload |
|---|---|---|
| 1 | ACTION | `u32 length`, `u8 type`, payload, `u32 sequence` (`fast/__init__.py:251-267`) |
| 2 | SYNC | time increment, optional checksum block (see 2.3) |
| 3 | VIEWLOCK | `float x`, `float y`, `u32 player` (`fast/__init__.py:72-75`) |
| 4 | CHAT | two `u32`s, then `length` bytes of text (`fast/__init__.py:270-274`) |
| 5 | START | in the enum only; not dispatched, so it raises `RuntimeError` (`fast/__init__.py:355-367`) |
| 6 | POSTGAME | trailing data stored reversed and big-endian (`fast/__init__.py:295-304`) |
| 7 | SAVE | any unknown op id falls back here. This is an embedded saved-chapter header, used for campaign save points (`fast/__init__.py:352-354`, `mgz/body/embedded.py:33-40`) |

### 2.3 Sync records and checksums

The sync record is the heartbeat. Its fields are an increment in milliseconds, then a four-byte "marker" that is really the first four bytes of the next record (`fast/__init__.py:14-40`).

- **Increment-only sync**: marker is non-zero (it is the next op id). The record is 8 bytes including the op id. The parser rewinds 4 bytes so the marker is read as the next op.
- **Checksum sync**: marker is zero. A 16-byte block follows: four pad bytes, a `u32` checksum, four pad bytes, then an `is_de` flag. Pre-DE files then skip 8 bytes, so the record is 36 bytes total.
- **DE checksum sync**: the parser rewinds and reads 88 `u32` values (11 per player for 8 slots, `fast/__init__.py:11,42`) and then a `u32` current time. That is about 368 bytes including the op id.

The DE layout is documented only by a code comment with guessed meanings. The checksum is `sum(values)` (`fast/__init__.py:58`), so it is a sum of per-player statistics, not a hash of game state. Two desyncs with equal totals would hide each other.

Two definitions of the sync layout exist. The `construct` schema (`mgz/body/__init__.py:89-101`) reads `sync`, `unknown` and `sequence` fields and a fixed 332-byte block, which does not match the fast parser. I did not verify which one is right for every build. The `fast` version is the one the summary uses.

The `checksum_interval` field from `meta` is read but never used by this code, so the unit and cadence of checksums are unverified.

### 2.4 Action records

Every action is a `u32` length (which counts the type byte and payload), a `u8` action type, the payload, and a trailing `u32` sequence number (`fast/__init__.py:251-267`). Newer DE builds (version 71094 and later) prefix the payload with a player byte and a `u16` length. The parser detects this by checking `len(data) == length + 3` (`fast/__init__.py:78-82`, `fast/actions.py:9-15`).

The main action types (enum in `mgz/fast/enums.py:16-85`; payload layouts in `mgz/fast/__init__.py:96-248` and `mgz/fast/actions.py:9-138`):

| Action | Payload |
|---|---|
| MOVE 3 | player, selected unit IDs, target x and y, optional 8 flag bytes |
| ORDER 0 | target object ID, x, y, selected IDs (attack, gather, repair-style orders) |
| STOP 1, GUARD 19, FOLLOW 20 | selected IDs, plus target ID for guard and follow |
| STANCE 18, FORMATION 23 | stance or formation ID, selected IDs |
| BUILD 102 | building type, x, y, selected builders |
| RESEARCH 101, MAKE 100 | building ID and technology or unit ID |
| QUEUE 119, MULTIQUEUE 112, DE_QUEUE 129 | unit type, amount, building IDs |
| GATHER_POINT 120 | target ID and type, x, y, selected IDs |
| ATTACK_GROUND 107, PATROL 21, DE_ATTACK_MOVE 33 | x, y (plus waypoints for patrol and attack-move), selected IDs |
| SPECIAL 117 | order ID, slot ID, target, x, y, selected IDs |
| UNGARRISON 111, REPAIR 110, DELETE 106 | object IDs, target |
| BUY 123, SELL 122 | resource ID, amount |
| TRIBUTE 108, DE_TRIBUTE 196 | player pair, resources, fee |
| RESIGN 11 | player byte (and a disconnect flag in the body schema) |
| GAME 103 | command ID: diplomacy, speed, cheat, farm and fish-trap queues, default stance (`mgz/body/actions.py:229-303`) |
| WALL 105, FLARE 115, TOWN_BELL 127, BACK_TO_WORK 128 | wall line, flare target list, bell state, town-centre ID |
| CREATE 4 | unit type, player, x, y (a cheat-type command, treated as such by the summary) |
| AI_ORDER 10, AI_COMMAND 53 | AI-issued commands |
| POSTGAME 255 | raw trailing block |

Two points matter for design. First, the selection is a list of object IDs, not resulting positions. Second, a move or attack is a request about IDs and a target point. Turning it into state changes needs the simulation's pathfinding and combat code. The enum also contains many `DE_UNKNOWN_*` entries whose meaning is unverified.

Chat is a length-prefixed string. The model layer drops consecutive duplicate viewlocks (`mgz/model/__init__.py:258-259`).

### 2.5 Sizes, computed from the struct layouts (not measured)

- Classic one-unit MOVE: op id 4, length 4, type 1, payload 19 (player 1, pad 2, pad 4, selected 4, x 4, y 4), flags 8, one ID 4, sequence 4. That gives about 44 bytes, or 36 without the flags.
- Classic sync with no checksum: 8 bytes. With a checksum: 36 bytes.
- DE checksum sync: about 368 bytes, dominated by the 352-byte stat block.

## 3. How the summary is built, and what it cannot know

The summary is a single pass over the body (`mgz/summary/full.py:89-146`). It keeps running duration, action counts, resignations and cheat flags, then derives the following.

- **Duration**: the sum of all sync increments (`full.py:104-106`). The game clock lives entirely in the stream.
- **Match identity**: SHA-1 of the first four checksum values (`full.py:107-108,154-155`). DE and HD use the GUID instead.
- **Winner**: teams with no resigned member win (`mgz/model/__init__.py:302-307`, `mgz/summary/players.py:108-167`). If a postgame block exists, its victory field overrides this (`players.py:159-167`).
- **Completion**: the postgame `complete` flag, otherwise whether anyone resigned (`full.py:352-361`).
- **eAPM**: count of actions per player, excluding AI orders, divided by minutes (`full.py:109-111,246`; `model/__init__.py:309-311`). This is every command, repeats included, so it is "commands per minute", not APM as players mean it.
- **Age-ups**: taken from in-game chat lines such as "advanced to the ... Age" (`mgz/common/chat.py:9-172`). This is text matching against the game's own messages, which is localisation-dependent and may be missing.
- **Resources over time**: only on DE, from sync slot 1 ("total resources") and object-count slots (`fast/__init__.py:43-69`; `model/__init__.py:246-256`). The code says these values "are just guesses".
- **Cheat detection**: heuristics on CREATE, TRIBUTE-to-zero, BUILD with unknown building IDs, and specific GAME command IDs (`full.py:117-126`).
- **Build orders**: there is no build-order module. They could be derived from BUILD, QUEUE, MAKE and RESEARCH records with timestamps. That is unverified against the game's own build-order view.

**What it cannot know without re-simulating**: unit positions, HP, carried resources, stockpiles, kills, fog-of-war, pathing results, and whether an order succeeded. The README's answer is "You can't, without replaying the match in-game."

**The core limitation** is that a command stream is input, not output. Replaying it correctly needs the same simulation binary, the same rules data, the same map generator and the same RNG. The code shows the cost of this. `save_version` gates behaviour in at least a dozen places (for example 61.5 in `replay.py:38`, 62.0 in `map_info.py:16`, 66.3 in `lobby.py:18,64`). `fast/__init__.py:81` branches on a payload-length check to tell one DE build family from another. Any patch can change a byte offset.

## 4. The fast parser's design

- **Sequential, hand-coded**: each action type has its own `struct.unpack_from` call with hard-coded offsets (`fast/__init__.py:96-248`). No object graph is built and most payloads are not validated.
- **Errors are local**: a `struct.error` in one action returns `Action.ERROR` and the loop carries on (`fast/__init__.py:263-265`). A short read at end of file becomes `EOFError`, which ends the loop (`fast/__init__.py:365-366`).
- **Headers use regex scans**: `fast/header.py` finds object blocks with precompiled byte regexes (`header.py:21-29`) and skips known junk objects (`header.py:17-19`). It only supports UP 1.5, DE and HD (`header.py:740-745`).
- **"Fast" is not "indexed"**: there is no offset index. Reaching tick T means scanning from the start. The summary reads the whole body once to hash it (`full.py:82`) and then scans again for actions. Reading the body twice is cheap at these sizes, but the design has no random access.

## 5. Design lessons for RTS_2027's replay format

**Header (proposed, roughly 1 to 4 KB plus map)**

| Field | Size | Why |
|---|---|---|
| Magic and format version (major, minor) | 8 B | Refuse or convert on mismatch |
| Sim build hash (git commit or hash of sim sources) | 32 B | Know which binary can replay this file |
| Rules and data pack hash | 32 B | Balance changes break replays silently otherwise |
| RNG algorithm ID and seed | 9 B | Deterministic replay |
| Map ID and content hash | 36 B | Detect a changed map; embed the map if needed |
| Ticks per second, checksum interval (ticks) | 6 B | Self-describing timing |
| Player table: slot, team, faction, colour, AI level, name | about 48 B each | Analysis without the sim |
| Game settings as TLV (speed, fog, start resources, victory) | variable | New settings add blocks, old readers skip them |
| Optional keyframe 0 (initial state) | about 10 to 60 KB raw, compressed | Start a headless run from the file alone |

**Body (proposed)**: records of `[varint tick delta][u8 type][u8 slot][u16 payload length][payload]`. A one-unit move could be about 19 bytes with fixed-point coordinates and 4-byte entity IDs. That compares with 36 to 44 bytes in the AoE layout, computed from the struct sizes above. These are estimates.

**Checksums**: one record per K ticks, for example `[varint delta][u8 type][u64 state hash]`, about 12 bytes each. At one per second of sim time this costs about 43 KB per hour. Use a real hash (for example xxHash64 or BLAKE3 truncated) over a canonical, sorted serialisation of the state. Optionally add three or four per-subsystem hashes (economy, positions, combat, production) so a desync can be localised. AoE's sum-of-stats checksum cannot do this.

**Keyframes**: optional snapshots every 60 seconds of sim time, stored as a separate chunk type. They let a viewer scrub without re-simulating, and they let a tool check its own sim. Estimated cost is tens of kilobytes per keyframe, so this is an option, not a default.

**Footer index**: an entry every 10 seconds of `(tick, byte offset)`, about 8 bytes each. That is roughly 3 KB per hour and gives the random access AoE lacks.

**Framing**: body in chunks, each with a length and a CRC32. A truncated file then stays readable up to the last good chunk. AoE's format has length fields but no integrity check. Its only consistency signal is the checksum, and that is a sum.

**Analysis without the sim**: APM, build timings from production and research commands, and unit-production counts can all come straight from the command stream. Positions and resource totals need a headless simulation run or keyframes. Keep the replay minimal (inputs, checksums, optional keyframes, header) and write derived statistics to a JSON sidecar. Do this with a headless tool that runs the sim and compares checksums. That tool doubles as the regression test for determinism.

**Versioning**: TLV blocks with lengths, so readers skip what they do not know. Major version bumps are for layout changes. Patch-level compatibility is only promised within one build hash.

**Schema as the single source**: write the record layouts once, in a declarative form, and generate or check the reader from it. The `construct` approach (section 6) suits a Python analysis tool. The game's C++ writer and reader should share one table.

## Implications for RTS_2027

- **Store a state hash, not a stats sum.** AoE's per-player stat sum hides desyncs that cancel out. **Recommendation:** a 64-bit hash of a canonical serialisation, with per-subsystem hashes as an optional extension.
- **Put the build hash in the header and refuse mismatches.** Command-only replays are only valid on the exact sim. **Recommendation:** refuse playback on a build-hash mismatch and offer a converter only where a sim change is known to be inert.
- **Keep the body as inputs only.** Positions and resources are derived, not recorded. This keeps the file small and stops the format from freezing derived data. Trade-off: every stats feature needs a headless run.
- **Add a footer index from day one.** The fast parser's lack of an index is a design cost AoE has lived with. Cheap to add now, hard to retrofit.
- **Use small, fixed-width command records with varint ticks.** Unit IDs as 4-byte IDs, coordinates as fixed-point. **Recommendation:** payloads under 32 bytes for the common commands (move, attack, build, queue).
- **Keyframes are a cache, not the source of truth.** They trade file size for scrubbing and faster analysis. **Recommendation:** make them optional and off by default; turn them on for campaign save points and for bug reports.
- **Build the analysis tool as a separate program.** AoE's summary code mixes parsing, heuristics and guesses in one place. Ours should read the replay, run the sim headless, and emit stats. The same tool checks determinism.
- **Plan for campaign save points inside the stream.** AoE embeds saved-chapter headers in the body. Our campaign will need a comparable marker so a mission can resume from a checkpoint, and a replay can be cut there.
- **Frame the body in chunks with CRC32.** It costs a few bytes per chunk and makes crashed or partial recordings recoverable.
- **Plan for an explicit "unknown block" type.** AoE's unknown op ids fall back to "save" and its unknown actions are silently dropped. A typed unknown-record path would make format drift visible.

## Open questions for the owner

1. **What is a replay for?** Options: (a) statistics and sharing only, which needs no sim-exact playback; (b) watching the match back, which needs exact playback and a rules-version lock; (c) both, with (b) gated by build hash. Option (c) is the most work, but (a) alone is much simpler.
2. **Are old replays expected to play in newer builds?** Options: never (each build is closed to its own replays); within a patch window with a converter; or always, which means a long-term compatibility commitment on the sim.
3. **How often should checksums run, and are they always on?** Options: always at a fixed interval (about 1 per second costs about 43 KB per hour); only in multiplayer; or only in debug builds. Cadence trades file size against how quickly a desync is caught.
4. **Should keyframes exist?** Options: none; every 60 seconds; or only at campaign checkpoints. This sets how fast the scrubber and the stats tool can be without a sim run.
5. **Does LAN multiplayer use the replay log as its desync log?** If yes, the checksum records must be on in multiplayer, and their format becomes a protocol concern too.

## Sources

Repo: `/home/claude/ref/aoc-mgz` (MIT; read-only study). Files read and line ranges used:

- `README.md` (whole): supported versions, parser design, support matrix, FAQ.
- `LICENSE` lines 1-3; `setup.py` lines 6, 13-16.
- `mgz/__init__.py` lines 1-60: top-level header and subheader structs.
- `mgz/util.py` lines 24-107, 110-164, 189-245, 296-328: version enum, save-version helpers, adapters, `Find`, `RepeatUpTo`, `GotoObjectsEnd`, `find_postgame`, `unpack`.
- `mgz/header/replay.py` lines 1-40; `mgz/header/initial.py` lines 1-101; `mgz/header/lobby.py` lines 1-67; `mgz/header/map_info.py` lines 1-64.
- `mgz/body/__init__.py` lines 1-172: `action_data`, `sync`, `viewlock`, `chat`, `start`, `meta`, `operation` schemas.
- `mgz/body/actions.py` lines 1-555: action struct definitions, notably `game` (229-303), `wall` (310-335), `order` (410-423), `gatherpoint` (425-433), `postgame` (515-550).
- `mgz/body/embedded.py` lines 1-70: saved chapters and embedded chat.
- `mgz/fast/__init__.py` lines 1-367: `sync` (14-69), `viewlock` (72-75), `parse_action` (78-248), `action` (251-267), `chat` (270-274), `meta` (331-345), `operation` (347-367), `postgame` (295-328).
- `mgz/fast/actions.py` lines 1-138: DE build 71094 payload decoders.
- `mgz/fast/enums.py` lines 1-98: `Operation`, `Action`, `Postgame`, `Age`.
- `mgz/fast/header.py` lines 1-60, 659-770: `decompress`, `parse_version`, `parse`, and the regex object scan.
- `mgz/summary/full.py` lines 1-375: the single-pass body loop (89-168), hash (150-155), completion (352-361), player and eAPM (235-250).
- `mgz/summary/players.py` lines 108-190: `guess_winner`, `get_players_data`.
- `mgz/model/__init__.py` lines 220-311: timeseries, uptimes, viewlock dedup, winner and eAPM.
- `mgz/model/definitions.py` lines 34-40, 157-163: `TimeseriesRow`, `Uptime`.
- `mgz/common/chat.py` lines 1-190: age markers, `parse_chat`.
- Not read: `tests/` (binary fixtures excluded per brief; test `.py` names listed only), `mgz/body/achievements.py`, `mgz/header/de.py`, `mgz/header/hd.py`, `mgz/model/compat.py`.
