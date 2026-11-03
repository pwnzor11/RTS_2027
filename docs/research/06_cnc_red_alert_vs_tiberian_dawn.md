# What Red Alert Changed Over Tiberian Dawn

**Summary**

- The headline change is data-driven balance. TD hard-codes every unit, weapon and warhead stat in compiled C++ tables (`CONST.CPP`, `UDATA.CPP`, ...). RA adds `RULES.INI`, parsed at startup by `RulesClass` (`RULES.CPP`, 1,073 lines), and every type class gets a `Read_INI()` that overrides its compiled defaults. RA still keeps compiled defaults as a fallback; it is a hybrid, not a pure data engine.
- RA introduces a real fixed-point class (`fixed`, 16.16, unsigned) for rule values. TD used raw ints plus 8-bit "cardinal/fixed" asm helpers. The simulation core is still integer-and-lookup-table based; `fixed` is mostly a rule/config type.
- Most of the "new" 220 files in RA are not gameplay. They are library code (compression, crypto, streams/pipes, Westwood Online, Winsock/UDP, bitmap/DIB, hardware detection). The real gameplay additions are about 15 files: `VESSEL`, `CRATE`, `VORTEX`, `WARHEAD`, `WEAPON`, `TEVENT`, `TACTION`, `TRIGTYPE`, `SESSION.CPP`, `CCINI`, `FIXED`, `CCPTR`.
- Some "new" features existed in TD already. Superweapons (`SUPER.CPP`, 383 lines in TD), Tiberium growth/spread, `LAND_WATER`, and the separate non-sync random stream (`Sim_Random`) are all in TD. RA extended them rather than inventing them.
- Triggers were rewritten from one event plus one action to two events and two actions with AND/OR/linked combinators, and split into separate event/action classes. Details belong to another helper's file.
- The 2020 Remaster did not rewrite the sim. It wrapped it in a DLL (`DLLInterface.cpp`, 7.8k and 8.8k lines) exposing a C API (`CNC_Advance_Instance`, `CNC_Handle_Input`, ...). A separate (GlyphX) client renders it. This is the strongest evidence that the original sim/render separation was salvageable but never designed.

## 1. Inventory of new, removed and grown files

`diff <(ls TIBERIANDAWN) <(ls REDALERT)`: 315 vs 535 files. RA has about 220 files TD lacks; TD has 14 that RA lacks (`ALLOC.CPP`, `KEYFRAME.CPP`, `PAGFAULT.ASM`, `PHONE.H`, `REAL.H`, `TARCOM.*`, `TEXTBLIT.H`, `WINASM.ASM`, the two `.RC` icon files, the vcxproj files). RA lost nothing gameplay-relevant.

New RA files, grouped by what they actually are:

| Group | Files | Meaning |
|---|---|---|
| Gameplay | `VESSEL.*` (2,450 lines), `CRATE.*`, `VORTEX.*` (1,257), `WARHEAD.*`, `WEAPON.*`, `TEVENT/TACTION/TRIGTYPE.*`, `EGOS`, `FACE`, `CARRY` | Real new systems. Warhead and weapon became first-class classes. |
| Config / data | `CCINI.*` (1,484), `INI.H`, `INIBIN.CPP`, `INICODE.CPP`, `FIXED.*`, `CCPTR.*`, `HSV`, `RGB`, `PALETTEC` | INI parsing with game-typed getters (`Get_Fixed`, `Get_Lepton`, `Get_WarheadType`, `Get_Buildings`, ...). |
| Streams / compression / crypto | `STRAW/PIPE` family (`LCWPIPE`, `LZOPIPE`, `BLOWPIPE`, `SHAPIPE`, `B64PIPE`, ...), `LCW`, `LZO*`, `LZW`, `BLOWFISH`, `SHA`, `PK`, `CRC`, `BASE64` | A filter-chain I/O abstraction. Used for save games, mix files, and network. Roughly 60 files. |
| Networking | `WOL*`, `WSPROTO`, `WSPUDP`, `WSPIPX`, `UDPADDR`, `RAWOLAPI`, `SENDFILE`, `SESSION.CPP` | Westwood Online and Winsock. |
| UI widgets | `BAR`, `BIGCHECK`, `STATBTN`, `TOOLTIP`, `SEDITDLG`, `ICONLIST` | `BIGCHECK` is only a big checkbox gadget (draws `bigcheck.shp`), not a sync check. |
| Platform | `SURFACE`, `BUFF`, `MCI*`, `DIB*`, `CPUID.ASM`, `COMINIT`, `TENMGR`, `CCTEN` | Win95 plumbing. |

Same-named files that grew most (lines TD to RA):

| File | TD | RA | Why |
|---|---|---|---|
| `SCENARIO.CPP` | 819 | 3,804 | Mission INI loading, Counterstrike/Aftermath hooks, more sections (teams, base, triggers). |
| `NETDLG.CPP` | 5,433 | 7,843 | More lobby options, WOL, internet. |
| `TECHNO.CPP` | 4,825 | 6,971 | Naval, spies, cloaking, more AI behaviour. |
| `TEAM.CPP` / `TEAMTYPE.CPP` | 1,515 / 977 | 3,074 / 1,879 | Team scripting roughly doubled in size. |
| `RULES.CPP` / `RULES.H` | 126 / 275 | 1,073 / 895 | The rules system. |
| `LIST.H`, `FTIMER.H` | 146, 88 | 799, 716 | Templated timers (`CDTimerClass<FrameTimerClass>`) and list helpers. |
| `CONNECT.CPP` | 248 | 835 | Connection abstraction. |
| `QUEUE.CPP` | 4,213 | 4,560 | Only +8%. The lockstep core was already stable in TD. |

Takeaway: the sim core (`TECHNO`, `UNIT`, `INFANTRY`, `SCENARIO`, `TEAM`) grew 20-70% by accretion. Nothing was restructured. RA is TD plus features, with the same object-per-class inheritance tree.

## 2. Data-driven balance (RULES.INI) and what it taught

**TD**: weapons are a `const` array in `CONST.CPP:73`, one row per weapon: `{BULLET_SNIPER, 125 damage, 40 ROF, 0x0580 range, VOC_SNIPER, ANIM_NONE}`. Unit stats are in constructors in `UDATA.CPP`, `IDATA.CPP`, `BDATA.CPP`. `RulesClass` in TD (`RULES.CPP`, 126 lines) holds only AI-build ratios and limits (`HelipadRatio(0x1E)`, `DefenseLimit(40)`, `IQ*`). It has no INI loader. Its header comments are dated 2019, so it looks like a Remaster-era backport; unverified whether TD originally had anything here.

**RA**: `INIT.CPP:310` loads `RULES.INI` into the global `RuleINI` and calls `Rule.Process()` (`RULES.CPP:344`), which runs 11 section handlers:

```cpp
General(ini); MPlayer(ini); Recharge(ini); Heap_Maximums(ini); AI(ini);
Powerups(ini); Land_Types(ini); Themes(ini); IQ(ini); Objects(ini); Difficulty(ini);
```

`Objects()` (`RULES.CPP:974`) walks every heap and calls `Read_INI` on every Warhead, Bullet, Weapon, Unit, Infantry, Vessel, Aircraft and Building type. So each type has an INI section named after it.

What is exposed:
- **Weapons** (`WEAPON.CPP:201-231`): `Damage`, `ROF`, `Range`, `Burst`, `Speed`, `Report` (sound), `Anim`, `Warhead`, `Projectile`, `Charges`, `TurboBoost`.
- **Warheads** (`WARHEAD.CPP:176`): `Verses=` is a comma list of damage percentages per armor class. This is the damage-vs-armor matrix, now data. `SpreadFactor` controls falloff.
- **Techno types** (`TECHNO.CPP:6733`): `Strength`, `Armor`, `Cost`, `TechLevel`, `Prerequisite`, `Speed`, `Sight`, `Owner`, `Primary`/`Secondary` weapon, `Cloakable`, `Crushable`, `SelfHealing`, `Passengers`, `ROT`, `Image` (which sprite to use). The `Image=` key means a mod can clone a unit under a new name.
- **Terrain movement costs** (`RULES.CPP:840-865`): for each land type, a `fixed` cost per locomotion class (`Foot/Track/Wheel/Float`) plus `Buildable`. Terrain and locomotion are a matrix in data, not `if` chains.
- **AI** (`AI` section, ~100 lines): base-building ratios, attack intervals, power thresholds, and the IQ ladder (what the AI is allowed to do at each IQ level: superweapons, production, guard, repair/sell, crush, scatter, harvester).
- **Superweapon recharge times** (`Recharge`), **crate** counts and radius, **ShroudRate**, **GapShroudRadius**, and Aftermath-only keys (`ChronoTankDuration`, `QuakeUnitDamage`).

Two things were learned the hard way:
1. **Multiplayer must hash the rules.** `NETDLG.CPP:3108` puts `RuleINI.Get_Unique_ID()` (a message digest of the INI) in the player-info packet as `CheatCheck`; `NETDLG.CPP:5317` rejects joiners whose digest differs (`REJECT_MISMATCH`). Data-driven balance without a sync hash is a desync factory.
2. **Compiled defaults plus INI overrides is brittle.** `SCENARIO.CPP:2240-2255` has an `#ifdef` block that patches types in C++ after load for some modes (`UNIT_ANT1.Level = -1`, `INFANTRY_SPY.PrimaryWeapon = NULL`). `Rule.Difficulty()` is a stub (`#if 0`, `RULES.CPP:1045`); the per-difficulty `DifficultyClass` biases (firepower, speed, armor, ROF, cost, build speed) exist but the loader is disabled. Expansion content (Counterstrike, Aftermath) was layered on as a second INI (`AFTRMATH.INI`) processed by the same `Rule.*` calls (`SCENARIO.CPP:2269+`).

Per-scenario re-read: `SCENARIO.CPP:2258` re-runs `Rule.General/AI/IQ/...` on `RuleINI` for each mission, so a mission file can layer overrides on rules.

## 3. Math and determinism changes

**Fractions in TD**: no fixed-point class. Percentages and ratios are `unsigned short` where 256 = 1.0 (`PowerEmergencyFraction(0x00C0)` = 75%). Scaling goes through two Watcom inline-asm routines in `JSHELL.H:221-240`:

```
Fixed_To_Cardinal(base, fixed):  (base * fixed + 0x80) >> 8, saturating
Cardinal_To_Fixed(base, cardinal): (cardinal << 8) / base
```

So an 8-bit fraction, rounded, with saturation. This costs precision: one step is 1/256 (about 0.4%), and nothing in the type system says which ints are fractions.

**RA's `fixed`** (`FIXED.H`): a class holding `{unsigned short Whole; unsigned short Fraction}`, i.e. unsigned 16.16. Constructors from int, float, "numerator, denominator", and ASCII ("75%" or "0.75"). Arithmetic on two `fixed` yields `fixed`; mixing with int yields int and rounds to nearest. Limits stated in the header comment: no negatives, no overflow detection, whole part <= 65535. It exists because the INI parser needs a typed value (`Get_Fixed`). Positions and distances in the sim still use `LEPTON` (1/256 of a cell, ints) and 8-bit facings. `fixed` is not the sim's math type.

**Determinism**: both games run lockstep with integer sim and a seeded LCG (`RANDOM.CPP:96`: `Seed = Seed*MULT + ADD`, top bits returned, 15 significant bits). Both already have a second, non-sync generator for cosmetic effects: TD `Sim_Random_Pick`, RA `NonCriticalRandomNumber` (`INLINE.H:1002`, `GLOBALS.CPP:284`). RA wraps it in a `RandomClass`; TD used free functions. The requirement to keep the two streams separate was learned in TD; RA only tidied it. Both compute a per-frame CRC of game state (`Compute_Game_CRC`, `QUEUE.CPP:683` in TD and `QUEUE.CPP:640` in RA) into a 32-entry ring `CRC[Frame & 0x1f]` for desync detection.

**CCPtr** (`CCPTR.H`): not a smart pointer in the ownership sense. It stores an int index into a static heap and converts to `T*` on access (`return Heap[ID]`), with `-1` for null. Purpose, from the header: "requires no fixups for saving and loading". It replaces raw type pointers inside objects (`CCPtr<VesselTypeClass> Class`). It is a handle pattern, so save/load and network snapshots don't need pointer rewriting. TD does that fixup with `Code_Pointers/Decode_Pointers` on every class (RA still has both, so CCPtr is a partial migration).

## 4. New gameplay systems and how they were bolted on

| System | Where | Clean or not |
|---|---|---|
| **Naval** | `VESSEL.CPP`/`.H`, `VESSEL` derives from `DriveClass`; `SPEED_FLOAT` locomotion; `LAND_WATER` and `LAND_RIVER`; `VesselTypeClass` | Mostly clean. Water was not new: TD already had `LAND_WATER`, `SPEED_FLOAT` and `SPEED_HOVER`. RA added a type class, ship-specific repair (shipyards/sub pens, `VESSEL.CPP:1069`) and sonar/cloak for subs. But the movement/terrain checks are scattered `land == LAND_ROCK \|\| LAND_WATER \|\| LAND_RIVER` tests in `FOOT.CPP:596,994` and `DRIVE.CPP:1375`, not a lookup on the new cost matrix. |
| **Ore / gems** | `OVERLAY_GOLD1-4`, `OVERLAY_GEMS1-4`; code still says Tiberium (`Grow_Tiberium`, `LAND_TIBERIUM`, `TiberiumGrowth[]`) | A reskin of TD's system with the original names kept. Growth/spread uses reservoir sampling in both. TD (`MAP.CPP:917-921`) just overwrites a random slot once the candidate array is full, which biases toward late cells. RA (`MAP.CPP:1055-1062`) adds `TiberiumGrowthExcess` so each candidate has fair odds. A small correctness fix. Gems are another overlay type with higher value, not a new mechanism. |
| **Superweapons** | `SUPER.CPP/H`; `SuperClass SuperWeapon[SPC_COUNT]` in `HOUSE.H:321` | Already in TD (`SPC_ION_CANNON`, `SPC_NUCLEAR_BOMB`, `SPC_AIR_STRIKE`). RA grew the enum to 8: Sonar Pulse, Nuke, Chronosphere, Para-Bomb, Para-Infantry, Spy Plane, Iron Curtain, GPS. Each is one `SuperClass`: a `CDTimerClass` countdown, flags (powered, present, one-time, ready), recharge time from `Rule.Recharge`, and voice lines. Chronosphere needs two targeting steps and uses a second pseudo-ID `SPC_CHRONO2` (`DEFINES.H:820`), a hack. Effects themselves are `switch`ed in `HouseClass`/`Map` code, not data. |
| **GPS / Gap / Shroud** | `GPS` as `IsGPSActive` flag on house; `Jam_From/UnJam_From` and per-cell `Jammed` (`MAP.H:71`, `CELL.H:121`); `Rule.GapShroudRadius`, `Rule.ShroudRate` | New. TD has no shroud regrowth or jamming. RA regrows shroud on a timer (`LOGIC.CPP:280`, `Special.IsShadowGrow`, every `ShroudRate` minutes) and gap generators jam by per-house cell counters. |
| **Crates** | `CRATE.CPP` (182 lines), `CrateClass Crates[256]` in `Map`; `CRATE_*` enum of 18 types incl. `ICBM`, `TIMEQUAKE`, `VORTEX`, `INVULN` | Cleaner than TD, where crates are overlay types (`OVERLAY_WOOD_CRATE`). RA crates are a separate struct: cell, timer, create/remove. Spawn counts, radius, regen are rules. Crate effect values live in the `Powerups` section. |
| **Vortex** | `VORTEX.CPP` (1,257 lines) | A single global effect object (a roaming storm that picks units up and damages them) with its own render path, rather than a unit or animation. Heavy and special-cased; the `Render` and `Set_Redraw` methods show that it fought the cell-redraw model. |
| **Expansions** | `FIXIT_CSII` (215 uses), `FIXIT_VERSION_*` (130), `FIXIT_ANTS`, `FIXIT_CARRIER` | Counterstrike/Aftermath were done as `#ifdef` patches in core files. This is the clearest example of un-modular bolt-on. |
| **Triggers** | `TRIGTYPE/TEVENT/TACTION` (+ ~3,800 lines) vs TD `TRIGGER` (1,483) | Event and action split into classes. Two of each per trigger with `MULTI_ONLY/AND/OR/LINKED`; persistence levels (volatile, semi, persistent). About 33 event types (`TEVENT.H:44-78`) and a larger action set (`TACTION.H`) vs TD's smaller event list (`TRIGGER.H:39-70`); exact counts unverified. Still compiled enums with INI-supplied parameters, so scripting is data but the vocabulary is code. |

## 5. Multiplayer and session changes

- TD declares `SessionClass` in `SESSION.H` with no `.CPP` (members are inline or elsewhere). RA gets `SESSION.CPP` (1,875 lines) as the one place for game options (credits, unit count, bases, ore, crates, AI players, tech level, shroud), player slots, and per-protocol buffers.
- Game-type enum: TD has `GAME_NORMAL/MODEM/NULL_MODEM/IPX/INTERNET` and Remaster adds `GAME_GLYPHX_MULTIPLAYER` (`DEFINES.H:2509`). RA adds Winsock/UDP (`WSPUDP`, 428 lines), Westwood Online chat/lobby/download (`WOL_*`, `WOLAPIOB`), and keeps IPX (`IPX95`, `IPXMGR`).
- Lockstep core (`QUEUE.CPP`) barely changed (+8%). The frame-ahead command queue (`EVENT.H`, events scheduled N frames ahead), CRC ring check and resend logic are the same idea in both. The growth is in connection plumbing, not the sync model. The lesson is that TD's lockstep design held up; what broke was transports (IPX died, modem died).
- Mismatch detection added: rules digest (section 2). RA also checks versions (`VERSION.CPP`) and has `REJECT_MISMATCH` and file-send (`SENDFILE.CPP`) to push a missing map to joiners.
- Skirmish lobby: `NETDLG.CPP` grew +2,410 lines. A skirmish (`GAME_NORMAL` with AI houses chosen in the lobby) reuses the multiplayer setup path rather than being a separate mode.
- Net packet size is still bounded by IPX (546 bytes, `SESSION.H:64`). Even in 1996 the network format was shaped by a dead transport.

## 6. Remaster-era changes (2019-2020)

Code is tagged with initials and dates: roughly 296 date-tagged comments in TD and 238 in RA (`ST - 3/12/2019`, `SKY`, `Jason Scott 2019/08/14`). There is no `REMASTER_BUILD` define in this repo (0 hits); the DLL is simply the only build.

What the Remaster team changed:
- **Sim as a DLL with a C API** (`DLLInterface.cpp/h`). Exports: `CNC_Init`, `CNC_Advance_Instance(player_id)`, `CNC_Get_Game_State`, `CNC_Handle_Input`, `CNC_Handle_Sidebar_Request`, `CNC_Handle_SuperWeapon_Request`, `CNC_Save_Load`, `CNC_Config` (difficulty bias table), `CNC_Add_Mod_Path`. The renderer receives object lists (`CNCObjectListStruct`) rather than a framebuffer (though `CNC_Get_Visible_Page` still exists for the classic view). This is the sim/presentation split the original never had.
- **Sidebar rewritten** (`SIDEBARGlyphx.CPP/H`): per-player sidebar state for the new client; `MultiplayerSidebars[MAX_PLAYERS]`.
- **Player identity**: `uint64 player_id` via `GlyphxPlayerIDs[]`, replacing "the" local player. The original code assumes one local `PlayerPtr` global; the DLL had to make every UI-ish call take a player id.
- **Movie/UI hooks** replaced by callbacks (`Play_Movie_GlyphX`, `GlyphX_Debug_Print`).
- **Rule fixes**: `Is_MCV_Deploy()` (`RULES.CPP`, 2019) adds a `Special` override over `Rule.IsMCVDeploy`; difficulty biases come from the host via `CNC_Config`, not `Rule.Difficulty()`.
- **Modding**: `CNC_Add_Mod_Path` lets Steam Workshop mods replace data files. The map editor is a separate C# app (`CnCTDRAMapEditor`).
- **Build**: 32-bit Win32, MSVC 2017, Windows 8.1 SDK, MFC (README). The Watcom `#pragma aux` asm helpers still appear in `JSHELL.H`; whether the Remaster build still uses them is unverified.

What this says: after 25 years, the sim core runs unmodified once you add a headless loop, per-player input routing, and a state-export call. That part of the 1995 design (integer sim, command-queue input, deterministic frame advance) aged well. The global singletons and local-player assumptions needed the most surgery.

## 7. Lessons: what to get right from the start

1. **Define data schemas first.** RA retrofitted INI loading onto compiled tables and still left patch-ups in code. In a new engine, types, weapons, warheads, armor matrices and terrain costs should be data from day one. Compiled defaults duplicated by data is the worst of both.
2. **Hash the rules in multiplayer.** RA's INI digest in the join packet is cheap and necessary.
3. **Typed fractions from the start.** TD's untyped 8-bit fractions and RA's `fixed` retrofit show the need. But RA's `fixed` is unsigned only and not used in the sim; a new engine needs signed fixed-point or integer-only rules chosen once.
4. **Keep sim and rendering separable.** The Remaster needed 16k lines of glue to get there.
5. **Don't gate content with `#ifdef`.** `FIXIT_CSII/ANTS/CARRIER` shows expansions bolted on. Make "content packs" overlays on a base ruleset.
6. **Per-player state, not globals**, so skirmish, observers, and LAN all work (the Remaster had to retrofit player ids).
7. **Index handles instead of raw pointers** (`CCPtr` idea) make saves and snapshots trivial. RA half-migrated and kept both systems.
8. **Superweapons as data.** `SuperClass` is already generic (timer + flags); effects are not. A power spec (targeting mode, effect list, recharge) would remove the Chrono2 hack.
9. **Terrain/locomotion matrix everywhere.** RA built the cost matrix but left hard-coded land checks in movement code.
10. **Separate non-sync RNG and CRC the game state each frame.** Both done in TD; keep them.

## Implications for RTS_2027

- **Externalise all balance data from day one.** Recommendation: one data format (TOML/JSON/INI) for units, weapons, warheads, armor tables, terrain costs and AI parameters, with no compiled fallback. Trade-off: a little upfront schema design and a loader, but it makes the tech tree and campaign gating (the owner's priority 1) far easier to tune.
- **Hash the loaded rules** and compare it at LAN join even if LAN is optional; it costs about a dozen lines.
- **Use integer or fixed-point sim math with a signed fixed type**, not RA's unsigned 16.16. Recommendation: use ints in 1/256-cell-style units for positions and a 32-bit signed fixed (or integer permille) for ratios; floats only for rendering. This keeps LAN lockstep possible later.
- **Warhead x armor matrix as data** (`Verses=` style). It is the cheapest and best-understood balance lever in the classic RTS.
- **Design a superweapon/special-power spec** (targeting shape, effect list, recharge, power requirement) rather than `switch` on an enum.
- **Naval is optional.** RA's naval cost is high (VESSEL 2,450 lines plus scattered land checks). With two factions and 2D sprites, water as a locomotion class can be added later if the terrain/locomotion cost matrix is in data now.
- **Crates and vortex-style novelty are cheap or expensive depending on architecture.** A crate is a timer plus a cell (182 lines); a vortex is a special renderer (1,257). Skip anything that needs a special render path.
- **Split the engine into sim and presentation** from the beginning: the sim takes commands per tick and exposes state; the client renders. This also gives you a replay/AI harness for free.
- **Triggers are the campaign engine.** Recommendation: copy RA's shape (up to two events combined by AND/OR, up to two actions, persistence levels) but make the vocabulary extensible from data/scripts; the owner needs the campaign story to be authorable.
- **Expansion/campaign gating via content overlays**, not compile-time flags.

## Open questions for the owner

1. **Mod-ability scope:** Do you want players to edit balance only (INI-style files), or add new units and art (full content packs)? Balance-only is a weekend of work; content packs shape the whole asset pipeline.
2. **Naval:** In or out for v1? Options: none (land/air only); water as an impassable terrain only; full naval later via a locomotion class.
3. **Difficulty levels:** RA's biases (firepower, speed, armor, build speed, cost) were never wired to the loader. Do you want difficulty to be stat handicaps (cheap, "cheating" feel), AI-behaviour tiers (IQ-ladder style, more work), or both?
4. **Superweapons:** Is a small fixed set (e.g. 2-3 per faction) enough, or do you want them in the tech tree for the campaign to unlock?
5. **LAN determinism now or later:** Commit to a deterministic lockstep sim from the start (constrains every design choice) or build a looser sim and accept that LAN may never happen?

## Sources

All paths under `/home/claude/ref/CnC_Remastered_Collection/`.

- `TIBERIANDAWN/RULES.CPP:1-126`, `TIBERIANDAWN/RULES.H:60-100`: AI-only rules class.
- `REDALERT/RULES.CPP:344-363` (Process), `382-550` (General/Aftermath keys), `568-1000` (Recharge, AI, IQ), `840-870` (Land_Types), `974-1010` (Objects), `1045-1053` (Difficulty stub).
- `REDALERT/RULES.H:36-60, 495, 626, 669, 786`: `DifficultyClass`, `Diff[]`, `ShroudRate`, `GapShroudRadius`, `CrateRadius`.
- `REDALERT/INIT.CPP:300-322`: `RULES.INI` and `AFTRMATH.INI` load.
- `REDALERT/SCENARIO.CPP:2240-2275`: post-load patches and per-scenario rule re-read.
- `REDALERT/WEAPON.CPP:201-231`, `REDALERT/WARHEAD.CPP:77-180`, `REDALERT/TECHNO.CPP:6733-6800`: `Read_INI` for weapons/warheads/techno types.
- `TIBERIANDAWN/CONST.CPP:71-80`: compiled weapon table.
- `REDALERT/FIXED.H:20-110`, `REDALERT/FIXED.CPP`; `TIBERIANDAWN/JSHELL.H:221-245`: fixed-point, cardinal helpers.
- `REDALERT/CCPTR.H:20-113`: index-handle pointer. `REDALERT/BIGCHECK.CPP:30-81`: checkbox gadget.
- `REDALERT/RANDOM.CPP:86-103`; `REDALERT/INLINE.H:983-1030`; `REDALERT/GLOBALS.CPP:284`; `TIBERIANDAWN/RAND.CPP:89-106`; `TIBERIANDAWN/FUNCTION.H:626,794`: RNG streams.
- `REDALERT/QUEUE.CPP:640-680`, `TIBERIANDAWN/QUEUE.CPP:683-684`: CRC ring.
- `REDALERT/NETDLG.CPP:3108, 5317-5319`; `REDALERT/NULLDLG.CPP:4026, 4210`; `REDALERT/CCINI.CPP:1413`: rules digest check.
- `REDALERT/SESSION.H:64-122, 459`; `TIBERIANDAWN/DEFINES.H:2504-2509`: game types and packet size.
- `REDALERT/SUPER.H`, `TIBERIANDAWN/SUPER.H`, `REDALERT/DEFINES.H:806-820`, `TIBERIANDAWN/DEFINES.H:309-314`, `REDALERT/HOUSE.CPP:659, 1514`: superweapons.
- `REDALERT/CRATE.H`, `REDALERT/CRATE.CPP`, `REDALERT/DEFINES.H:776-796`, `REDALERT/MAP.H:149`: crates.
- `REDALERT/VORTEX.H`, `REDALERT/VESSEL.H:44-56`, `REDALERT/VESSEL.CPP:1069-1090`, `REDALERT/DRIVE.CPP:1338-1375`, `REDALERT/FOOT.CPP:596,994`: vortex and naval.
- `REDALERT/MAP.CPP:1055-1080`, `TIBERIANDAWN/MAP.CPP:904-930`: Tiberium/ore growth.
- `REDALERT/LOGIC.CPP:280`, `REDALERT/CELL.H:121`, `REDALERT/MAP.H:71-87`: shroud regrowth and jamming.
- `REDALERT/TRIGTYPE.H:36-100`, `REDALERT/TEVENT.H:44-80`, `TIBERIANDAWN/TRIGGER.H:39-70`: trigger structure.
- `REDALERT/DLLInterface.cpp:122-154, 350-356`, `REDALERT/DLLInterface.h:829`, `TIBERIANDAWN/DLLInterface.cpp:256-450`, `README.md`: Remaster DLL.
- Directory listings and `wc -l` comparisons of `TIBERIANDAWN/` and `REDALERT/`.
