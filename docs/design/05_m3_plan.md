# Design 05 · M3 plan: the Directorate, tunnels, Authority counters, AI tiers, audio
From D-073..D-080 (session 4). Owner can try at the end: skirmish as either faction against either faction; the
Directorate tunnels, burrows, irradiates and blacks out; the Authority seals its walls, spots burrowers, cleans up
radiation; AI opponents at five tiers; sound effects and a music player.

All numbers below are starting values marked `(tune)` in TOML. The balance pass (M3-14) sets them from headless
matches. Ticks are at 20 Hz.

## 1 · Tunnel spec (answers Q-29; D-074)
**Two mechanics.** *Network transit* moves units between a player's own entrances. *Free burrowing* moves
Burrow-locomotion units under open ground.

**Tunnel size.** Every mobile type has `tunnel_size` = none | S | M | L. Infantry default S. Directorate vehicles
are M unless set. L (Reactor Crawler, land-train) never uses the network. Authority units have none.

**Entrances.** A building with `entrance = "S"` (Hatch, Muster Hatch, Smelter Vent* ) or `"M"` (Bore Head,
Assembly Bore, Command Shaft, Smelter Vent*) is a network node. (*The Smelter Vent is M so scrapers fit.)
All of one player's finished entrances form one network.

**Transit (A3).** `Enter` with Subject = unit and Target = a destination entrance: the unit walks to the nearest
own entrance that fits its size, joins that entrance's queue, and is drawn in at `ticks_per_point` per point
(S = 1 point, M = 3 points). Hatch class 20 ticks/point (1 pt/s), Bore Head class 13 ticks/point (≈1.5 pt/s)
(tune). Then it is underground for a flat `transit_ticks` = 40 (2 s), then surfaces at the destination's exit cell
(normal unit exit search). The network carries at most `network_cap_points` = 10 points in transit at once; a full
network stalls all queues. The destination must fit the unit's size; else the order is refused.

**Exit lost (D2).** If the destination is destroyed or sold while units are in its transit list, each unit is
re-routed to the nearest surviving own entrance (straight-line from the lost one) that fits it, restarting
`transit_ticks`. If none exists, the units are lost (killed, no credits back).

**Free burrowing.** A Burrow unit ordered to move dives (`dive_ticks` = 20, visible), travels underground with the
terrain table's burrow column, ignoring surface occupancy, and surfaces at the goal. Surfacing needs a *soft*,
unhardened cell not within 1 cell of any structure (either side's); if the goal is not valid, the unit surfaces at
the nearest valid cell (spiral search). Surfacing takes `telegraph_ticks` = 60 (3 s): a *surfacing telegraph* sits
on the cell; the unit appears at the end. While burrowed: cannot attack, cannot be attacked or crushed, gives no
vision, invisible to enemies (no trail) except inside an enemy Seismograph radius.

**Soft ground** (`terrain.toml [soft]`): clear, rough, ash = soft; road, rock, cliff, water = hard.

**New entrances.** The Bore (burrow, M) is ordered with `Deploy` at a cell: it travels burrowed, then breaks
through into a Hatch. Break-through uses `entrance_telegraph_ticks` = 100 (5 s); the Hatch exists from the start of
the telegraph with `emerging_health_permille` = 250 of its HP and is not a network node until finished. A Hatch is
upgraded to a Bore Head with the `Upgrade` command (cost = difference, build time from cost).

**Telegraph.** A view-level marker (cell, owner, ticks left, kind unit|entrance) visible to any player who sees the
cell. The game draws ground darkening → cracks → dust and a 1-tile shake (D-075).

**What the enemy and the AI know (F).** Fog-honest: the network, queues and burrowed units are known only to their
owner (and allies). Enemies see entrances as normal buildings and ghosts, telegraphs in their vision, and burrowed
units inside their Seismograph radius. The AI gets exactly the human view (PlayerView).

## 2 · Hardening and gates (D-076)
- **Foundation Grouting** is an Authority upgrade (researched at the Authority tech structure — see roster). With
  it, every soft cell fully enclosed by that team's walls, gates and structures becomes *hardened*: nothing can
  surface there and a Bore cannot break through there. Enclosure = not reachable by a 4-neighbour flood fill from
  the map edge through cells that are not that team's wall/gate/structure cells. Recomputed when such a building
  appears or disappears (event-driven, deterministic order).
- **Gate**: a 1×1 Authority wall piece (`gate = true`). Passable for the owner's team, a wall for everyone else.
  It always counts as part of the seal. Enemy units must destroy it. Pathing becomes team-aware for gate cells.
- **Walls may close.** The one-cell gap rule does not apply between wall/gate pieces and other buildings, so a
  base can be sealed (supersedes the M2 lead choice in design 04).
- A destroyed wall or gate re-opens the area (flood fill reaches it) — the Sapper's Collapse Charge is built to
  exploit this.

## 3 · Radiation, Blackout, vision (D-077)
- **Radiation** is a per-cell level 0..255 in a new state part. Warheads may `contaminate` (add level in their
  splash) or `decontaminate` (subtract). Level decays by 1 per `rad_decay_ticks` = 40. Every 20 ticks, units on a
  cell with level ≥ 32 take `level × rad_damage_permille / 1000` damage, scaled by their `rad_resist_permille`
  (Directorate infantry 600, crawlers 1000 immune, Authority infantry 0). Authority can't place structures on cells
  with level ≥ 64. Units flagged `irradiated` (reactor-cored) take the warhead's `bonus_vs_irradiated_percent`.
- **Blackout**: warheads may `blackout_ticks`; cells in a Blackout cloud can only be revealed by observers within
  1 cell or by revealers with `sees_through_blackout` (Searchlight Tower).
- **Seismograph**: building `seismic_radius` (cells) reveals enemy burrowed units and in-progress telegraphs within
  it to its owner, through fog.
- **Stealth**: `stealth = true` units (Informant) are invisible to enemies after `stealth_delay_ticks` = 60 without
  moving or firing; revealed to enemies within 1 cell, and to Searchlight sight.
- **Static Broadcast**: a jam radius (`jam_radius`) on the Agitprop Crawler and Broadcast Mast hides those cells on
  enemy minimaps (view-level only; sim unaffected).

## 4 · Generic systems the roster needs (contracts)
- **Factions**: `Faction { Authority = 0, Directorate = 1 }`; every type has `faction` (default from the id prefix
  `auth_` / `dir_`). Each player has a faction (setup); the start kit per faction comes from `game.toml
  [faction.<name>]` (conyard id, start units). Buildable lists filter by faction.
- **Upgrades**: `[upgrades]` table in a new `upgrades.toml`: id, faction, cost, build_time, prerequisites, effect key.
  `ProductionCategory.Upgrade = 5` (appended) queued at a building with `researches = true`; finishing sets a bit
  in the player's upgrade mask (players part gets `ulong[] Upgrades`, appended to its serialisation). Prerequisite
  strings may name upgrades.
- **Auras**: `aura_radius`, `aura_rof_permille`, `aura_speed_permille`, `aura_targets` (type ids) on mobile types;
  recomputed every 10 ticks into per-actor multipliers.
- **Transports**: `passengers` (infantry capacity) on vehicles; `Enter` with Target = own transport loads;
  `Deploy` on a transport unloads. Passengers are hidden and move with it; die with it (rec: they are lost).
- **Deploy**: vehicles with `deploys_into` = building id (land-train → Command Shaft; Bore → Hatch at a cell).
- **Air**: `Locomotion.Air` units ignore terrain and occupancy, fly at altitude; weapons get `targets` =
  ground | air | both (default ground). Dirigibles are slow, fragile, unarmed except bomb-like drops.
- **Death weapon**: `death_weapon` fires at the unit's own cell when it dies (Reactor Crawler meltdown).
- **Engineer**: infantry `engineer = true`; `Enter` on an enemy building below 50 % HP captures it, on a friendly
  one repairs it to full (unit consumed in both cases) (tune).
- **Superweapon**: building `superweapon = <id>` with charge ticks and a `Special` command to fire; the Sunrise
  Device also needs an own entrance within `superweapon_entrance_range` = 8 cells of the target.

New `CommandKind` appended: `Upgrade = 21` (Subject = building, Arg = target building type). `Enter` (9) and
`Deploy` (6) and `Special` (18) get the payloads above. New state parts appended after "production" (and after any
extension parts already registered): `"tunnels"`, `"ground"` (radiation, hardening, blackout timers),
`"effects"` (aura multipliers, stealth timers) — exact order fixed by M3-01 and pinned by its test.
New `MissionKind` appended as needed (EnterTunnel, Transit, Burrow, Surface, Deploy, Capture).

## 5 · Rosters (D-078, D-079)
**Directorate units (13):** dir_conscript, dir_ashthrower, dir_sapper, dir_informant, dir_commissar (infantry);
dir_scraper (harvester, burrow), dir_bore (burrow, deploys to Hatch), dir_mole (burrow, 5 passengers),
dir_agitprop (aura + jam), dir_reactor_crawler (L, rad cannon, meltdown, irradiated), dir_rocket_truck
(artillery + anti-air), dir_dirigible (air, scouting, Blackout drop), dir_landtrain (L, deploys into Command Shaft).
**Directorate structures (16):** dir_shaft (conyard, M), dir_reactor_vent (power), dir_smelter_vent (refinery, M),
dir_muster_hatch (barracks, S), dir_assembly_bore (factory, M), dir_hatch (S), dir_bore_head (M), dir_cistern
(silo), dir_bunker (wall), dir_pillbox, dir_rocket_nest (anti-air), dir_rad_mortar (rad defence), dir_mast (radar
+ jam), dir_isotope_works (tech, researches), dir_mooring (produces aircraft), dir_sunrise (superweapon).
**Authority counters (5):** auth_seismograph (structure), auth_decon_truck (vehicle), auth_searchlight (defence),
auth_gate (wall piece), upgrade auth_grouting researched at a new auth_engineering (tech structure, researches).
Later (M4): Seismic Mortar, Quick-Set Foam, the rest of the Authority roster. Names are working names.

## 6 · Audio (D-080)
Voice is text-only (teletype/ticker). SFX: procedural sfxr-style generator (`tools/audio/`, our own, CC0) plus CC0
packs the owner adds later; music: ACE-Step on the owner's PC from our prompts (`docs/audio/music_brief.md`),
shipped as CC0 with the tool credited. Game: a MonoGame `IAudio` backend, FxEvent → sound mapping in
`data/audio/sounds.toml`, positional volume by camera distance, a music player that plays whatever OGG files are
in `data/audio/music/` (none is required).

## 7 · Items (ids in ROADMAP)
| Id | What | Wave | Model |
|---|---|---|---|
| M3-01 | Contracts: factions, schema v3 fields, new enums, state-part skeletons, command payloads, view/HUD additions, stubs | 0 | Opus |
| M3-02 | Tunnel network transit: entrances, queues, throughput, transit, reroute, Enter | 1 | Opus |
| M3-03 | Burrow locomotion, surfacing + telegraph, Bore deploy → Hatch, Hatch → Bore Head upgrade, land-train deploy | 1 | Sonnet |
| M3-04 | Hardening (grouting enclosure), gates, team-aware pathing for gates, walls may close | 1 | Opus |
| M3-05 | Radiation, decon, Blackout, Seismograph, Searchlight, stealth, jam view | 1 | Sonnet |
| M3-06 | Upgrades queue, auras, transports, air + targets, death weapon, engineer capture/repair | 1 | Sonnet |
| M3-07 | Data: Directorate roster + Authority counters + upgrades in TOML, faction start kits | 1 | Haiku |
| M3-08 | Game: faction pick on setup, telegraph/radiation/blackout/jam/underground rendering, upgrade tab, placeholders | 1 | Sonnet |
| M3-09 | Audio: MonoGame backend, sounds.toml mapping, sfxr generator + first SFX set, music player, music brief | 1 | Sonnet |
| M3-ART-01..n | Art fan-out from the approved pilot (Directorate 29 types, Authority 5, effects) | 1–3 | Sonnet |
| M3-10 | Directorate AI (Recruit framework: tunnels, Bores, radiation) | 2 | Sonnet |
| M3-11 | AI tiers 2–5 on both factions, measured with `Tools ai matrix` | 2 | Sonnet |
| M3-12 | Session 3 follow-ups (redundant probes, explosion size, hitscan effects, mission replays, Lua instruction limit, wall art at 1×, Rocketeer flash crop) | 2 | Haiku/Sonnet |
| M3-13 | Superweapon: the Sunrise Device | 2 | Sonnet |
| M3-14 | Integration + balance pass (tunnel time saved 30–50 %, faction win rates near 50 % at equal tier) | 3 | lead |
