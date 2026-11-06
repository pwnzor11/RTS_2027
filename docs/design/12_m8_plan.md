# M8 plan · The Campaign, all four factions (D-005, D-030, D-087)

Status: lead plan, session 9, plan only (no code). Prerequisites: M6 (Left Behind) and M7 (Expedition) landed. Every story choice is
"assumed — confirm" and stays a proposal until the owner approves it (D-086); the lore already approved is only what is in docs/lore.
Every number is `(tune)`. Sections 1-2 are the story plan, 3 the systems, 4-6 the work plan.

## 1 · Goal and what the player can try
- Main menu, Campaign, pick a faction (Authority and Directorate open; Left Behind and Expedition unlock by progress, §2.4), read a
  briefing, play a scripted mission, win or lose, see the next briefing. Progress is saved between sessions.
- Missions grant buildings, units and upgrades as the story introduces them (a tech tree that opens up, priority 1 in 00_START_HERE).
- Archive entries marked `unlock: campaign` (leader secrets, late units) open as missions are completed (D-086).
- First slice at the end of M8 (recommended): **3-4 missions per faction, 14 in all** `(tune)`, plus one shared tutorial. The full
  10-12 per faction of D-030 is a later expansion (M8b) once the slice has measured the real cost (§5).

## 2 · Campaign structure (owner answers D-154, D-155; remaining story assumed — confirm)
### 2.1 What is fixed
- D-087: one campaign written once for four factions. D-030: one spine per side, 2-3 two-way choice points that rejoin, maps reused with
  different scripts, no unit carry-over, text briefings, radio messages, 2-3 stills at act breaks. D-020 (two-faction era): parallel and
  opposed campaigns, a clear protagonist and antagonist. D-030's 10-12 missions per side is the target for the full campaign.
- Timeline from docs/lore/world_history.md: war begins 2001 after the Spring 2000 convoy loss and the surfacing; the Left Behind are
  watching the Glass Coast (left_behind.md timeline, 2001); the Second Relief Fleet sailed spring 2000 (expedition.md, faction_exp_reason).

### 2.2 Arc: one shared year, 2001, told four ways (Q-A, D-154)
Missions are told from each side as a *parallel* war (the D-020 pattern), not a branching plot. Four acts, each faction gets one
mission per act in the slice:
| Act | Shared events (all from existing lore) | Authority | Directorate | Left Behind | Expedition |
|---|---|---|---|---|---|
| 1 Surfacing | Directorate surfaces openly; Authority mobilises | A1 Survey Convoy (exists: auth_01) | D1 first hatch opened | L1 the refused: raid an Authority convoy | E1 first landing, a beach |
| 2 Rust Belt | Convoy Roads fight; the Glass Coast is eyed by both | A2 hold a road junction | D2 raid the roads | L2 strip the Authority road posts | E2 beachhead, supply line |
| 3 Glass Coast | Both big powers send forces into the dunes | A3 escort to the coast | D3 take the launch sites | L3 break into a Directorate hatch field | E3 fleet reaches the coast |
| 4 Reckoning | Sunrise Device (Order Seven, D-086 canon) and Salvo Array come into play | A4 stop the device | D4 fire the device | L4 drive the Expedition off the coast; the warlord takes over | E4 the ash fields or the fuel decision |
Cells are working titles only. The Left Behind are hostile to everyone (D-155): Authority first (the refusal), then the Directorate, then
the Expedition; their victory is taking over. The 4th mission of each faction is the slice's finale; it ends in that faction's victory
and the ending sequence in 3.8 (Q-C, D-154).

### 2.3 Where the stories cross (assumed — confirm)
- **Same map, different eyes (D-030 map reuse):** each act uses one map; each faction plays it with its own script (e.g. act 2 Rust Belt
  map: Authority holds it, Directorate raids it, Left Behind scavenge it, Expedition land on its coast edge). A procedural map is **not**
  used for story missions; hand-made maps in the editor (M3) so triggers can name zones.
- **Cameos:** a mission may show another faction's units as scripted neutral or hostile actors, with a radio line from that faction's leader
  (leaders.md, left_behind.md, expedition.md names only; no new characters).
- **Archive crossings:** completing a mission can unlock another faction's classified entry (e.g. the Directorate's A3 completion opens
  the Authority entry about the Annex) so the player pieces the timeline together.

### 2.4 Which faction unlocks when (assumed — confirm)
Recommendation: Authority and Directorate are open from the start (they are the original pair; auth_01 exists). **Left Behind unlocks
after act 2 of either** original faction; **Expedition unlocks after act 3 of either**. Rationale: the Left Behind and the Expedition
enter the war late in the lore (2001 caravans notice; the fleet is far away) so the order matches the story. Option B: all four open at
once (simplest, no spoilers protected). Option C: strict linear order, rejected (hides content from a returning player).

### 2.5 Story answers (D-154, D-155)
| Id | Answer |
|---|---|
| Q-A | One shared 2001 told four ways (2.2). |
| Q-B | Each campaign has its own hero. |
| Q-C | Each campaign ends with its faction's victory over the others, then a Fallout-style ending (3.8). Each ending is that faction's version; no canon winner. |
| Q-D | No choice points in the first slice (3.6 stays unused). |
| Q-E | The Left Behind are hostile to every faction: refused by the Authority, years of infighting, now loosely allied clans under a warlord; sympathetic but ruthless (docs/lore/left_behind.md, D-155). |
| Q-F | Commissioner Strand decides the Expedition's stance (assumed — confirm, Q-56). |
| Q-G | Existing leaders only; no new names without approval. |
| Q-H | Text now; every line keyed in a locale file with an optional voice file (3.2); setup guide for a local voice generator in docs/audio/voice_setup.md. |
| Q-I | Campaign difficulty reuses the five skirmish tiers, no resource cheats (D-061). |

## 3 · Systems
### 3.1 What exists and what is missing
Verified in code (worktree wp/s9-m8):
- **Extension point:** `ISimExtension` has Name, OnSetup, OnTick, State (src/Sim/Core/ISimExtension.cs:11-28). `Simulation` calls
  `RunTriggers()` in the trigger phase of `Step` (src/Sim/Simulation.cs:239) which loops `Setup.Extensions` in list order (Simulation.cs:628-633);
  the extension's state part is appended after the built-in parts (Simulation.cs:94, 112).
- **Lua host:** `src/Scripting` (MoonSharp, `MissionHost : ISimExtension`, MissionHost.cs:23). API tables registered in
  MissionHost.Api.cs:35-100: Trigger (AfterDelay, Every, OnEnter, OnDestroyed, OnAllDestroyed), Actor (Create, Order, Alive, Count,
  CountInZone, Cell), Reinforce.Edge, Objective (Add, Complete, Fail), Tech (Grant, Revoke, Has), Media.Message, Player (Cash, AddCash),
  Map (Zone, Center, Width, Height), Mission (Win, Lose, Tick), Random.Next. Sandbox integers only; an instruction budget stops runaway
  scripts (InstructionBudget.cs, MissionHost.cs:35, 83). Pilot cost: docs/missions/pilot_report.md (about 10-12 h per mission).
- **Mission state part:** "mission" is the `MissionStatePart` the host registers; granted tech sets are per-player sorted sets saved in it
  (MissionHost.cs:52, 280). `Sim.UnlockFilter` is the gate (Simulation.Production.cs:105-110; set at MissionHost.cs:137).
- **Replays:** replay header carries mission id and script hash; playback re-runs the script (D-092; tests/Sim.Tests/Replay/MissionHeaderTests.cs:11-35).
- **Game side:** menu has a Campaign entry (src/Game/Menu/MainMenuModel.cs:50), `BriefingScreen`, objectives HUD and ticker (pilot report).
  Archive has `ArchiveUnlockKind.Campaign` keyed by mission id (src/Game/Archive/ArchiveUnlockKind.cs, ArchiveProgress.cs:36-58).
- **Save version:** `SimWriter.SaveVersion = 10` here (src/Sim/Serialization/SimWriter.cs:15); M7 plans 12. M8 takes the next free number.
- **Tech:** per-type `Prerequisites` plus faction tier names appended by `TierSugar` (src/Data/TierSugar.cs:8-12). data/rules/techs.toml is
  currently a **test fixture** of effects, not a campaign unlock list.

Missing (this is the M8 work; none exists today):
| Need | Today | M8 addition |
|---|---|---|
| Objectives | Add/Complete/Fail, no primary/secondary, no hidden objectives | `primary` flag, hidden-until-revealed, on-screen text |
| Timers | AfterDelay, Every; no player-visible countdown | `Mission.Timer(id, ticks)` shown in HUD; expiry callback |
| Reinforcements | `Reinforce.Edge` only | from a zone, by a transport, delayed arrival, naval (Expedition) and "rig arrives" (Left Behind) |
| Scripted dialogue | one-shot `Media.Message` | speaker, portrait, locale key; queueing; pause-free radio |
| Camera | none | `Camera.Focus(zone/actor, ticks)` as a **view hint to the game layer**, never read by the sim |
| Win/lose | `Mission.Win/Lose(player)` | multi-condition helpers, "any primary failed" |
| Area triggers | OnEnter | OnLeave, OnHold(n ticks), OnSeen, OnIdle |
| Unit spawn | Actor.Create | groups, formation, veterancy level from M7 |
| AI script hooks | none | `AI.Attack(zone)`, `AI.Defend(zone)`, `AI.SetTier`, wave spawns for the built-in AI (never inside Step, D-051: hook sets a flag in hashed state the AI reads next tick) |
| Per-mission tech | `Tech.Grant/Revoke` | persistent campaign unlocks (3.3), upgrades, superweapons |
| Faction mechanics | none | Left Behind pack/rig actions and Expedition beachhead/landing from scripts |

### 3.2 Briefings
Screen: reuse `BriefingScreen`, add portrait, faction theme, map thumbnail, objectives preview and a Start button.
Text in `data/missions/<id>/briefing.md` plus a per-mission locale file keyed by string id (D-030: all text in one locale file) so voice
can be added later. Archive links: a briefing may name Archive ids (`[[leader_auth_wrenfield]]`); the link opens the entry if unlocked,
greyed otherwise. Radio messages (3.1 dialogue) use the same locale keys. Voice slot (Q-H, D-154): each locale entry may name an optional
`voice = "voice/<leader_id>/<key>.ogg"`; the game plays it when the file exists, else shows text only. No voice files are required for M8.

### 3.3 Tech gating and campaign carry-over (D-005, D-030)
- Keep one skirmish tech tree. A mission has `[players].tech` (existing allow-list) and the script's `Tech.Grant`. Both **only restrict
  or lift** what prerequisites already allow (`UnlockFilter`); skirmish ignores them.
- New: a **campaign progress file** per profile and faction: completed mission ids, difficulty per mission, and the *campaign tech set*
  (ids granted by finished missions). Each mission's `mission.toml` has `[unlocks_on_win]` listing ids added to the campaign tech set,
  and `[starts_with]` lists ids always available. At mission start the tech allow-list = `starts_with` + the set. No unit carry-over (D-030).
- Mid-mission saves (D-018): the allow-list already lives in the saved "mission" part. The campaign set is **not** in the sim save (it is
  profile data); a mission save stores which mission id it is and the set at start, so reload is exact.
- Archive: `MarkCampaignMission(id)` is called on win; entries already key off mission id.

### 3.4 Campaign flow
Menu Campaign -> faction pick (locks per 2.4) -> mission select (completed, next, locked) -> briefing -> mission -> debrief (stats, unlocks,
new Archive entries) -> next. Progress saved after each win; replay a completed mission any time. Difficulty chosen at mission start from
the skirmish tiers (Q-I): changes enemy strength only, never resource or vision bonuses (D-061). Lose: retry or quit; no penalty.

### 3.5 Mission file format
`data/missions/<id>/` holds `mission.toml` (title, map, players, zones, actors, `starts_with`, `unlocks_on_win`, `act`, `faction`, `next`),
`mission.lua` (named top-level functions only, 100-300 lines), `briefing.md`, `locale.toml` (text by key). A new `data/campaigns/<faction>.toml`
lists mission ids in order with unlock rules. The script hash goes in the replay header and the "mission" part (exists).

### 3.6 Choice points (not used in the slice, Q-D)
`next` may be a table of two ids selected by `Mission.Win(player, "route_b")`; the campaign file lists both successors and the rejoin.

### 3.7 Determinism and save format impact
- Scripts run **inside the tick** via `ISimExtension` (RunTriggers); they use their own seeded RNG, integers only, no wall clock (D-071).
  All new API stays sandboxed; camera/dialogue/timer HUD data is *outputs* read by the game layer after the tick, never fed back into the sim.
- New script state goes through the existing Lua state codec; new fields (timers, hidden objectives, AI hook flags) are added to the
  "mission" part version and listed in HashCoverageTests. No new sim parts are expected; if AI hooks need one it is appended last.
- Save and replay format bump by one (next free number after M7's 12; assumed 13). Older saves refuse as before.
- Mission replays: playback re-runs the script (D-092). Open gap from the pilot: script-driven spawns must replay. Acceptance (§6) checks it.
- Campaign progress file is separate, versioned, human-readable TOML; losing it never corrupts a sim save.

### 3.8 Ending sequence (Q-C, D-154)
After a faction's final mission: a short victory debrief, then 6-10 captioned slides in the Fallout manner, each a still (or a reused
map view) with 2-4 sentences: what became of each major character of that campaign (its leaders, the rival leaders it met), and how the
victory shaped the wider world (the Authority's towns, the tunnels, the wastes, the coast, the fleet's homeland). Slides may vary on
mission facts the progress file records (e.g. difficulty, optional objectives completed). Data: `data/campaigns/<faction>_ending.toml`
(ordered slides, each: image id, locale key, optional condition), text in the faction's locale file, voice slot as in 3.2. Screen:
`EndingScreen` (game layer only, no sim impact). Package M8-16 (screen + data format), slides written per faction with M8-12; owner reviews.

## 4 · Contracts
- **API additions (lead, M8-02):** the table in 3.1 as Lua function signatures with argument checks; every new function listed in
  `docs/missions/api.md` with an example. Removing or changing an existing pilot function is not allowed (auth_01 must still pass).
- **Mission file schema v2:** keys in 3.5; the loader accepts schema 1 (auth_01) unchanged.
- **Progress file:** `{version, faction, completed[], difficulty{}, techSet[]}`; read-only to scripts.
- **Game-layer hooks (outputs only):** `MissionView` exposes timers, objectives, messages, camera hint; read after each tick.
- **Rules:** none of the mission code reads rules types by index; scripts use string ids.

## 5 · Packages
| Id | What | Owns (main files) | Depends on |
|---|---|---|---|
| M8-02 | Contracts: API table, schema v2, progress-file shape, format bump, hash tests | src/Scripting (API stubs), docs/missions/api.md, SimWriter | M6, M7 |
| M8-03 | Objectives, timers, area triggers, win/lose helpers | MissionHost.Api.cs, TriggerRecord.cs | M8-02 |
| M8-04 | Reinforcements (transport, delayed, naval, rig arrival), spawn groups | MissionHost.Reinforce.cs | M8-02, M6, M7 |
| M8-05 | Scripted dialogue, camera hints, locale loader | MissionHost.Media.cs, locale | M8-02 |
| M8-06 | AI script hooks (attack/defend zone, wave spawn, tier) | src/AI hook reader, MissionHost.Ai.cs | M8-02 |
| M8-07 | Campaign progress, tech set, unlocks_on_win, Archive wiring | src/Game/Campaign/*, ArchiveProgress | M8-02 |
| M8-08 | Game UI: faction pick, mission select, briefing v2, debrief, difficulty | src/Game/Screens | M8-07 |
| M8-09 | Tutorial mission (shared) + `--mission-check` and bot test template | data/missions/tut_01, tests/Scripting.Tests | M8-03 |
| M8-10 | **Pilot missions**, one per faction: Authority A1 (port auth_01 to schema 2), D1, L1, E1; hours logged | data/missions/*_01 | M8-03..07 |
| M8-11 | Owner review of the four pilots (briefings, tone, tech unlocks, difficulty) | owner | M8-10 |
| M8-12 | Fan-out: remaining missions per faction (2-4 each), 1 helper per faction in own worktree | data/missions/<faction>_02..04 | M8-11 |
| M8-13 | Archive campaign unlock ids, classified leader entries | data/archive | M8-07 |
| M8-14 | Stills at act breaks (2-3 per faction), art pilot then fan-out | tools/art | owner approval |
| M8-16 | Ending sequence: EndingScreen, `<faction>_ending.toml`, conditions from the progress file | src/Game/Screens, data/campaigns | M8-07 |
| M8-15 | Acceptance: bot play-through of every mission, mission replays, balance table | tests, docs/balance | all |

Mission-authoring workflow (D-039 pilot rule): (1) paper design (beats, archetype, tech granted, map use, difficulty) one page,
(2) toml + zones, (3) lua, (4) briefing + locale, (5) bot test, (6) 4-6 play-throughs tuning, (7) owner approval. **Pilot one mission per
faction first (M8-10)**, review it with the owner (M8-11), only then fan out. Estimate 10-12 h per mission (pilot report); 14 missions
~ 140-170 h, more for the first of each faction's mechanics (rig, landing). Archetype mix per D-030: half build-and-destroy, a quarter
defend/escort, a quarter no-base/strike; E1 and L1 are the no-base missions because their start kits are mobile.

Waves: wave 1 = M8-02 (lead-reviewed). Wave 2 = M8-03..07 parallel from `integ-m8`. Wave 3 = M8-08, 09, 13. Wave 4 = M8-10 pilots, then
M8-11 gate. Wave 5 = M8-12, 14. Wave 6 = M8-15. M8 does not start before M6 and M7 packages that mission scripts use have landed.

## 6 · Risks and acceptance
Risks: new API grows beyond "about 30 functions" (D-015); faction mechanics (pack, landing) need mission-safe script entry points;
balance of campaign tiers differs from skirmish (research 16); story approval can block fan-out (so pilots are scoped to tone, not plot).
Acceptance:
- All pilot and fan-out missions are winnable by the bot test and by the owner on Normal; every mission loads from a mid-mission save.
- Mission replays reproduce the final hash for every mission, including script-driven spawns and reinforcements (closes the pilot-report gap).
- Progress save survives quit/resume; tech unlocks carry between missions and never leak into skirmish; Archive campaign entries unlock.
- All four factions can be started from the menu per the unlock rule (2.4); no story beat appears that the owner has not approved.
- Full suite green on Linux and Windows CI.
