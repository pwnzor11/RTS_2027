# M7 plan · The Overseas Expedition (D-087, D-124..D-129)

Status: lead plan, session 9. Design: docs/design/07_expedition.md (§10 answered by D-124..D-127; §6.1 and §6.5 are partly out of
date, see 2.1). Prerequisites: supply cap (13_supply_cap.md, D-125/D-129) and veterancy (14_veterancy.md, D-127). Every number is
`(tune)`; every new building, persona or lore choice is "assumed — confirm"; balance numbers follow D-113/D-123 (measured, then shown
to the owner). Lore: docs/lore/expedition.md (leaders and institutions invented, D-087).

## 1 · Goal and what the player can try
- Skirmish: pick **The Overseas Expedition** (human or AI) on any map with a landing coast. Start kit: Command Ship at sea,
  1 Landing Craft, 3 Marines, 1 Skimmer (07 §2.1).
- Sail, deploy the Command Ship into a Landing Depot on a coast, grow a beachhead zone with beacons, land troops from boats and
  hovercraft, hide submarines, bombard from the sea, run a supply line with home shipments (which shrink), fire the Salvo Array.
- The built-in AI plays the faction (07 §7); an LLM commander can lead it. 2-4 leader personas are written in M7-10 (D-128).

## 2 · Contracts
### 2.1 What the code already has (checked)
`Locomotion.Hover = 3` exists; only Naval is missing (src/Sim/Rules/Enums.cs:6). `LandType.Water` already means deep and `Shallow = 7`
exists; `Coast = 8` is reserved (Enums.cs:12-13), so the "water alias, no migration" answer of D-124 is already true. Shallow speeds
are in data/rules/terrain.toml (foot 40, vehicles 0, hover 100). `CommandKind.Unload = 22` exists and its comment reserves the shore
cell for M7 (Command.cs:13-15), so **no new CommandKind is needed**. `Faction` ends at LeftBehind = 3 (Enums.cs:24); `PlayableFactions`
is 2 (Enums.cs:47, M6-09 raises it). `WeaponTargets` is Ground / Air / Both (Enums.cs:32). Map file format is 2 (MapFile.cs:22).

### 2.2 Enums and format (append only)
| Enum | New value |
|---|---|
| `Faction` | `Expedition = 4` |
| `Locomotion` | `Naval = 6` (Hover stays 3) |
| `LandType` | `Coast = 8` (beach; `landing = true` in terrain.toml) |
| `ArmorClass` | `ShipLight = 5`, `ShipHeavy = 6`, `Submerged = 7` (warhead versus lists pad with 100) |
| `WeaponTargets` | `Ground` (0) now means land **and surface ships** (D-124 default, no data change); append `Naval = 3`, `Submerged = 4`, `NavalAndSubmerged = 5` |
| `MissionKind` | `Unload = 19`, `Submerge = 20`, `Sortie = 21`, `ConvoySail = 22` (Embark reuses Enter; lead fixes numbers in M7-02) |
| `CommandKind` | none. `Unload` (shore cell), `Deploy` (Command Ship; submarine dive toggle), `Special` (Salvo Array) are reused |
| Save and replay format | **12** (veterancy took 11). Map file format 2 -> 3 (Coast cells, `convoy_edge` and `landing` markers); version 1-2 still load |

### 2.3 Rules fields (TOML; all default "off")
| Type | Field | Meaning |
|---|---|---|
| vehicle | `locomotion = "naval"`, `draw_scale_permille` | ship; one-cell body drawn larger (D-124, 1000 = a cell) |
| vehicle | `deep_only` | cannot enter Shallow (large ships) |
| vehicle | `submerges`, `sonar_radius` | dive toggle; detection radius of pickets and destroyers |
| vehicle | `aircraft_slots`, `sortie_ticks` | carrier hangar |
| vehicle | `unload_ticks` | landing craft: ticks per passenger onto the shore |
| weapon | `targets` | now also `naval`, `submerged`, `naval_submerged` |
| building | `water_building` + footprint `water` mask | quay, shipyard, anchorage, drydock straddle the shoreline |
| building | `beachhead_source`, `beachhead_radius` | depot 10, quay and shipyard 8, beacon 6 cells `(tune)` |
| `[shipments]` (game.toml) | `interval_ticks` 1800, `value` 1500, `decay_permille` 40, `floor_permille` 350, `charter_ticks` 1200, `tenders_max` 3 | home supply line (07 §2.2) |
| `[shipments]` | `local_ash_bonus_permille` | knob for the balance section (§4) |
| terrain | `[speed.coast]`, naval column on every land | deep 100, shallow 100 (not `deep_only`), coast 0 |

### 2.4 State parts (append-only, after "veterancy")
- `"shipments"`: per player timer, decay step, charter progress, in-flight tenders (id, cell, payload). Spawn cell: deterministic
  index into the map's ocean-edge cells; no SimRandom. (Named "shipments" so it is not confused with the supply cap, which has no
  state, 13 §3.)
- `"beachhead"`: per-player zone mask, recomputed in cell-index order when a source building appears or dies (like hardening, D-076).
- `"naval"`: region labels per locomotion class, unload queues, submerged flags. Lead's call to merge into "ground" if small;
  recommendation: separate part, same pattern as "rigs".
Every new field is hashed or allow-listed with a reason in HashCoverageTests (HashCoverageTests.cs:190).

### 2.5 Mechanics contracts
- **Terrain split / water:** nothing to migrate (2.1). Structures may not stand on Water or Shallow except `water_building` types.
- **Pathing:** per-locomotion cost grid and connected-region labels (flood fill in index order); unreachable goals refused at once.
- **Ships:** one-cell occupancy, drawn at `draw_scale_permille` (D-124). The approach is studied in the GPL reference source and written
  from scratch (D-050). No new inertia; reuse vehicle movement with a turn rate.
- **Landing and unload:** `Unload` with a shore cell; passengers leave one per `unload_ticks` onto the nearest free cells. Vehicles
  land only on `landing` cells (Coast, Shallow beach). Hover and Skyhook bypass it.
- **Submerge and sonar:** reuse the burrow/seismic machinery (Simulation.Burrow.cs): `submerges` replaces the hidden layer,
  `sonar_radius` replaces seismic detection. Submarines: missile sub and minelayer both kept (D-124).
- **Beachhead zone:** placement for Expedition structures requires `cell distance <= beachhead_radius` of a live source; losing a source
  only blocks new placement. Stores of the supply cap use this zone (13 §2.3).
- **Supply line:** shipment timer per player; tenders sail from the map edge to an Anchorage; lump `value x (1000 - decay) / 1000`, never
  below the floor; each held pit or ash field for `charter_ticks` resets one decay step. Tenders are unarmed and can be sunk at sea:
  credits lost (D-126). Shipments **shrink** over time so the faction must build local income.
- **Landlocked rule (D-124):** the generator guarantees a landing coast for an Expedition start; a hand-made map without one disallows
  the faction in setup (SkirmishSettings validation, SkirmishSettings.cs:155; MapValidator warns). No air-supply fallback: drop
  `exp_airhead` and `exp_skyfreighter` from the roster.
- **Superweapon:** Salvo Array via the existing superweapon path (Simulation.Superweapon.cs); The Long Tide is postponed (D-124).

### 2.6 View and AI hooks
`PlayerView` gains own beachhead mask, shipment ETA and decay, own tenders, submerged flag (visible only when detected), naval
regions for AI reachability. Game layer: faction pick, ship sprites with draw scale, beacon placement ghost, shipment HUD timer, landing
alert cue. The AI reads only the view (D-051).

## 3 · Packages
| Id | What | Owns (main files) | Depends on |
|---|---|---|---|
| SUP-1..6 | Supply cap (13_supply_cap.md) | see 13 §9 | — |
| VET-1..7 | Veterancy (14_veterancy.md) | see 14 §9 | — |
| M7-02 | Contracts above: enums, rules parsed with defaults, empty parts, format 12, map format 3, order and hash tests | src/Sim Core, Rules, Serialization | VET-1 |
| M7-03 | Ships and pathing: Naval locomotion, regions, draw scale, occupancy | Simulation.Naval.cs, Pathing | M7-02 |
| M7-04 | Landing and unload, Skyhook and landing craft | Simulation.Landing.cs | M7-03 |
| M7-05 | Submerge and sonar | Simulation.Submerge.cs | M7-03 |
| M7-06 | Supply line: shipments, tenders, decay, charter | Simulation.Shipments.cs | M7-02 |
| M7-07 | Beachhead zone and beacons | Simulation.Beachhead.cs | M7-02 |
| M7-08 | Data: roster, structures, upgrades, tiers, start kit, `exp_provision_store` (SUP-3), targets/armour tables | data/rules (exp_), FactionStartKit | M7-02 |
| M7-09 | Carrier and air (sorties, slots) | Simulation.Carrier.cs | M7-03, M7-08 |
| M7-10 | AI Expedition (establish, convoy, strike, withdraw), personas, Archive entries | src/AI/Recruit, data/archive | M7-03..08, SUP-5, VET-6 |
| M7-11 | Art: pilots (Glide Tank, Landing Depot) then fan-out | tools/art, data/sprites | M7-08 |
| M7-12 | Salvo Array | Simulation.Superweapon.cs | M7-02 |
| M7-13 | Maps and generator: ocean edge, landing coast guarantee, `convoy_edge`, editor water brushes | src/Data/MapGenerator.cs, MapValidator | M7-02 |
| M7-14 | Game layer + balance and acceptance (§6) | src/Game, docs/balance | all |

Waves: **wave 0** = SUP-1 and VET-1 (small, independent). **Wave 1** = M7-02 (lead-reviewed, after VET-1 so formats do not
collide). **Wave 2** = M7-03, 06, 07, 08, 12, 13 in parallel worktrees from `integ-m7`. **Wave 3** = M7-04, 05, 09, game layer, art.
**Wave 4** = M7-10 (after M6-10 and SUP-5, so the AI changes do not collide), then M7-14. M7 does not wait for M6 art.

## 4 · Early-game income balance target (D-126)
Goal: the Expedition is neither starved nor ahead in the opening. Measure **income per minute** (credits harvested plus shipments
paid, from the stats counters, Simulation.Stats.cs) over **0-3, 0-5 and 0-10 minutes**, AI vs AI, for **all four factions**, on
**every map size** (small, medium, large), 20 games per cell `(tune)`, same seeds for all factions, tier-2 AI.
- **Target band:** Expedition within **±10 %** of the mean of the other three factions at 3, 5 and 10 minutes `(tune)`. The
  other three factions should also stay within ±20 % of each other (a sanity check, not a change request).
- **Knobs, in order:** `[shipments] value`, `interval_ticks`, `decay_permille`, `floor_permille`; then the skimmer capacity and local
  ash value (`local_ash_bonus_permille`); then the start cash. Never touch the other three factions' economy without the owner (D-113).
- **Table format** (one per map size; docs/balance/m7_income.md):
| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games |
|---|---|---|---|---|---|---|
| Authority | | | | | n/a | |
| Directorate | | | | | n/a | |
| Left Behind | | | | | n/a | |
| Expedition | | | | +x % / +y % / +z % | | |
Also log: first-refinery tick, tenders sunk, decay step reached at 10 min. Pass: all nine Expedition cells inside the band.

## 5 · Risks
- Naval pathing and landing are the biggest items (M7-03, 04); cut `exp_missile_sub` and `exp_minelayer` last (D-124 keeps both).
- AI on water maps: the Recruit framework has no region logic yet; M7-10 needs M7-03 regions in the view.
- Supply cap and shipments interact (13 §2.3 stocking); measure with both on.

## 6 · Acceptance
- An Expedition player can win a skirmish against each tier of the other three factions using only faction features, on a map with a coast.
- Landing, unload, submerge, shipment, tender sinking, beacon placement and the Salvo Array save/load mid-replay exactly (golden replays).
- AI vs AI soak (all faction pairs, 5 tiers, all map sizes) has no desync, no stuck ship, no unreachable-goal spam.
- The income table (§4) passes; supply and veterancy tables are attached.
- A hand-made map with no landing coast refuses the Expedition; the generator always gives one.
- Full suite green on Linux and Windows CI.
