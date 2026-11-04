# Design 08 · M4 plan: procedural maps, spectator mode, garrisons, tech tiers, map editor
From D-087 (session 4). All five features have equal priority. This file is a plan, not a decision: the lead turns §6 into
code stubs (M4-01) and records any lead choices as new D-entries. Q-drafts for the owner are in §9.

## 0 · Summary and what the owner can try at the end
**Owner can try at the end:**
- Skirmish with 2–4 players on a generated map: pick a biome (Rust Belt ruins, Glass Coast dunes, Mountain passes, Ash plains
  and rivers), a size (S/M/L) and a seed. Every start is fair by construction, and the generator checks connectivity.
- Watch AI against AI: set every slot to AI (2–4 of them) and spectate. Toggle full map vision or one player's fog, read a
  live stats panel per player, and run at pause / 1× / 2× / 4× / 8×. Save the replay at the end.
- Put infantry into Blockhouses, Redoubts and (if Q-M4-b is approved) ruined neutral buildings. They fire out with more range and a faster rate of fire,
  and are thrown out when the building dies or on Unload.
- Climb three tech tiers per faction: tier 2 after the Engineering Works / Isotope Works, tier 3 after the Signals HQ /
  Committee Chamber. Research upgrades per tier that really change stats. Use about a dozen new units and structures,
  including Authority air, the Seismic Mortar and Quick-Set Foam that D-074/D-077/D-079 promised.
- Open the Map Editor from the main menu. Paint terrain, resources, features and starts with a symmetry helper, validate,
  save, then play the map in skirmish.

All numbers are starting values marked `(tune)` in TOML. All new names are **working names, assumed — confirm**. The
balance pass (M4-16) sets the numbers from headless matches. Ticks run at 20 Hz. 1 cell = 256 leptons.

**Format bumps (one D-entry, like D-082):** RulesHash FormatVersion 6→7, SaveVersion 7→8, Replay Version 7→8, MapFile
format_version 1→2 (the loader still reads 1). Old saves and replays are rejected with the existing message.

## 1 · Procedural maps (D-087)
**Entry point.** `MapGenerator.Generate(MapGenParams p)` in `src/Data`. The output depends only on (biome, size, players,
seed). It uses only `SimRandom`, integer maths and scans in cell-index order. The legacy `Generate(int size, ulong seed,
string? name)` stays **byte-identical**: existing tests, goldens and the "seed ≠ 0" skirmish path rely on it.

**Sizes (tune):** Small 64×64, Medium 96×96, Large 128×128 (128 = `SimConstants.MaxMapCells`). All sizes allow 2–4
players. On Small, 4 players is marked "tight" in the setup screen.

**Fairness by construction.** The generator paints one *fundamental domain* and copies it with the symmetry transform.
- 2 players: `Rot2` (point symmetry, today's scheme), or the biome's `MirrorX` / `MirrorY`.
- 4 players: `Rot4` (90° rotation, square maps only).
- 3 players: see Q-M4-a. Recommendation: a `Rot4` map with 4 starts, where 3 are used and the empty start's resources
  become a contested expansion. The slot-to-start assignment is rotated by seed.
- Each transform maps cell (x, y) to its images. A paint writes every image or none, like today's `Grid.Paint`.

**Checks** (retry `seed + k` up to `MaxAttempts` = 10, then throw, as today):
1. Every start reaches every other start for `Foot` and for `Tracked`. Use a 4-way flood fill through the real
   `TerrainTable` when one is given, otherwise the built-in conservative rule.
2. Every start reaches at least `min_fields_per_start` = 2 resource fields within 14 cells of path distance (tune).
3. Equal paths: the BFS path length from each start to (a) its nearest field, (b) the map centre and (c) each other start
   is the same across starts, ±0 under exact symmetry. The check is still run because it catches transform bugs.
4. A 5×5 clear, buildable square at each start (the largest start kit's conyard is 3×3, plus the build radius).
5. At least 12 cells (Chebyshev) between any two starts.

**Biomes.** The parameters live in `data/mapgen/biomes.toml`. They are *not* sim rules: maps are identified by the map
hash, so the parameters stay out of RulesHash.

| Biome (`biome` key) | Theater | Default symmetry 2p / 4p | Character |
|---|---|---|---|
| `rust_belt` | `rustbelt` | Rot2 / Rot4 | Road grid (Road land), rubble blocks (Rough + Rock), frozen canals (Water) with Shallow fords, ruined neutral buildings (features), rich ashfall along roads |
| `glass_coast` | `glasscoast` | Rot2 / Rot4 | Ocean on **all four edges** (Water, deep), Shallow lagoons, dune ridges (Rough with impassable Rock "glass" crests), sparse Pitchblende. Every start is a shore start (M7-ready; Q-M4-d) |
| `mountains` | `mountains` | MirrorX / Rot4 | Plateaus (HeightLevel 1–2) ringed by Cliff, 2–4 cell passes as chokes, Rock spurs, little water, fields in valleys |
| `ash_plains` | `ashplains` | MirrorY / Rot4 | Open Ash land, 1–2 rivers (Water) crossing the symmetry axis with Shallow fords and Road bridges every 16–24 cells (tune), dense Ashfall |

Per-biome keys (all tune): `theater`, `symmetry_2p`, `symmetry_4p`, `water_permille`, `rock_permille`, `rough_permille`,
`cliff_plateaus`, `rivers`, `ford_spacing`, `road_grid` (cells, 0 = none), `ash_fields_per_start` (3), `pitch_fields_per_start`
(1), `contested_fields` (2), `field_density_min/max` (3/12), `ruins` (neutral feature count per start, 0 when Q-M4-b is no).

**Water hook for M7 (design 07 §6.1 option A).** Append `LandType.Shallow = 7`, a fordable shallow (Foot 40 %, Wheeled 0,
Tracked 0, Hover 100, Air 100, Burrow 0; not buildable; hard). Existing `Water = 4` now *means deep*. `Coast = 8` is
**reserved** for M7 and is not added in M4. Generators always surround deep water with at least one ring of Shallow where
a shore meets land, so M7 can later re-tag shore cells without moving any coastline. Glass Coast guarantees an ocean edge
and shore starts (design 07 §6.7). There is no naval code and no water buildings in M4.

**Map format v2** (`MapFile.FormatVersion = 2`; version 1 files load with defaults):
- `[map]` new optional keys: `biome` (string, default ""), `symmetry` ("none"|"rot2"|"rot4"|"mirror_x"|"mirror_y"|"mirror_diag",
  default "none"), `max_players` (int, default = number of start positions), `[map.generator]` (optional provenance:
  `biome`, `size`, `players`, `seed`; informational only, never re-run on load).
- `[[features]]` (optional array): `type` (building id), `cell` = [x, y] (top-left). These are neutral structures placed at
  setup (§3, Q-M4-b).
- `[layers]` unchanged. The `land` bytes may now hold 7 (Shallow).
- Theater palettes: `data/theaters/<theater>.toml` gains `[land.shallow]`. New files: `glasscoast.toml`, `mountains.toml`,
  `ashplains.toml`. The renderer loads a palette by the map's `theater` id instead of special-casing `rustbelt`. The test
  maps use the id `rust_belt`; make it an alias of `rustbelt`.

**Tools:** `map gen --biome <b> --size s|m|l --players <n> --seed <s> --out <path>` and `map validate <path>`.

## 2 · AI-vs-AI spectator mode
**Setup.** `SkirmishSettings` v2 (§6) holds a map choice (curated file / generated params / user file) and 2–4 slots. Each
slot is Closed, Human or AI, with a faction, AI tier, colour and team. With no Human slot the game is a spectator match:
`SkirmishGame.LocalPlayer` is null, every AI slot is `AiDriver.Add`ed, and the default viewer is −1 (omniscient).

**Determinism.** Nothing changes in the sim. AIs still run in `AiDriver.Collect` before `Step` (D-051, `SimHost.StepCore`),
and their commands are recorded in the replay. Speed is front-end pacing only: `SimHost.SpeedMultiplier` steps N ticks per
20 Hz interval. Acceptance test: a 4-AI match stepped at 8× through `Advance` and the same match stepped headless with
`StepTicks` give identical `RecentHashes`. The generated map is written next to the replay as `<replay>.map.toml`, so
`replay verify --map` works. The replay header is unchanged.

**Observer controls (spectator only):**
- Camera: free pan and zoom, minimap click. No selection orders (the order controller is disabled). Clicking a unit shows
  its info panel read-only.
- Vision: `V` toggles between full map vision (viewer −1) and the fog of the player picked in the stats panel. `F2` cycles
  −1, 0, 1, … N−1 (generalises today's 0 → 1 → −1).
- Speed: `Space` pauses or resumes. Keys `1` / `2` / `3` / `4` set 1× / 2× / 4× / 8×. The speed shows on screen. The
  250 ms catch-up cap stays: if the machine cannot keep up at 8×, the game runs slower but stays deterministic.
- Stats panel (top strip, `Tab` for detail). One row per player: colour, faction, tier (1–3), cash + stored, income per
  minute (front-end ring buffer over the last 60 s of snapshots), army value (sum of cost of alive units), structures,
  power out/use, units built/lost, kills, upgrades researched. Defeated players are greyed out. Counters come from the new
  `"stats"` state part (§6), so they are deterministic and saved.
- At game over: winner team plus the full stats table, then "Save replay" / "Watch again" (same seed) / "Menu".

**AI changes for N players** (M4-12): target choice works for 2–4 players (nearest undefeated enemy by path distance from
own start, re-evaluated every 30 s (tune)). The scout route no longer assumes the mirror corner. `AiMatchRunner` and
`Tools ai matrix` take 2–4 sides (`SideTracker` stops indexing with `& 1`).

## 3 · Garrisoning
**Reuse.** Garrisoning is boarding a transport whose carrier is a building:
- `Enter` (CommandKind 9) with Target = a building whose type has `garrison > 0` routes to `ApplyGarrison`. Routing order
  in `ApplyEnter`: (1) own building with Entrance ≠ None → tunnel (unchanged); (2) building with `garrison > 0` that is own,
  allied, or neutral and empty → garrison; (3) otherwise → engineer (unchanged). No current type is both an entrance and
  garrisonable; the loader rejects that combination.
- State uses the existing `"tunnels"` part: `InfantryLayer = ActorLayer.Garrisoned` (7, new) and `InfantryCarrier` = the
  building's EntityId. The layer says which store the id indexes (Passenger → vehicle, Garrisoned → building). No new
  per-infantry arrays.
- Walking there: the existing `MissionKind.Capture` handler (it already walks soldiers to a transport) gets a third arrival
  branch, `GarrisonNow`, when the soldier is within `enter_range_cells` = 1 of the footprint. Inside, the soldier runs
  `MissionKind.Garrisoned` (15, new): acquire and fire.
- Unload: new `CommandKind.Unload = 22` with Subject = the building (Arg low byte = `ActorKind.Building`). It also accepts a
  transport vehicle (Arg low byte = Vehicle), as an alias of Deploy-unload, so the UI has one verb. It uses the same
  exit-cell search as `FindUnloadCell`, run around the footprint (rings 1–2). With no room, the rest stay inside. M7 reuses
  `Unload` for landing craft with Cell = shore (design 07 §6.3). That is why it is one shared kind.

**Rules (tune):**
- Who may enter: infantry with `can_garrison = true` (the default; engineers default to false). Not underground and not a
  passenger. The building must be alive, finished (not an emerging entrance) and have room.
- Firing out: each occupant fires its own weapon with its own reload (its `CombatEntry`). Range is measured from the
  nearest footprint cell, and the weapon range gets `+ garrison_range_bonus` (building key, default 256 = +1 cell). Reload is
  multiplied by `garrison_rof_permille` (default 750). Projectiles start at the building centre. Occupants see with their
  own sight from the building centre.
- Occupants are hidden from enemies and cannot be targeted (`IsHiddenFrom` / `IsUntargetableBy` treat Garrisoned like
  Passenger). They hold no cell, are excluded from splash, and cannot be crushed. Enemies see `BuildingView.Occupants`
  (the count) only while the building is visible now, never on a ghost.
- Clearing a garrison: warheads with `garrison_damage_percent > 0` (Ashthrower, Collapse Charge, Seismic Mortar (tune))
  pass that % of each hit on the building to every occupant. Other warheads hurt only the building.
- Building dies or is sold: every occupant is ejected (exit search as Unload). Each takes `eject_damage_permille` = 200 of
  its max health (game.toml `[garrison]`). Occupants with no room are lost, like passengers today. Ejection happens in
  `OnM3BuildingRemoving`, before the building's cells are freed, in infantry slot order.
- Capture: an engineer capturing an occupied building first ejects the occupants with no damage, then captures it.
- Neutral buildings (map features, Q-M4-b): the first soldier to enter flips `_buildings.Player` to the occupier, and
  `GarrisonState.HomeOwner[b]` remembers the old owner. When it empties, ownership reverts. While flipped, it is an
  ordinary enemy building to others (auto-targeted). It never counts for defeat (`UpdateDefeat` ignores buildings with
  HomeOwner ≠ 255) or for tiers.

**Garrisonable types:** `auth_blockhouse` (new, T1 defence, 2×2, garrison 5, no gun), `dir_redoubt` (new, T1 defence, 2×2,
garrison 5, half-buried, fortified armour), neutral `neu_ruin_tenement` (3×2, garrison 6) and `neu_ruin_depot` (2×2,
garrison 4). Existing types stay `garrison = 0`. Their capacity is easy to add in data later (e.g. `auth_barracks`).

## 4 · Three-tier tech trees and upgrades
**Mechanism (no new sim gate).** Tiers are prerequisites with sugar:
- Each faction names its tier provides in `game.toml [faction.<name>]`: `tier2 = "auth_tier2"`, `tier3 = "auth_tier3"`
  (Directorate `dir_tier2` / `dir_tier3`).
- Every infantry, vehicle, building and upgrade gets `tier` = 1 | 2 | 3 (default 1). At load, a type with tier N ≥ 2 gets
  the faction's tierN provides name **appended to its prerequisites**, unless the type itself provides that name (the gate
  structure). RulesHash hashes the final list. A lost gate re-locks the tier (C&C behaviour, today's prerequisite rule;
  Q-M4-e).
- `HasPrerequisites` (units and buildings) accepts upgrade ids, as `UpgradePrerequisitesMet` already does. So a type can
  be unlocked by research. The effect op `enable` stays reserved and unused.
- `Simulation.TierOf(player)` = 3 if the player owns a live tier-3 provider, else 2 if it owns a tier-2 provider, else 1.
  The HUD, AI and stats use it. Tier is derived, not stored.

**Gate structures (tune):** Authority T2 = `auth_engineering` (provides `auth_tier2`; prerequisites `auth_power` →
`auth_factory`, so tier 2 is not instant). Authority T3 = **new** `auth_signals_hq` "Signals Headquarters" (3×3, 2500,
power −60, prerequisites `auth_engineering`, provides `auth_tier3`, researches; also gives a minimap ping when enemies are
spotted). Directorate T2 = `dir_isotope_works` (provides `dir_tier2`; prerequisites `dir_smelter_vent` → `dir_assembly_bore`).
Directorate T3 = **new** `dir_committee_hall` "Committee Chamber" (2×3, 2500, power −60, prerequisites `dir_isotope_works`,
provides `dir_tier3`, researches).
`researches = true` is also set on `auth_barracks`, `auth_factory`, `dir_muster_hatch` and `dir_assembly_bore`, so tier-1
upgrades have a home. There is still one research lane per player (ASSUMED M3-06).

**Tier assignments for every existing type (tune):**

| Tier | Authority | Directorate |
|---|---|---|
| 1 | auth_conyard, auth_power, auth_refinery, auth_barracks, auth_factory, auth_silo, auth_wall, auth_gate, auth_turret, auth_seismograph; auth_rifleman, auth_rocketeer; auth_scraper, auth_jeep, auth_tank | dir_shaft, dir_reactor_vent, dir_smelter_vent, dir_muster_hatch, dir_assembly_bore, dir_hatch, dir_bore_head, dir_cistern, dir_bunker, dir_pillbox, dir_rocket_nest; dir_conscript, dir_ashthrower; dir_scraper, dir_bore, dir_rocket_truck |
| 2 | **auth_engineering** (gate), auth_searchlight; auth_decon_truck; upgrade auth_grouting | **dir_isotope_works** (gate), dir_mast, dir_rad_mortar, dir_mooring; dir_sapper, dir_informant, dir_commissar; dir_mole, dir_agitprop, dir_dirigible |
| 3 | **auth_signals_hq** (gate, new) | **dir_committee_hall** (gate, new), dir_sunrise; dir_reactor_crawler, dir_landtrain |

The Seismograph stays at T1 on purpose: it is the counter to T1 Bores (D-074).

**New types ("fill out the rosters creatively", no quota; working names, assumed — confirm; numbers tune):**

| Faction | Id | Name | Tier | Kind | Role / mechanic (reuses) |
|---|---|---|---|---|---|
| Authority | auth_engineer | Field Engineer | 1 | infantry | `engineer = true` (Authority has none today) |
| Authority | auth_blockhouse | Blockhouse | 1 | defence | garrison 5 (§3) |
| Authority | auth_flak | Flak Emplacement | 2 | defence | anti-air gun `targets = "air"` (Authority has no AA vs dirigibles) |
| Authority | auth_marksman | Marksman | 2 | infantry | long-range anti-infantry; strong in garrisons |
| Authority | auth_carrier | Sealed Carrier | 2 | vehicle | `passengers = 5`, ashfall-proof APC (design 03) |
| Authority | auth_seismic_mortar | Seismic Mortar | 2 | vehicle | artillery; warhead `hits_burrowed`, `bonus_vs_entrance_percent` = 200 (D-074) |
| Authority | auth_foam_truck | Quick-Set Foam Layer | 2 | vehicle | warhead `foam_ticks` = 400: cells cannot be surfaced or broken through and slow every ground unit to 50 % (D-077) |
| Authority | auth_airfield | Airfield | 2 | structure | `produces = "aircraft"` |
| Authority | auth_interceptor | Interceptor | 2 | aircraft | air, anti-air (prop fighter) |
| Authority | auth_bomber | Heavy Bomber | 3 | aircraft | air, splash bombs vs ground |
| Authority | auth_howitzer | Field Howitzer | 3 | vehicle | long-range artillery (design 03 "best late-game artillery") |
| Authority | auth_bulwark | Bulwark Heavy Tank | 3 | vehicle | heavy armour, crusher |
| Authority | auth_atomic_battery | Atomic Artillery Battery | 3 | structure | superweapon (design 03 idea 1; reuses `superweapon`, `Special`) |
| Directorate | dir_assessor | Assessor | 1 | infantry | `engineer = true` |
| Directorate | dir_redoubt | Redoubt | 1 | defence | garrison 5 (§3) |
| Directorate | dir_volunteer | Volunteer | 2 | infantry | irradiated runner, `death_weapon` rad burst |
| Directorate | dir_ash_sower | Ash Sower | 2 | vehicle | lays radiation (contaminate-only weapon, no damage) |
| Directorate | dir_delver | Delver | 2 | vehicle | armed Burrow-locomotion ambush tank, tunnel M |
| Directorate | dir_great_dirigible | Great Dirigible | 3 | aircraft | heavy air bomber, rad bombs + Blackout |

Aircraft production must work for this (today `TryTypeInfo` refuses `ProductionCategory.Aircraft`). Contract: Aircraft
`Arg2` = a vehicle type index whose `locomotion = "air"`. The Vehicle category refuses Air types. Aircraft leave from a
building with `produces = "aircraft"` (dir_mooring, auth_airfield). There is no fuel, ammo or landing in M4 (Q-M4-c).

**Upgrades.** Through the existing upgrade lane. `upgrades.toml` gains `tier` and `effects` (a list of `techs.toml`
`[[effect]]` ids). The string `effect` stays for special behaviours (`grouting`). **Effects become real.** `RulesOf(player)`
returns a per-player derived `SimRules`: the base rules with the effects of every researched upgrade applied (upgrades in
index order, each upgrade's effects in list order). It is cached per player and rebuilt when the mask changes or on Load.
It is not state: it derives from the hashed mask. Ops `set` / `add` / `mul` (per mille). Targets: type id, provides name,
`class:vehicle|infantry|building`, plus new `weapon:<id>` and `warhead:<id>`. Allowed attrs:
- unit: `max_health`, `speed`, `sight`, `cost`, `build_time`, `rad_resist_permille`
- building: `max_health`, `sight`, `power`, `cost`, `ticks_per_point`, `superweapon_charge_ticks`
- weapon: `damage`, `range`, `rate_of_fire`
- warhead: `contaminate`, `blackout_ticks`

A raised max_health does not heal existing actors (Q-M4-j).

| Faction | Tier | Id | Name | Effects (tune) |
|---|---|---|---|---|
| Authority | 1 | auth_up_flak_vests | Flak Vests | class:infantry max_health ×1150 |
| Authority | 1 | auth_up_convoy_drill | Convoy Drill | auth_jeep, auth_scraper, auth_carrier speed ×1100 |
| Authority | 2 | auth_grouting | Foundation Grouting | existing (`effect = "grouting"`) |
| Authority | 2 | auth_up_composite_armour | Composite Armour | auth_tank, auth_bulwark max_health ×1200 |
| Authority | 2 | auth_up_sodium_lamps | Sodium Lamps | auth_searchlight sight +2 |
| Authority | 3 | auth_up_tungsten_shot | Tungsten Shot | weapon:auth_tank_cannon damage ×1250 |
| Authority | 3 | auth_up_signals_net | Signals Net | class:building sight +2 |
| Directorate | 1 | dir_up_mass_conscription | Mass Conscription | dir_conscript cost ×800, build_time ×800 |
| Directorate | 1 | dir_up_tunnel_bracing | Tunnel Bracing | dir_hatch, dir_bore_head ticks_per_point ×750 |
| Directorate | 2 | dir_up_hot_cores | Hot Cores | warhead:dir_rad_shell contaminate +40 |
| Directorate | 2 | dir_up_lead_lining | Lead Lining | class:infantry rad_resist_permille set 900 (Directorate types only, by id list) |
| Directorate | 2 | dir_up_blackout_canisters | Blackout Canisters | warhead:dir_blackout blackout_ticks ×1500 |
| Directorate | 3 | dir_up_order_seven | Order Seven | dir_sunrise superweapon_charge_ticks ×750 |
| Directorate | 3 | dir_up_fervour | Fervour | dir_conscript max_health +10; weapon:dir_rifle rate_of_fire ×900 |

That makes 14 upgrades, plus `auth_grouting` already counted. `RuleCounts.MaxUpgrades` = 64 stays. Four factions at about
15 each would fit (see the risk in §9). Lore entries follow D-086: the lead drafts them in docs/lore and the owner approves.

## 5 · Map editor (was M3-15)
- **Screen:** `MapEditorScreen` in `src/Game/Screens/Editor/`. It opens from the main menu "Map Editor" entry, which stops
  being greyed (D-085).
- **New map:** size S / M / L or custom 32..128, theater/biome, or "Generate…" with the §1 params as a starting point.
- **Tools:**
  - land brush (all 8 land types)
  - height brush (0..3)
  - resource brush (Ashfall / Pitchblende, density 1..12)
  - feature placer (neutral `neu_*` buildings, footprint preview)
  - start-position placer (1..4, numbered)
  - eraser and flood fill
  - brush size 1 / 3 / 5
- **Symmetry helper:** none / rot2 / rot4 / mirror X / mirror Y / mirror diag. Each stroke is applied to every image,
  overlays included. Starts are placed as a set (placing start 1 places its images as 2..N). The chosen symmetry is saved
  in `[map] symmetry`.
- **Undo / redo:** front end only, 100 steps.
- **Validate:** `MapValidator.Validate(MapData, TerrainTable?)` (shared with `map validate` and the generator).
  - Errors: 2–4 starts; each start has a clear 5×5 buildable square; starts at least 12 cells apart; all starts connected
    for Foot and Tracked; features on legal cells, not overlapping, not within 6 cells of a start.
  - Warnings: a start with fewer than 2 fields within 14 cells; a symmetry mismatch against the declared symmetry, with
    the first differing cell.
  - Issues are listed and clicking one centres the camera on it. Saving is allowed with warnings, not with errors.
- **Save / load:** `MapFile` v2 into the user map folder `%APPDATA%/RTS_2027/maps/` (Q-M4-h). The skirmish map picker
  lists `data/maps/*.map.toml` and the user folder.
- **Test play:** "Play this map" opens the skirmish setup with the map selected.
- **No sim code.** The editor edits a `MapData` builder. Determinism is not affected: the map hash identifies the result.

## 6 · Contracts
M4-01 writes these as stubs and pins them with `tests/Sim.Tests/M4/M4ContractTests.cs` (enum values, part order, TOML
keys). Helpers must not change them. If a contract is wrong, stop and report.

| Thing | Owner item | Exact names / payloads / values | What the other side may assume |
|---|---|---|---|
| `CommandKind` | M4-01 | `Unload = 22`. Subject = building (Arg low byte 2 = Building) or transport vehicle (Arg low byte 0). Cell ignored in M4 (M7: shore cell) | No other new CommandKind in M4. Garrison entry is `Enter` (9). M7 reuses 22 |
| `ActorLayer` | M4-01 | `Garrisoned = 7`. `ActorLayers.IsUnderground` does **not** include it. `IsHiddenFrom` and `IsUntargetableBy` treat it like Passenger | Garrisoned infantry may `Fire`. `InfantryCarrier` holds the building id |
| `MissionKind` | M4-01 | `Garrisoned = 15` (inside: acquire and fire). Walking in reuses `Capture` (14). Handler stub registered in `MissionHandlers` | M4-03 fills `GarrisonMission.Handle` |
| `LandType` | M4-01 | `Shallow = 7`; `RuleCounts.LandTypes = 8`. `Coast = 8` reserved (M7), not added. TOML key `shallow` | terrain.toml and the test fixture get `[speed.shallow]`, `buildable.shallow = false`, `soft.shallow = false` |
| `FxKind` | M4-01 | `GarrisonExit = 5` (Pos = building centre, Size = soldiers ejected) | Audio and render may map it |
| `DebugOp` | M4-01 | `GrantUpgrade = 6` (Arg2 = upgrade index, player = Command.Player) | Tests grant upgrades without research |
| State parts | M4-01 | Order: `… tunnels, ground, effects, garrison, stats, [extensions]`. `GarrisonState` (`Rts.Sim.Garrison`, Name `"garrison"`): `byte[] HomeOwner` per building slot, 255 = none. `StatsState` (`Rts.Sim.Stats`, Name `"stats"`): per-player `int[]` `UnitsBuilt, UnitsLost, StructuresBuilt, StructuresLost, Kills, CreditsHarvested, CreditsSpent` | All fields hashed and saved. The order test is updated once by M4-01 |
| GroundState field | M4-01 | Appends `int[] Foam` (ticks left per cell) to the `"ground"` part; part Version byte 1→2 | M4-05 decays and applies it. Hashed |
| Hooks (call sites placed by M4-01, bodies by owners) | M4-01 → M4-03/05/06 | `ApplyGarrison(Command, ActorRef)`, `ApplyUnloadCommand(Command)`, `OnGarrisonBuildingRemoving(int b)` (M4-03); `OnLethalHit(ActorRef target, byte source)` in `ApplyPendingDamage` when health crosses to ≤ 0, `OnStatsBuilt(byte p, ActorKind k)`, `OnStatsLost(byte p, ActorKind k)`, `OnStatsHarvested(byte p, int credits)`, `OnStatsSpent(byte p, int credits)` in `Spend` (M4-06); `ApplyWarheadFoam(WarheadType, CellCoord, int radius)` (M4-05) | Owners edit only their own partial files |
| Public sim API | M4-01 | `int Simulation.OccupantsIn(EntityId building)`; `int Simulation.TierOf(byte player)`; `PlayerStats Simulation.StatsOf(byte player)`; `readonly record struct PlayerStats(int UnitsBuilt, int UnitsLost, int StructuresBuilt, int StructuresLost, int Kills, int CreditsHarvested, int CreditsSpent)` in `src/Sim/Core/PlayerStats.cs`; `RulesOf(byte)` signature unchanged (now per-player) | Game, AI and Tools read only these |
| View | M4-01 | `BuildingView.Occupants` (init property, default 0: own/allied exact, enemy only when visible now, ghost 0); `PlayerView.OccupantsOf(EntityId)` (own/allied, else −1); `PlayerView.Tier`; `SimSnapshot` unchanged otherwise | AI garrison logic is fog-honest |
| Building TOML (schema v4) | M4-01 | `tier` (1..3, default 1), `garrison` (0..10, default 0), `garrison_range_bonus` (leptons, default 256), `garrison_rof_permille` (default 750). Loader error if `garrison > 0` and `entrance ≠ "none"`. `BuildingType.Tier / Garrison / GarrisonRangeBonus / GarrisonRofPermille` | Defaults leave every M3 type unchanged |
| Infantry / vehicle TOML | M4-01 | `tier` (both); infantry `can_garrison` (default true, false when `engineer = true` unless set). `InfantryType.Tier / CanGarrison`, `VehicleType.Tier` | |
| Warhead TOML | M4-01 | `hits_burrowed` (bool), `bonus_vs_entrance_percent` (default 100), `foam_ticks` (0), `garrison_damage_percent` (0). `WarheadType.HitsBurrowed / BonusVsEntrancePercent / FoamTicks / GarrisonDamagePercent` | M4-05 implements all four |
| Upgrade TOML | M4-01 | `tier` (1..3), `effects` (list of effect ids, default []). `UpgradeType.Tier`, `UpgradeType.Effects` (init properties; the positional record is unchanged) | M4-04 applies `effects` in `RulesDeriver` |
| techs.toml | M4-01 (parse) / M4-04 (apply) | Targets add `weapon:<id>`, `warhead:<id>`. Attr whitelist as in §4; unknown attr = load error | |
| game.toml | M4-01 | `[faction.<name>] tier2 = "<id>", tier3 = "<id>"` (`FactionStartKit.Tier2Provides / Tier3Provides`); `[garrison] eject_damage_permille = 200, enter_range_cells = 1` (`SimRules.Garrison` : `GarrisonRules`); `[foam] speed_permille = 500` (`SimRules.Foam` : `FoamRules`) | Absent tables = these defaults |
| Tier sugar | M4-01 (loader) | Type with tier N ≥ 2 gets the faction's tierN provides appended to prerequisites, unless it provides that name itself. `HasPrerequisites` accepts upgrade ids (M4-04) | Data (M4-07) writes `tier`, not tier provides, in prerequisites |
| Aircraft category | M4-04 | `Build` Arg = 2 (Aircraft), Arg2 = vehicle index with `Locomotion.Air`. Vehicle category refuses Air types. Exit from a `produces = "aircraft"` building. Buildable list includes it | M4-07 moves dir_dirigible / new aircraft under it; the sidebar gets an Aircraft tab (M4-09) |
| Map generator API (Rts.Data) | M4-01 stub / M4-02 | `enum Biome : byte { RustBelt = 0, GlassCoast = 1, Mountains = 2, AshPlains = 3 }`; `enum MapSize : byte { Small = 0, Medium = 1, Large = 2 }` (64/96/128); `enum MapSymmetry : byte { None = 0, Rot2 = 1, Rot4 = 2, MirrorX = 3, MirrorY = 4, MirrorDiag = 5 }`; `record MapGenParams(Biome Biome, MapSize Size, int Players, ulong Seed, string? Name = null)`; `MapData MapGenerator.Generate(MapGenParams)`; `static int MapGenerator.CellsFor(MapSize)`; `static (int X, int Y)[] MapSymmetries.Images(MapSymmetry s, int w, int h, int x, int y)` | Legacy `Generate(int, ulong, string?)` unchanged byte for byte. Default name `"<biome>_<s|m|l>_<players>p_<seed>"` |
| Map metadata (Rts.Data) | M4-01 | `MapData` unchanged (sim contract). New `record MapMeta(string Biome, MapSymmetry Symmetry, int MaxPlayers, MapGenParams? Generator, IReadOnlyList<MapFeature> Features)`; `record MapFeature(string Type, CellCoord Cell)`; `MapFile.Save(MapData, MapMeta?, string path)`, `MapFile.Load(string path)` stays, plus `MapFile.LoadWithMeta(string path) → (MapData, MapMeta)`; `MapFile.FormatVersion = 2`, reads 1 | Features are not part of `MapData`, so `MapHasher` is unchanged. Feature placement goes through the setup log (replays reproduce it) |
| Validator (Rts.Data) | M4-01 stub / M4-02 | `enum MapIssueSeverity : byte { Warning = 0, Error = 1 }`; `record MapIssue(MapIssueSeverity Severity, string Code, CellCoord? Cell, string Message)`; `IReadOnlyList<MapIssue> MapValidator.Validate(MapData, MapMeta?, TerrainTable?)`. Codes: `starts_count`, `start_space`, `start_distance`, `connectivity_foot`, `connectivity_tracked`, `feature_cell`, `few_fields`, `symmetry` | The editor (M4-13) and `map validate` use it |
| Features at setup (Rts.Data) | M4-01 stub / M4-15 | `EntityId[] MapFeatures.Place(Simulation sim, MapMeta meta, byte neutralSlot)`: places in list order via `PlaceBuilding`, before tick 0 | M4-11 calls it when the map has features (adds a neutral slot, team 255) |
| Skirmish settings (Game) | M4-01 stub / M4-11 | `enum SlotKind : byte { Closed = 0, Human = 1, Ai = 2 }`; `record SkirmishSlot(SlotKind Kind, Faction Faction, int AiTier, int Colour, int Team)`; `record MapChoice(string? File, MapGenParams? Generated)` (curated = File "rustbelt_m2.map.toml"); `record SkirmishSettings(MapChoice Map, IReadOnlyList<SkirmishSlot> Slots, ulong SimSeed)` with `bool IsSpectator`; `SkirmishGame.Create(string rulesDir, SkirmishSettings s)`, `byte? SkirmishGame.LocalPlayer` | The old 2-player settings map to Slots [Human, Ai] (kept as `SkirmishSettings.Default`) |
| SimHost (Game) | M4-08 | `int Viewer` cycles −1, 0..N−1; `void SetViewer(int)`; `SpeedMultiplier` accepts 1/2/4/8; `bool Spectator` disables `IssueOrder` from input | Stats panel reads `Sim.StatsOf`, `Sim.TierOf` |
| Render hooks (Game) | M4-01 stub / M4-09, called by M4-10 | `GarrisonOverlay.Draw(SpriteBatch, SimSnapshot, Camera)`, `FoamOverlay.Draw(SpriteBatch, Simulation, int viewer, Camera)` in `src/Game/Render/` | SceneRenderer calls both |
| Theater palettes | M4-10 | `data/theaters/<id>.toml` same format plus `[land.shallow]`; `TerrainPalette.Load(string theaterId)`; `rust_belt` is an alias of `rustbelt` | Unknown theater falls back to rustbelt with a warning |
| Biome params | M4-02 | `data/mapgen/biomes.toml`, `[biome.<key>]` with the §1 keys | Not in RulesHash |

## 7 · Items (ids go in ROADMAP)
No two items in the same wave share a file. "Owns" lists files and folders. New files are owned by their item.

| Id | What | Wave | Model | Owns |
|---|---|---|---|---|
| M4-01 | Contracts: every row of §6 as stubs; loaders parse the new keys; RulesHash/Save/Replay/MapFile bumps; state-part skeletons + order test; hook call sites; new LandType in terrain.toml + fixture; D-entry text for the lead | 0 | Opus (lead) | anything needed for §6 |
| M4-02 | Procedural generator: 4 biomes × S/M/L, 2–4 players, symmetry, checks; `MapValidator`; `map gen` / `map validate` | 1 | Opus | src/Data/MapGenerator.cs, src/Data/MapGen/**, src/Data/MapValidator.cs, data/mapgen/**, src/Tools/MapCommands.cs, tests/Sim.Tests/Map/MapGen*Tests.cs, tests/Sim.Tests/Map/MapValidatorTests.cs |
| M4-03 | Garrison sim: enter, fire-out bonus, unload, ejection on death/sale/capture, fog | 1 | Sonnet | src/Sim/Simulation.Garrison.cs, src/Sim/Missions/GarrisonMission.cs, src/Sim/Missions/CaptureMission.cs, tests/Sim.Tests/M4/Garrison*Tests.cs |
| M4-04 | Per-player rules (`RulesDeriver`, `RulesOf` cache), effect application, upgrade-id prerequisites, `TierOf`, Aircraft category | 1 | Opus | src/Sim/Rules/RulesDeriver.cs, src/Sim/Simulation.Players.cs, src/Sim/Simulation.Production.cs, src/Sim/Simulation.Upgrades.cs, src/Sim/Simulation.Hud.cs, tests/Sim.Tests/M4/Rules*Tests.cs, tests/Sim.Tests/M4/Tier*Tests.cs, tests/Sim.Tests/M4/Aircraft*Tests.cs |
| M4-05 | Warhead mechanics: `hits_burrowed`, `bonus_vs_entrance_percent`, `garrison_damage_percent`, Quick-Set Foam (decay, no surfacing or break-through, 50 % speed) | 1 | Sonnet | src/Sim/Simulation.Combat.cs, src/Sim/Simulation.Ground.cs, src/Sim/Simulation.Hardening.cs, src/Sim/Simulation.BurrowMove.cs, src/Sim/Missions/MoveMission.cs, tests/Sim.Tests/M4/Warhead*Tests.cs |
| M4-06 | Stats part: counters and kill attribution | 1 | Haiku | src/Sim/Simulation.Stats.cs, src/Sim/Stats/**, tests/Sim.Tests/M4/Stats*Tests.cs |
| M4-07 | Data: tiers on every type, gate structures, the §4 new types, upgrades and effects, neutral `neu_*` types, game.toml tier provides | 1 | Haiku | data/rules/*.toml |
| M4-08 | Spectator front end: viewer cycling and vision toggle, speed keys, stats panel, game-over stats, input lock | 1 | Sonnet | src/Game/Host/SimHost.cs, src/Game/Ui/Spectator/**, src/Game/Screens/GameScreen.cs, src/Game/Screens/GameOverScreen.cs |
| M4-09 | Game wiring for garrison and tiers: right-click Enter on garrisonable, D = Unload on a building, occupant pips, foam overlay, sidebar tier groups + Aircraft tab, upgrade effect text | 1 | Sonnet | src/Game/Controls/**, src/Game/Ui/Sidebar/**, src/Game/Render/GarrisonOverlay.cs, src/Game/Render/FoamOverlay.cs |
| M4-10 | Theaters: generic palette loading, 3 new palettes + shallow, minimap, overlay calls | 1 | Sonnet | src/Game/Render/TerrainPalette.cs, src/Game/Render/TerrainBake.cs, src/Game/Render/SceneRenderer.cs, src/Game/Ui/Minimap.cs, data/theaters/**, tools/maps/** |
| M4-ART-01..n | Art for the §4 new types (D-088 flow: pilot per faction, then fan-out), neutral ruins, biome terrain textures | 1–3 | Sonnet | data/sprites/** on wp/art-* branches (D-084) |
| M4-11 | Skirmish setup v2: 2–4 slots, AI/Closed/Human, teams, biome/size/seed picker, user maps, spectate start, map file saved next to the replay | 2 | Sonnet | src/Game/Screens/SkirmishSettings.cs, src/Game/Screens/SkirmishSetupScreen.cs, src/Game/Screens/MapPicker.cs, src/Game/Sandbox/SkirmishGame.cs, src/Data/SkirmishStart.cs |
| M4-12 | AI: 2–4 players (target choice, scouting), tier climbing and research, garrisoning Blockhouses/Redoubts/ruins, new units in bags, N-side match runner + `ai matrix --players` | 2 | Sonnet | src/AI/**, src/Tools/AiCommands.cs, src/Tools/MatchCommands.cs, src/Tools/MatchSetup.cs |
| M4-13 | Map editor screen (§5), main menu entry | 2 | Sonnet | src/Game/Screens/Editor/**, src/Game/Screens/MainMenuScreen.cs |
| M4-14 | RulesOf sweep: replace `Setup.Rules.*` reads that must honour upgrades (spawn health, movement speed, sight, tunnels, economy) | 2 | Sonnet | src/Sim/Simulation.cs, src/Sim/Simulation.Actors.cs, src/Sim/Simulation.Movement.cs, src/Sim/Movement/**, src/Sim/Simulation.Economy.cs, src/Sim/Simulation.Tunnels.cs, src/Sim/Vision/** |
| M4-15 | Neutral map features: placement at setup, neutral garrison owner flip and revert, defeat/tier exclusion | 2 | Sonnet | src/Data/MapFeatures.cs, src/Sim/Simulation.Garrison.cs (after M4-03 is merged), src/Sim/Simulation.Production.cs (UpdateDefeat only), tests/Sim.Tests/M4/NeutralGarrison*Tests.cs |
| M4-16 | Integration + balance: tier timings, garrison bonus, foam/mortar vs tunnels, 4-player AI fairness per biome, 8× spectator performance, lore drafts for new types | 3 | lead | — |

Notes:
- M4-15 changes `UpdateDefeat` (in `Simulation.Production.cs`, owned by M4-04 in wave 1) in wave 2. That file is free in
  wave 2.
- M4-01 makes the small cross-cutting edits up front: `ApplyEnter` routing, the `Unload` case in `Apply`,
  `IsHiddenFrom` / `IsUntargetableBy` for Garrisoned, the `OnM3BuildingRemoving` call, `MissionHandlers` registration and
  the hook call sites. That keeps wave 1 free of shared files.

## 8 · Tests and acceptance per item
- **M4-01:** the contract test pins every enum value in §6, the part order (`…effects, garrison, stats`), `RuleCounts.LandTypes == 8`,
  the format constants and the new TOML keys parse (fixture + real data). HashCoverageTests list the new fields (hashed, no
  allow-list). A v1 map still loads. The full suite is green with stubs.
- **M4-02:**
  - Same params give the same map hash, 3 times. A different seed gives a different hash.
  - For each biome × size × players (2, 3, 4): connectivity Foot + Tracked, path-equality check (0 difference for 2 and 4
    players), 5×5 start square, distance ≥ 12, ≥ 2 fields per start.
  - The legacy generator's hashes are unchanged (golden list).
  - The Glass Coast edge ring is Water with Shallow inside it.
  - `map gen` and `map validate` work end to end. Each generation takes < 2 s at Large.
- **M4-03:**
  - Enter fills up to capacity and the 6th soldier is refused. An occupant fires out with range + 1 cell and reload × 0.75.
  - Enemies cannot target or see occupants. Splash on the building does not hurt occupants unless `garrison_damage_percent`
    is set.
  - Unload places soldiers in rings 1–2. Death and sale eject them with 20 % damage, and soldiers with no room are lost.
  - Engineer capture ejects first. A save mid-garrison round-trips. A deterministic replay of a garrison fight passes.
- **M4-04:**
  - An upgrade with `mul max_health` changes `RulesOf(p)` for that player only, not for others or the base. The cache is
    rebuilt after Load with the same hash.
  - A type with an upgrade-id prerequisite unlocks after research. Tier sugar locks T2 types until the gate exists and
    re-locks them when it dies.
  - `TierOf` is 1/2/3. Aircraft queue and leave from the Mooring/Airfield. An Air type in the Vehicle category is refused.
- **M4-05:**
  - The Seismic Mortar damages Burrowed units in its splash and does 2× damage to entrances.
  - Foam cells refuse surfacing and Bore break-through, slow ground units to 50 %, and decay to 0 after `foam_ticks`.
  - All of it is in the hash and round-trips through save.
- **M4-06:** counters match a scripted fight (built, lost, kills credited to the lethal hit's owner; harvested and spent
  totals equal the economy deltas). Save round-trip.
- **M4-07:**
  - RulesValidationTests pass. Every type has a `tier`. The gates provide the tier names.
  - A Recruit AI match per faction on the real data does not stall (headless, 20 min game time).
  - Every new name and number is marked `# ASSUMED (M4-07)` / `(tune)`.
- **M4-08:**
  - A 4-AI spectator match runs. `V` / `F2` change the view without changing the hash. Speed 8× and headless give equal
    hashes.
  - The stats panel equals `StatsOf`. There are no orders from input while spectating.
- **M4-09:** a manual test checklist in docs/USER_CHECKS.md: right-click garrison, D unloads, pips show, foam is drawn, tier
  headings, Aircraft tab.
- **M4-10:** every biome renders and its minimap matches. The rustbelt bake output is byte-identical to before for maps
  without Shallow (existing proof images).
- **M4-11:** the setup screen builds 2/3/4-slot games, spectate and curated. Old default settings give the same sim as today.
  The replay of a generated map verifies with the saved map file.
- **M4-12:**
  - `ai matrix --players 4` on each biome (M size): no AI stalls; slot win rates within 35–65 % at equal tier (tune).
  - The AI reaches tier 3 by 15 min at tier ≥ 3 difficulty, garrisons at least one building when it has one, and stays
    fog-honest (PlayerView only).
- **M4-13:**
  - Paint, symmetry, undo and redo work. Validation shows each issue code on crafted maps.
  - A save → load round-trip gives the same map hash. "Play this map" starts a skirmish.
- **M4-14:** after Flak Vests, a newly built rifleman has the higher health. Speed and sight upgrades act in movement and
  vision. There are no remaining `Setup.Rules` reads of upgradable attrs (a grep-based test with an allow-list).
- **M4-15:** a map with 2 ruins places them as neutral before tick 0 (setup log). Entering flips the owner and emptying
  reverts it. Ruins never count for defeat or tiers. The replay verifies.
- **M4-16:** the owner checklist in §0 works end to end. Balance notes go in docs/balance/m4_balance.md.

## 9 · Open questions for the owner (Q-drafts)
- **Q-M4-a · Three-player fairness.**
  - (A) Rot4 map, 3 of 4 starts used, the empty start becomes a contested expansion, slot assignment rotated by seed.
  - (B) Approximate 3-fold layout from integer polar tables, accepted by a fairness metric (path lengths within ±5 %).
  - (C) No 3-player generated maps.
  - **Rec A:** exact symmetry, simple. The slight positional asymmetry is measured in M4-16.
- **Q-M4-b · Neutral garrisonable ruins in skirmish.**
  - (A) Yes: the generator scatters `neu_*` ruins (Rust Belt, Ash plains), the first occupier takes the building, and it
    reverts when empty.
  - (B) Garrisons only in own Blockhouses/Redoubts.
  - **Rec A:** it is what makes "Rust Belt ruins" a place. Its cost is one wave-2 item (M4-15).
- **Q-M4-c · Authority air depth.**
  - (A) Simple flying units like the dirigible: no fuel or ammo, infinite loiter.
  - (B) C&C-style: aircraft return to the Airfield to rearm after N shots.
  - **Rec A for M4,** B as a later polish item. B needs landing pads and new missions.
- **Q-M4-d · Glass Coast layout.**
  - (A) Ocean on all four edges (peninsula/island), symmetric, every start on a shore.
  - (B) Ocean on one edge, mirror symmetry only, 2 players only.
  - **Rec A:** fair for 4 players and gives M7 shore starts everywhere.
- **Q-M4-e · Losing a tier gate.**
  - (A) The tier re-locks while you have no gate (today's prerequisite rule, classic C&C).
  - (B) A tier stays unlocked once reached (needs a per-player "max tier" in state).
  - **Rec A:** no new state, and it makes gate structures targets.
- **Q-M4-f · Clearing garrisons.**
  - (A) Some warheads (Ashthrower, Collapse Charge, Seismic Mortar) pass a % of building damage to occupants.
  - (B) Occupants are safe until the building dies.
  - **Rec A** with `garrison_damage_percent` (default 0). Otherwise garrisons are too strong against Directorate swarms.
- **Q-M4-g · Spectator stats.** Is the §2 list right? Would you also like a live graph (army value over time) on the
  game-over screen? **Rec:** list as written; graph as a stretch goal in M4-08.
- **Q-M4-h · Where user maps live.**
  - (A) `%APPDATA%/RTS_2027/maps/`, next to settings.toml.
  - (B) `data/maps/user/` in the install folder.
  - **Rec A:** the install folder may be read-only, and it matches Settings.
- **Q-M4-i · New roster.** Are the 13 Authority and 6 Directorate additions in §4 the right set and the right flavour (all
  names working names)? Is anything missing that you want, for example Broadcast (Directorate superweapon idea 2: turns
  enemy infantry)? **Rec:** approve the list. Broadcast needs a mind-control mechanic, so leave it for M5+.
- **Q-M4-j · Health upgrades on existing units.**
  - (A) Current HP is kept and only the max rises.
  - (B) Current HP scales by the same factor.
  - **Rec B:** it feels like the upgrade "did something". It is one integer multiply per actor at research time,
    deterministic in slot order. If chosen, M4-04 adds this.

## 10 · Risks the lead should watch
- `RulesOf` per player touches every type lookup. There are about 73 `Setup.Rules.*` reads in src/Sim, and M4-14 sweeps
  them. Any read left behind silently ignores upgrades. The grep test in M4-14 guards this.
- 64-bit upgrade mask: two factions use 15 now. Four factions at about 15 each is still under 64, but M6/M7 must budget.
- SaveVersion/RulesHash bumps invalidate session-4 replays and saves. That is expected and covered by the D-entry.
- 8× speed with 4 AIs on a 128×128 map may not hold 160 ticks/s on the owner's PC. The cap keeps it deterministic but
  slower. M4-16 measures it.
- The rustbelt proof-image bake (tools/maps) must stay byte-identical. M4-10 must not reorder palette land rows. Shallow
  is appended.
