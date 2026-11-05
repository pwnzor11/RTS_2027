# Design 13 · Supply cap (M7-SUP, D-125, D-129)

Status: lead design, session 9. Mechanic confirmed (D-125, on by default in skirmish per D-129). Buildings and every number are
assumed — confirm; numbers are `(tune)`. Code facts were checked in this worktree (file:line given).

## 1 · Goal
Every unit costs supply. Each faction raises its cap with its main base and a cheap supply building, up to a hard ceiling.
At the cap, production pauses with a clear message. This replaces the flat unit cap (the Expedition's 40-unit idea, 07 §2.3).
There is no flat cap in the sim today (grep for unit caps in src/Sim finds none), so nothing is removed.

## 2 · Mechanics
### 2.1 Supply per unit
Rule: derive from `cost`, with a per-type override `supply` in TOML. Bands `(tune)`: cost 0-400 = 1, 401-900 = 2, above 900 = 3.
Today that gives: riflemen, conscripts, jeeps = 1; rocketeers 300 = 1; scrapers, tanks, mortars, dirigibles 600-900 = 2;
reactor crawler 1 500 and land train 2 000 = 3 (data/rules/vehicles.toml, infantry.toml `cost`).
- **Aircraft count** (they are vehicles with `locomotion = "air"`, Simulation.Production.cs:38). Band rule, no discount.
- **Harvesters count**, override `supply = 1` `(tune)`. Reason: a 3-harvester economy must not eat 6 of the cap, and the AI
  already rebuilds harvesters (`ReplaceHarvesters`).
- Expedition units cost more per unit, so the bands give them 2-3 each: that is the "few but elite" lever without a flat cap.
- Buildings, walls, projectiles: 0.

### 2.2 Cap sources
`cap = min(ceiling, sum of supply_provided over the owner's completed, unpacked, alive buildings)`.
- Main base building (conyard / shaft / Mother Rig / depot): `supply_provided = 20` `(tune)`.
- Supply building: +10 each `(tune)`. Ceiling 200 `(tune)`, so about 18 supply buildings is the maximum.
- A captured building counts for the new owner. A sold or dead one stops counting at once.

### 2.3 Which building (all assumed — confirm)
| Faction | Building | Footprint | Cost | Power | Tier | Prereq | Why |
|---|---|---|---|---|---|---|---|
| Authority | **new** `auth_ration_hall` | 2x2 | 200 | -10 | 1 | `auth_power` | Everyone is counted and owed a ration (authority.md l.16); the barracks bunks (structures.md l.37) house the draft, but barracks are producers and should not double as housing. A ration hall is the ledger made brick. |
| Directorate | **new** `dir_bunk_gallery` | 2x2 | 150 | -10 | 1 | `dir_smelter_vent` | Bunker culture, "they sleep behind blast doors" (directorate.md l.12). Alternative: Air Scrubber (D-125 names both); a gallery is cheaper to draw. Follows the `dir_cistern` pattern (same prereq, same footprint). |
| Left Behind | **new** `lb_still_rig` (Water Still Rig) | 2x2 | 200 | 0 | 1 | `lb_mother_rig` | Caravans live on clean water; a still is the heart of a camp. It is a rig: `packs_into` a vehicle, so a packed still gives 0 (design 06 §2.1; rigs 10_m6_plan §3.2). |
| Expedition | **new** `exp_provision_store` | 1x2 | 150 | 0 | 1 | `exp_landing_depot` | A crate store of tinned rations from the shipments (expedition.md l.251). Placeable anywhere in the beachhead zone (07 §2.1), never a quay. |

No existing building fits without a gameplay clash, so each faction gets one new cheap building. Cost and footprint are `(tune)`.
**Expedition stocking** (assumed — confirm): a store gives +10 while "stocked", +5 when not. It is stocked if a shipment arrived in
the last 3 shipment intervals. So raiding the convoy (D-126) shrinks the cap slowly. Derived from the "shipments" state part's
last-arrival tick (11_m7_plan §2.4); no extra state. Option B: flat +10, no link. Recommendation: A.

### 2.4 Rules when over the cap
- No unit ever dies or is refused by the cap. Losing a supply building only blocks production until `used <= cap` again.
- **Enqueue:** a Build for a unit is rejected (new `QueueBlockReason.SupplyCap = 6`, appended after QueueFull = 5,
  PlayerHud.cs:14-28) when `used + supply of every queued unit + this unit > cap`. Structures and upgrades are never blocked.
- **Queued items:** if the cap drops, the front item pauses: no spending, no progress, no refund. The item resumes when
  `used + its supply <= cap`. A finished unit waits in the lane (like a blocked exit, Stalled, Simulation.Production.cs:318) until it fits.
- **Counts:** all live infantry and vehicle actors of the owner, including passengers (layer Passenger), garrisoned
  (Garrisoned), underground (Tunnel/Burrowed), diving and packed rigs (they are vehicles). Layers: src/Sim/Core/ActorLayer.cs:16.
- **Not blocked:** start kit, `FreeUnit` spawns (Simulation.Production.cs:197), mission and debug spawns, unpack/deploy.
  They count but may push `used` over the cap.
- Packing: a rig that starts packing stops giving supply at once (see 10_m6_plan §3.3); the packed vehicle counts its own `supply`
  (`lb_mother_rig` = 0 `(tune)`, since the base amount comes from the unpacked building).

## 3 · Determinism and state
Prefer **no new state part**. `used` and `cap` are derived each tick from live actors and buildings by an integer scan in slot
order (like `PassengerCount`, Simulation.Transport.cs:26). They go in a per-tick scratch cache (`_supplyUsed[p]`, `_supplyCap[p]`,
stamped with the tick) and are used by `QueueBlockReason`, `AdvanceLane` and the HUD. Why this is enough: every input is already
in a hashed part (units, infantry, buildings, players, production, tunnels), so the derived result is a pure function of the
hashed state. The scratch fields are allow-listed in HashCoverageTests.cs:190 with that reason. Save/replay format is unchanged by
this feature; it rides on the M7 bump to 11 (14_veterancy §4). The rules hash gains a schema block (RulesHash.cs) for the new keys.
Cost: one pass over slots per player per tick (a few thousand entries), tick-stamped so it runs once.

## 4 · Rules fields (all default "off")
| Where | Key | Default | Data value | Meaning |
|---|---|---|---|---|
| game.toml `[supply]` | `enabled` | false | true `(tune)` | master switch; old tests unchanged |
| `[supply]` | `ceiling` | 200 | 200 | hard cap |
| `[supply]` | `band1_max` / `band2_max` | 400 / 900 | same | cost band edges |
| `[supply]` | `band_values` | [1,2,3] | same | supply per band |
| vehicle, infantry | `supply` | -1 (use bands) | override | per type |
| building | `supply_provided` | 0 | 20 / 10 / 0 | cap contribution |
| building | `supply_unstocked` | = provided | 5 (store) | Expedition store when not stocked |
| `[supply]` | `stock_intervals` | 3 | 3 | shipment gap before "unstocked" |
Tests that load the real data (RulesValidationTests, SaveTests, ReplayTests, M6ContractTests) must set `enabled = false` in
their fixture or use a data override; the first package checks each one.

## 5 · View and UI
- `PlayerView`/HUD gains `Supply(used, cap, ceiling)` (src/Sim/Core/PlayerHud.cs). The sidebar (src/Game/Ui/Sidebar/, SidebarPanel,
  SidebarMetrics) shows `used/cap` beside power. Text turns amber at `cap - 4`, red at the cap.
- Greyed cameo tooltip (BlockReasonText.cs:11): `SupplyCap` => "Supply cap reached. Build more {supplyBuilding}." The placeholder
  `{supplyBuilding}` is filled from the faction's building display name (keyed string, not hard-coded).
- Paused queue head: the cameo shows "Waiting for supply" (key `ui.supply.waiting`). Enemy supply is not shown.

## 6 · AI
The AI reads only the view (D-051). New rule in ProductionManager / BuildOrderManager (src/AI/Recruit): when
`used + queued supply >= cap - margin` and `cap < ceiling` and no supply building is queued or placing, queue one. It goes above
army orders. Margin by tier 1..5 = 4, 6, 8, 10, 12 `(tune)`; higher tiers build earlier. Faction info (building id) goes in
RecruitFaction like `Tier2GateId`. Expedition AI places stores inside its beachhead; Left Behind AI never packs a still while
`used + queued >= cap - margin`.

## 7 · Balance plan
Measure with `ai match`, all 4 factions, every map size, 20 games per pair `(tune)`: before (`enabled=false`) and after.
Table per faction: win rate vs mean, median army supply at 5 and 10 min, minutes spent capped, cap-blocked ticks, supply buildings
built, income. Target: no faction capped for more than 10 % of minutes 5-15 `(tune)`; win-rate spread unchanged within 5 points.

## 8 · Contracts
| Contract | Value |
|---|---|
| `QueueBlockReason.SupplyCap` | 6 (append) |
| `SupplyView` | record (Used, Cap, Ceiling) on the HUD/PlayerView |
| `Simulation.SupplyUsed(p)`, `SupplyCap(p)` | public, derived |
| Rules | section 4 |

## 9 · Packages
| Id | What | Owns | Depends on |
|---|---|---|---|
| SUP-1 | Rules + loader + rules hash, derived scan, block reason, AdvanceLane pause, tests | src/Sim (Simulation.Supply.cs, Rules), src/Data | — |
| SUP-2 | Data: Authority + Directorate buildings, `supply_provided` on conyards, game.toml | data/rules (auth_/dir_) | SUP-1 |
| SUP-3 | Data: Left Behind still rig + Expedition store (after M6-08 / M7-08) | data/rules (lb_/exp_) | SUP-1, M6-08 |
| SUP-4 | Game layer: counter, tooltip, waiting label, placeholder cameo | src/Game/Ui/Sidebar | SUP-1 |
| SUP-5 | AI supply rule, margins, tests | src/AI/Recruit | SUP-1, SUP-2 |
| SUP-6 | Balance table before/after | docs/balance | all |

## 10 · Acceptance
- With `enabled = false` every existing test and golden replay is unchanged.
- With it on: a lane stops at the cap, resumes after a supply building is built; destroying one kills no unit.
- AI at all five tiers never sits capped for long (section 7); save/load mid-pause round-trips.
- Open: Expedition stocking (A or B), Directorate building (gallery or scrubber), harvester override.
