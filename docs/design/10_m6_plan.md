# M6 plan · The Left Behind (D-087, D-102)

Status: lead plan, session 8. Design and owner answers: docs/design/06_left_behind.md (§10 recommendations confirmed in D-102).
Every number below is a starting value marked `(tune)` in TOML; balance numbers players see follow D-113 (measured, then shown to
the owner as a table before they are treated as final). Lore stays DRAFT until the owner approves it (D-086).

## 1 · Goal and what the player can try
- Skirmish: pick **The Left Behind** for any slot (human or AI). Start kit: a packed Mother Rig, 2 Scavengers, 1 Gleaner.
- Pack and unpack rigs, tow a packed rig with a Tow Truck, salvage wrecks for cash, steal ash with Hook Runners, hide
  Veilwalkers in ash, lay dust with Dust Sails, repair with Tinkers, research cheap upgrades, fire The Second Drift.
- The built-in AI plays the faction as the Raider Warlord at all five tiers; an LLM commander can lead it (caravan elder).

## 2 · Determinism and the save format
All rules are integer and tick based (D-051); no new randomness. New state goes into **two appended parts** after
"directive": `"rigs"` (pack state per building and per packed actor) and `"salvage"` (wrecks). The Second Drift storm and the
raider withdraw flag go into the existing "effects" part as a version-2 layout (v1 still reads). Save and replay format
**10** (older saves stop loading, as before). Every new field is hashed or allow-listed with a reason in HashCoverageTests.

## 3 · Contracts (M6-02, lead; helpers build on these and do not change them without the lead)
### 3.1 Enums (append only)
| Enum | New value |
|---|---|
| `Faction` | `LeftBehind = 3` (after Neutral = 2; Neutral keeps its value) |
| `CommandKind` | `Pack = 24`: Subject = a rig building (pack) or a packed rig vehicle (unpack, Cell = top-left target) |
| `MissionKind` | `Salvage = 16` (drain a wreck), `Pack = 17` (packing or unpacking in progress), `Withdraw = 18` (raider leaving) |

### 3.2 Rules fields (TOML; all default to "off")
| Type | Field | Meaning |
|---|---|---|
| building | `packs_into` (vehicle id) / `pack_ticks` | rig: Pack turns it into that vehicle over pack_ticks |
| vehicle | `unpacks_into` (building id) / `pack_ticks` | packed rig: Pack on a free footprint turns it back |
| vehicle, infantry | `salvage_rate` | ash per 10 ticks drained from a wreck (0 = cannot salvage) |
| vehicle, infantry | `raider` | Enter on an enemy Silo/Refinery steals ash; kills of harvesters pay spoils |
| vehicle, infantry | `raider_one_use` | consumed on a successful plunder (default false: withdraws) |
| infantry | `ash_stealth_delay_ticks` / `ash_stealth_threshold` | shorter stealth delay on cells with ash above the threshold |
| vehicle | `drops_blackout_ticks` | lays Blackout on its own cell while moving (Dust Sail) |
| vehicle, infantry | `aura_repair_permille` | repair aura (per 10 ticks, permille of max HP) on own vehicles/rigs in aura_radius |
| vehicle | `tow_bonus_permille` | speed aura that applies only to packed rigs (Tow Truck) |
| economy (game.toml) | `wreck_ticks` 1200, `salvage_percent` 25, `plunder_percent` 20, `plunder_cap`, `spoils_permille` | |
| upgrade | `pack_ticks_permille`, `salvage_permille`, `plunder_permille`, `repairs_vehicles_at_range` | effect hooks for the six upgrades |
| superweapon | `storm` effect: radius, ticks, contaminate step and cap (64), speed permille on enemy vehicles | The Second Drift |

The wreck value of a type is `cost × salvage_percent / 100`, floored at 25 and capped at 1 000 (tune); it decays linearly to 0
over wreck_ticks. Wrecks do not block movement.

### 3.3 State parts
- `"rigs"` (RigState): per building slot `packTicksLeft` (0 = idle; >0 packing); per vehicle slot `packedFrom` (building type
  index + 1, 0 = not a packed rig), `hpPermille` carried over, `unpackTicksLeft`, and the paused production queue handle. A packed
  rig keeps its queue: production of that building pauses (D-102 Q-A, option 1) and resumes on unpack.
- `"salvage"` (SalvageState): fixed-capacity wreck table (cap from limits, default 256), each entry `cell`, `value`,
  `valueStart`, `ticksLeft`. Expiry and draining run in slot order; a new wreck takes the lowest free slot; when full the
  oldest wreck is replaced. Visible to all players (D-102).
- "effects" v2 adds: `storms` (centre cell, radius, ticks left, owner) and a per-infantry and per-vehicle `withdraw` flag.

### 3.4 View and AI hooks
`PlayerView` gains `Wrecks` (all wrecks; they are public), `IsPackedRig`, and pack progress for own actors. The AI reads only the
view (D-051). Game layer: a Pack button and hotkey, wreck sprites, storm overlay, faction pick in skirmish setup.

## 4 · Packages
| Id | What | Owns (main files) | Depends on |
|---|---|---|---|
| M6-02 | Contracts above: enums, rules fields parsed with defaults, empty "rigs" and "salvage" parts registered and hashed, effects v2 layout, save/replay format 10, part-order and hash-coverage tests | src/Sim Core, Rules, new Rigs/ and Salvage/ folders, Simulation.cs | — |
| M6-03 | Rig pack/unpack: Pack command, Pack mission, footprint clear and re-validate, HP ratio, power off while packed, production paused and kept | Simulation.Rigs.cs, Rigs/ | M6-02 |
| M6-04 | Wrecks and salvage: spawn on death (vehicles and buildings, any team), decay, Salvage mission, hopper docking at Yard and refinery rigs | Simulation.Salvage.cs, Salvage/ | M6-02 |
| M6-05 | Raiders: plunder on Enter, withdraw, spoils on harvester kills | Simulation.Raid.cs | M6-02 |
| M6-06 | Ash stealth, Blackout trail, repair aura, tow aura, upgrade hooks on built units | Simulation.Effects.cs (aura recompute), Simulation.Upgrades.cs | M6-02 |
| M6-07 | The Second Drift storm | Simulation.Superweapon.cs | M6-02 |
| M6-08 | Faction data: roster, structures, upgrades, tech tiers, start kit (design §3–5 values, `(tune)`) | data/rules/*.toml (lb_ sections), FactionStartKit | M6-02 |
| M6-09 | Game layer: faction pick, sidebar, Pack button, wreck and storm rendering, placeholder sprites | src/Game | M6-03..08 |
| M6-10 | AI Raider Warlord: relocate planner, salvage scheduling, raid targeting, five tiers, LLM persona hooks | src/AI (after M4-16 lands) | M6-03..08 |
| M6-11 | Art: one pilot (Mother Rig, packed and unpacked) for owner approval, then fan-out | tools/art, data/sprites | owner approval |
| M6-12 | Acceptance: AI vs AI soak across the three factions, golden replays, balance table to the owner | tests, docs/balance | all |

Waves: wave 1 = M6-02 (lead-reviewed). Wave 2 = M6-03..08 in parallel worktrees from the integration branch `integ-m6`.
Wave 3 = M6-09 and M6-10; the art pilot can start any time after M6-08 (names and footprints). M6-10 waits for M4-16 to land
so the two AI changes do not collide.

## 5 · Acceptance
- A Left Behind player can win a skirmish against each tier of both other factions using only faction features.
- Pack, unpack, salvage, plunder, storm and save/load in the middle of each replay exactly (golden replays).
- AI vs AI soak (all faction pairs, 5 tiers) finishes with no desync and no stuck rig.
- Full suite green on Linux and Windows CI.
