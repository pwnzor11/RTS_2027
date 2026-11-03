# Design 02 · Game design
Consolidates D-020..D-026 and D-030..D-034. Numbers marked *(tune)* are starting points for
playtesting, not decisions.

## 1. Pillars
1. **Classic C&C feel:** sidebar build, cell-locked movement, harvester economy, bases that matter.
2. **Two factions that play nothing alike** (StarCraft-grade asymmetry), the villain more fun.
3. **A story campaign that teaches the game** by unlocking the tech tree mission by mission.
4. **An honest AI** that gets better by doing more, not by cheating.

## 2. Economy
- **Resource:** ashfall (common tier) and pitchblende (rich tier), one resource type with two
  density bands in data. Per-cell density 0..12 *(tune)*; credits per unit density differ by tier.
- **Harvest loop:** harvester seeks richest cell on nearest ring (remembers last field), loads N
  units per tick *(tune)*, returns, docks, unloads as a trickle into storage.
- **Ledger:** `Cash` (uncapped, spendable) + `Stored` (capped by refinery and silo capacity;
  overflow lost). Spending draws from both.
- **Regrowth:** per-map setting. Ashfall regrows by a slow seeded sweep from designer-placed
  "downwind" seed markers; pitchblende never regrows.
- **Hazard:** unprotected infantry standing on ashfall take slow damage; both factions have
  countermeasures that are part of their identity (see 03_setting).
- **Power:** a gate, not a resource. Low power slows production (×1.5/2.5/4 at thresholds, the C&C
  curve *(tune)*) and disables powered defences.
- **Production:** cost drains over build time, stalls when broke, refunds on cancel. Five queues
  (infantry, vehicles, aircraft, structures, defences); additional factories speed the queue; one
  "primary" factory per category sets the rally point.
- **AI economy:** same rules as humans. Higher tiers field more harvesters and expand earlier.

## 3. Map and terrain
- Square cells, up to 128×128; skirmish maps 64–96 early on.
- Layers: visual template/sub-icon, land type (derived + per-cell override), height level,
  overlay (resource, walls, crates), smudge, visibility.
- **Elevation:** discrete cliff levels; cliffs are walls; ramps connect levels. No height bonuses.
- **Land types × locomotion** table in data. Locomotion classes in v1: foot, wheeled, tracked,
  hover, air, *burrow* (Directorate tunnelers), with a naval slot reserved.
- Buildings may never seal a region (one-cell gap rule), so "unreachable" is always terrain.
- Two theaters, each with its own template set and palette: **Rust Belt**, **Glass Coast**.
- Starter skirmish-map rules: two-fold symmetry with rotation check; at least two routes between any
  two bases; wide main ramps; several smaller ashfall fields rather than one wall; one pitchblende pit
  per start plus contested pits in the middle; playtest every start position.

## 4. Combat
- Weapon → projectile → warhead → armour class table (5 armour classes × ~8 warheads, percent
  integers). Minimum damage 1. Splash in a 3×3 cell block with falloff, hits allies.
- Rate of fire, range and sight in cells/leptons; 8-bit facings with turret facings separate.
- Infantry: 5 per cell, can be crushed by heavy vehicles; prone and garrison behaviours are per-type.
- Veterancy: *(deferred; decide after the first playable slice)*.

## 5. Tech tree
- **Unlocks:** owning building X enables Y (prerequisite list), with `provides` for equivalence.
- **Upgrades:** researched at a tech building, applied as effect lists (`set/add/mul` on attributes
  for a unit id or class). Each faction has roughly 8–12 upgrades in v1 *(tune)*.
- **Superweapons:** two per faction, each requiring a dedicated expensive structure, a deep
  prerequisite chain and a long charge; designed so that reaching one is a mid/late-game commitment.
- One skirmish tech tree. Campaign missions grant subsets via `Tech.Grant`.
- No navy in v1.

## 6. Fog and information
- Shroud (unexplored) + true fog (out of sight). Enemy units vanish in fog; buildings persist as
  last-seen ghosts. Per-player visibility bitmask per cell, recomputed incrementally.
- Radar/minimap requires its structure and power (classic).
- The AI's world model is built only from what its units see; it keeps last-known positions with a
  timestamp.

## 7. Skirmish AI
- Managers: Economy, Production, Military, Scouting, Strategy; each runs every N ticks and issues
  `Command`s through the normal queue.
- **Five tiers**, defined in `difficulty.toml` as capability profiles (decision interval, scout
  information age, build repertoire, micro flags, target-priority quality, attack threshold,
  expansion timing, superweapon use, multi-front flag). Sketch:
  1. *Recruit:* fixed build order, slow waves, no repair, no harvester replacement.
  2. *Regular:* + repair, harvester replacement, responds to attacks, scatters from splash.
  3. *Veteran:* + enemy-aware unit mix, early expansion, two-pronged attacks, counter-builds defences.
  4. *Elite:* + air, superweapons, active scouting, harasses harvesters and power, retreats damaged
     forces, uses upgrades.
  5. *Commissar:* + multi-front timing attacks, build-order adaptation, tight decision interval;
     tuned as an expert challenge.
- Three personalities (rusher / turtle / boomer) as weight sets, selectable or random.
- No cheats by default; an optional labelled "cheater" resource bonus toggle may be added later.
- Headless AI-vs-AI runner from the start; a win-rate matrix between tiers is a release check.

## 8. Campaign
- 10–12 missions per side + tutorial; one spine, 2–3 two-way choice points that rejoin.
- Maps shared between campaigns with different scripts; about half build-and-destroy, a quarter
  defend/escort, a quarter commando or no-base; no unit carry-over.
- Briefings: text + portrait + music; teletype for the Authority, propaganda radio for the
  Directorate; in-mission radio lines; 2–3 still-art cutscenes per side at act breaks.
- Each mission declares its unlocked set; the unlock order is designed against the story beats.
- Campaign difficulty uses the same tiers for enemy AI plus scripted differences (reinforcement
  timing, starting credits).

## 9. Skirmish and LAN
- Skirmish: full tech tree, 2–8 players (humans + AI), map, faction, colour, start, difficulty,
  personality, regrowth on/off, starting credits, superweapons on/off, crates on/off.
- LAN: host-star lockstep, 2–4 humans plus AI slots, broadcast discovery plus join-by-IP, hash checks
  on join, no host migration in v1.

## 10. Presentation
- HD sprite scale (cell pixel size from the art pilot, M0-12), 16 facings vehicles / 8 infantry.
- Palette-indexed house colours via shader lookup. Integer scaling, pixel-perfect camera.
- Classic right-hand sidebar with cameos and queues; radar minimap; EVA-style announcer per faction.
