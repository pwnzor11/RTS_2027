# Design 14 · Generic veterancy (M7-VET, D-127)

Status: lead design, session 9. Mechanic confirmed (D-127: same 3 ranks for every faction, save/replay-safe, rank 3 must matter as in
Red Alert). Thresholds and bonuses are `(tune)` and assumed — confirm. Code facts were checked in this worktree.

## 1 · Goal
Units that kill get better. Three ranks: **Veteran**, **Elite**, **Hero** (names assumed — confirm). Rank 3 is worth about two
units of the same type. No veterancy exists in the sim today (the word only appears in AI difficulty names, e.g. AiSide.cs:10).

## 2 · Mechanics
### 2.1 What ranks
Vehicles, infantry and aircraft (aircraft are vehicles with `locomotion = "air"`). Buildings never rank. A type ranks if it has a
weapon: new TOML `veteran` (default true when `primary_weapon` is set, false otherwise). Harvesters, engineers and unarmed
transports therefore stay at rank 0.

### 2.2 Kill value and thresholds
Each kill adds `victim cost x kill_permille / 1000` to the shooter's **kill value** (integer). `kill_permille` `(tune)`: units 1000,
buildings 500 (so base-busting helps a little). Cost is the victim type's rules cost (not the discounted price paid).
| Rank | Name | Kill value needed `(tune)` | Meaning today |
|---|---|---|---|
| 0 | Recruit | 0 | |
| 1 | Veteran | 600 | about one tank (800) or two jeeps |
| 2 | Elite | 1 600 | about two tanks |
| 3 | Hero | 3 200 | about four tanks |
The rank is **derived** from kill value and the threshold table; only kill value is stored. The upgrade `exp_veteran_drill`
(07 §5 upgrades) scales thresholds by an effect `vet_threshold_permille` (mul; 800 `(tune)`); a lower threshold only ever raises rank.

### 2.3 Bonuses (permille of base; reload and damage taken: lower is better)
| Rank | Damage dealt | Damage taken | Reload time | Speed | Self-heal |
|---|---|---|---|---|---|
| 1 Veteran | 1100 | 950 | 950 | 1000 | none |
| 2 Elite | 1200 | 900 | 900 | 1050 | none |
| 3 Hero | 1300 | 800 | 800 | 1100 | 1 % of max HP every 20 ticks (1 s) |
All `(tune)`. Rank 3 is about 1.3 / 0.8 = 1.6x damage output and 1.25x toughness, so roughly 2x combat value, plus it recovers
from a fight alone (about 100 s to full). Rank 3 costs the same supply as rank 0 (13_supply_cap), so it also saves cap.
Armour is a damage-taken factor, not a max-HP change, so promotion never needs a health fix-up. Self-heal does not run for a
unit that fired or was hit in the last 5 s `(tune)`; it uses the tick and slot (no stored timer), see section 4.

### 2.4 Kill credit (verified)
Today a kill is attributed to a **player**, not an actor: `PendingHit(Target, Amount, Source)` with `Source` the owner byte
(Simulation.Combat.cs:53); `ApplyPendingDamage` calls `OnLethalHit(target, (byte)h.Source)` when health crosses to <= 0
(Combat.cs:300); `OnLethalHit` increments `_stats.Kills[source]` for enemy victims only (Simulation.Stats.cs:16-24). Projectiles carry
only the owner byte (Combat.cs:239, 476-479 `Detonate(owner, ...)`). So veterancy needs the **shooter actor** carried through:
`Fire(kind, i, ...)` (Combat.cs:224) is the single place with the shooter's identity; add an `ActorRef shooter` to `Detonate`,
the projectile store (src/Sim/Combat/ProjectileStore.cs: one column) and `PendingHit`/`Hurt`/`QueueHit`. The unit that lands the
lethal hit gets the credit (not splash neighbours, not assists). Rules:
- Credit only for an enemy victim (same test as OnLethalHit). No credit for friendly or neutral victims.
- Shooter dead or its slot reused before impact (the EntityId generation no longer matches): no credit.
- Death weapons (Effects.cs:119), superweapons (Superweapon.cs:74), radiation and storms have no shooter: no credit.
- **Garrison:** occupants fire through `Fire(Infantry, i, ...)` (Simulation.Garrison.cs:149), so each occupant earns its own value
  and keeps its rank when it leaves. **Passengers** do not fire and earn nothing; a carrier gets no credit for them.
- Rank persists through loading, tunnels, burrowing and packing (same actor). It is lost when the actor becomes a building
  (deploy) or is replaced by another type.

### 2.5 Expedition
07 §2.3 says "3 levels, faster on kills", and lists `exp_veteran_drill`. D-127 says one generic system. **Assumed — confirm:**
no faster gain for the Expedition; only the `exp_veteran_drill` upgrade lowers its thresholds (to 80 %). Option B: a faction
`kill_permille` bonus of 1250 for the Expedition. Recommendation: A; check in the balance run.

## 3 · Rules fields (all default "off")
| Where | Key | Default | Data value |
|---|---|---|---|
| game.toml `[veterancy]` | `enabled` | false | true `(tune)` |
| `[veterancy]` | `thresholds` | [600,1600,3200] | same |
| `[veterancy]` | `kill_permille_unit` / `_building` | 1000 / 500 | same |
| `[veterancy]` | `damage_permille`, `taken_permille`, `reload_permille`, `speed_permille` | 3 entries each, 1000 | table in 2.3 |
| `[veterancy]` | `heal_interval_ticks` / `heal_permille` | 20 / [0,0,10] | same |
| `[veterancy]` | `heal_quiet_ticks` | 100 | same |
| vehicle, infantry | `veteran` | true if armed | per type |
| effect attr | `vet_threshold_permille` | 1000 | 800 (exp_veteran_drill) |
The rules hash gains a schema block for these keys (src/Data/RulesHash.cs).

## 4 · Determinism, state and format
- **New state part `"veterancy"`, appended after `"salvage"`** (Simulation.cs ~line 99-112: parts end with rigs, salvage; the
  array sizes `builtInAfterCombat = 11` become 12). Content: `int killValue` per vehicle slot and per infantry slot, a `lastCombatTick`
  per slot (for heal-quiet). Rank is derived, so there is no stored rank to drift from the table.
- Why a part and not fields in MobileStore: MobileStore is shared by units and infantry and every older part keeps its layout;
  M6 did the same (rigs, salvage). The cost is clearing the entry on spawn and in `RunCleanup` (Simulation.cs:631) so a reused slot
  starts at 0. A test must cover slot reuse.
- Bonuses are applied at use, all integer: damage in `Detonate`/`Fire` (shooter rank), damage taken in `Hurt` (Combat.cs:257,
  target rank), reload next to the aura line (Combat.cs:242), speed by composing with `Effects.SpeedPermille` (EffectsState.cs:85,
  used at MoveMission.cs:49-51, AirMission.cs:51). Self-heal runs where `(Tick + slot) % heal_interval == 0`.
- Hash coverage: both arrays are written and so enter `ComputeStateHash` (Simulation.cs:726); HashCoverageTests.cs:190 passes
  without allow-list entries. No float, no randomness.
- **Save and replay format 11** (SimWriter.cs:15 SaveVersion 10 -> 11; ReplayWriter.cs:21 Version 10 -> 11; OldestReadableVersion stays
  7 unless the lead decides otherwise). Format 11 is shared with the M7 parts (11_m7_plan §3); only one bump lands.

## 5 · View and UI
`PlayerView` actor records gain `Rank` (0-3) for any actor the viewer can see (fog-honest: only if visible now). The game layer draws
**rank pips** (1-3 chevrons) beside the health bar, and a short flash plus a sound on promotion (detected by the client when `Rank`
rises; no sim event). The unit info panel shows rank name and kill value to the next rank for own units. Archive/tooltips explain it.

## 6 · AI
AI reads rank from the view only (D-051). (a) Army value: `cost x (1000 + 250 x rank) / 1000`, used by wave-gate and "ahead" tests
(MilitaryManager). (b) RetreatManager (src/AI/Recruit/RetreatManager.cs:9) adds `+10 % health per rank` to the retreat threshold, so
rank 2-3 units leave earlier; a retreated rank-3 unit goes to repair, not home guard. (c) Focus-fire treats a visible enemy rank 3 as
a priority target. (d) Squad composition keeps ranked units together. Expedition AI follows 07 §7 ("value units by cost").

## 7 · Balance plan
`ai match`, all faction pairs, every map size: count units reaching each rank by minute, kills per rank, win rate with and without
veterancy. Table: faction x (rank-1/2/3 units per game at minute 10 and 20). Target: rank 3 appears in most games after minute 12
and in less than 1 in 10 units; no faction's win rate moves more than 5 points.

## 8 · Contracts
| Contract | Value |
|---|---|
| State part | `"veterancy"` after `"salvage"` |
| `Fire`/`Detonate`/`Hurt`/`QueueHit` | gain a shooter `ActorRef` (internal, not public API) |
| `PlayerView` | `Rank` on actor records; `Simulation.RankOf(kind, i)` |
| Rules | section 3 |
| Format | save and replay 11 |

## 9 · Packages
| Id | What | Owns | Depends on |
|---|---|---|---|
| VET-1 | Rules + loader + hash block, `"veterancy"` part, format 11, order and hash tests | src/Sim, src/Data | — |
| VET-2 | Shooter carried through Fire/projectiles/Hurt; kill credit; slot-reuse test | Simulation.Combat.cs, Combat/ | VET-1 |
| VET-3 | Bonuses: damage, taken, reload, speed, heal; unit tests per rank | Simulation.Veterancy.cs | VET-2 |
| VET-4 | Data: `[veterancy]`, `veteran` flags, `exp_veteran_drill` effect | data/rules | VET-1 |
| VET-5 | Game layer: pips, flash, info panel | src/Game | VET-1 |
| VET-6 | AI: army value, retreat, focus fire | src/AI/Recruit | VET-1 |
| VET-7 | Balance table | docs/balance | all |

## 10 · Acceptance
- With `enabled = false` all old tests, goldens and hashes match (apart from the format number).
- A scripted fight promotes a unit at the thresholds; rank-3 bonuses measured in a unit test (damage, reload, taken, heal).
- Save/load mid-fight and golden replay reproduce ranks exactly; a reused slot starts at rank 0.
- Open: names, Expedition rule (A/B), buildings' kill value 500, heal numbers.
