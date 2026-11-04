# Design 07 · The Overseas Expedition (faction design, naval contract sketch)
From D-086 and D-087. DRAFT by the M7-01 helper for the lead. Nothing here is a decision until it has a DECISIONS entry.
All ids, numbers and names are working values; numbers marked `(tune)` go to TOML and the balance pass. Ticks are 20 Hz.
Faction prefix: `exp_`. Fiction: see `docs/lore/expedition.md` (all leaders and institutions invented).

## 1 · Identity pitch
The Expedition is the **most advanced and least numerous** army on the board: the relief fleet of an old-world
remnant government, a long way from home, with a few sealed-core ships, hovercraft and jets that nobody on this
continent could build today. It does not own the land; it owns **the coast and the sky above it**. It arrives from
the sea, plants a beachhead, and pushes inland on a supply line the enemy can cut.

How it feels: the Authority grinds and holds, the Directorate swarms and surprises, the Expedition **lands, strikes
and withdraws**. Every unit is precious (it cannot be replaced from home quickly), so the player is rewarded for
micro, veterancy and trading efficiently, and punished for waste.

| | Authority | Directorate | Expedition |
|---|---|---|---|
| Base | Fixed, walled | Entrances, tunnels | Beachheads on a coast, forward beacons |
| Movement | Roads, ramps | Burrow | Sea, shallows, hover, air |
| Infantry | Few, protected | Many, cheap | Few, elite, amphibious |
| Vehicles | Heavy tracks | Strange reactor crawlers | Light hover, fast, accurate |
| Air | Real air force | None | Best air, carriers |
| Economy | Slow, durable | Fast, fragile | Local ash plus home shipments (raidable) |
| Weakness | Slow | Fragile, no range | Inland, cost, low numbers, supply line |

## 2 · Asymmetric mechanics

### 2.1 Beachheads and landing zones
- **Start.** The player starts with one `exp_command_ship` at sea. `Deploy` at a *landing coast* cell (a shore cell
  adjacent to shallow water) turns it into the `exp_landing_depot` (conyard), same pattern as the Directorate
  land-train → Command Shaft (D-078). Start kit also: 1 landing craft, 3 marines, 1 skimmer.
- **Build zone.** Expedition structures may only be placed within `beachhead_radius` (tune, 10 cells) of a
  *beachhead source*: the depot, a finished `exp_beacon`, or a finished `exp_shipyard`/`exp_quay`. Radius is by
  cell distance, not road or wall connectivity. (The Authority builds anywhere near its yard; this is the same idea
  with a shorter reach and a cheap, fast way to extend.)
- **Beacon.** `exp_beacon` is a 1x1 cheap structure; it can be delivered by landing craft or Skyhook airlift to any
  soft land cell and builds in 10 s (tune), extending the zone by `beacon_radius` 6 (tune). Beacons are fragile: that
  is the counterplay, and losing one orphans structures only for *new placement* (existing ones keep working).
- **Landing zone.** A shore cell flagged `landing = true` by terrain (beach/coast) lets a landing craft unload
  vehicles; plain shallows allow infantry and hover only (section 4.5).

### 2.2 Sea and air supply: the supply line
- **Home shipments.** An `exp_anchorage` structure (a quay on deep or shallow water) receives **shipments**. Every
  `shipment_interval` (tune, 90 s) a convoy of 1-3 `exp_supply_tender` ships spawns at a deterministic map-edge sea
  cell and sails to the anchorage; on arrival it pays a lump `shipment_value` (tune, 1500 credits) per tender.
  If a tender is sunk, its credits are lost. The convoy is visible, slow and escorted by nothing unless the player
  provides escorts: **the supply line is the Expedition's vulnerable line and the enemy's target.**
- **Home fatigue.** Shipment value decays by `home_decay_permille` (tune, 40 per shipment) down to a floor of
  35 % (tune), and **recovers** with a *Charter Milestone*: each captured or held pitchblende pit or ash field
  held for 60 s (tune) resets the decay by one step. Message: the home government keeps paying for results.
- **Landlocked fallback.** No sea access: shipments arrive by air (`exp_skyfreighter`, see roster) to an
  `exp_airhead` pad. Same economy, shorter interval, smaller value, much squishier. See question Q1.
- **Air supply.** Skyhook (VTOL) airlifts move infantry and light vehicles between beaches and beacons at 2x
  ground speed; they are the Expedition's tunnel analogue.

### 2.3 Few but elite
- Roughly **27 mobile types** (a few optional) vs the Directorate 13, each with higher stats per cost, `population`-style cap per
  faction of 40 units (tune; see Q4). Unit veterancy: 3 levels, faster on kills, because every unit matters.
- Costs are higher per unit, build times faster, so they replace losses *if* the shipment arrives.
- No mass infantry. Infantry are small elite squads (marines are one actor, not a squad; design stays one-per-actor).

### 2.4 Advanced tech
Sealed-core ships (huge range, no fuel logistics), **hover** units that cross water, shallows and beach at full speed
and land at 80 %, **guided weapons** (missiles with lock time, jammable by Directorate Static Broadcast), **sonar**
and **plotting** (spotting for naval gunfire), early valve-computer "Core Hall" research for upgrades. All numeric
bonuses are reachable with existing mechanics (auras, upgrades) plus the naval additions in section 4.

### 2.5 Other twists (kept small)
- **Naval gunfire support:** `exp_pathfinder` or `exp_gyro` can *designate* a ground cell; cruisers and destroyers in range
  fire at it (artillery-style, indirect, uses the existing `Attack`/ground target). No new command (section 4.7).
- **Withdraw:** a landing craft or Skyhook can `Enter`-load a wounded unit; units inside heal slowly (`bay_repair`).

## 3 · Economy
- **Resource:** ashfall as everyone else. Harvester: `exp_skimmer` (hover, amphibious, light armour, fast, low
  capacity) delivers to `exp_dredge_works` (refinery) or to a quay within `dock_range`. It can harvest on ash in the
  shallows, which land factions cannot reach (a modest bonus; map ash must exist there).
- **Silo:** `exp_tank_farm`.
- **Power:** `exp_pile_house` (reactor); lower output than the Authority plant, but cheaper and sealed.
- **Income shape:** low steady local income + a lump every ~90 s from home. The AI and player both see the
  convoy and the timer in the HUD (view-level only).
- **Rules:** shipments are deterministic (timers in sim ticks, spawn cell from the map's `convoy_edge` marker or a
  seed-index of ocean-edge cells). No float.

## 4 · Three-tier tech tree
**Tier 1, Landing (start):** `exp_landing_depot` → `exp_pile_house`, `exp_dredge_works`, `exp_billet` (barracks),
`exp_hover_yard` (vehicles), `exp_quay` (small ship yard, patrol boats and landing craft), `exp_beacon`. Units:
marine, pioneer, diver, skimmer, hoverlander, gyro, launch, landing craft.
**Tier 2, Fleet:** needs `exp_plotting_room` (radar, tier gate) + `exp_flight_deck` or `exp_shipyard`. Units: pathfinder,
glide tank, destroyer, missile sub, jet fighter, skyhook, gunship; defences: `exp_gun_battery`, `exp_picket`,
`exp_sam`; `exp_anchorage` available at T1 (economy gate). Upgrades begin.
**Tier 3, Core:** needs `exp_core_hall` (computer, researches) + `exp_drydock` (heavy ships) for cruiser/carrier; units:
ironbound, barrage lander, cruiser, carrier, bomber, skyfreighter; `exp_salvo_array` (superweapon).

## 5 · Roster
Size column: **S** = infantry cell, **M** = vehicle/boat 1 cell, **L** = large ship (draws 2 cells; collision 1 cell; see 6.4),
or building footprint. Tier T1-3. Locomotion: Foot, Hover, Naval, Air, Wheel/Track.

### Infantry (5)
| id | name | role | counters / weak to | size | T |
|---|---|---|---|---|---|
| exp_marine | Marine | all-round rifle, amphibious (shallows) | beats light vehicles; weak to Ashthrowers, artillery | S | 1 |
| exp_pioneer | Pioneer | engineer: capture/repair, builds beacon at land | fragile | S | 1 |
| exp_diver | Combat Diver | swims in deep water, plants charges on ships/quays | sonar picket, depth gear | S | 1 |
| exp_pathfinder | Pathfinder | spotter/sniper; designates ground cells for naval gunfire; stealth-in-cover | everything at close range | S | 2 |
| exp_ironbound | Ironbound Trooper | elite powered-suit heavy; slow; high HP, AA missile pod | massed anti-armour, crawlers | S | 3 |

### Vehicles and hovercraft (7)
| id | name | role | counters / weak to | size | T |
|---|---|---|---|---|---|
| exp_skimmer | Hover Skimmer | harvester, amphibious | any raider; light armour | M | 1 |
| exp_hoverlander | Hoverlander | APC, 5 passengers, amphibious | rockets, mines | M | 1 |
| exp_glide_tank | Glide Tank | fast hover medium tank, guided cannon | heavy tanks, artillery | M | 2 |
| exp_sparker | Sparker | hover AA (guided missile) | ground units | M | 2 |
| exp_barrage_lander | Barrage Lander | rocket artillery on tracks, can load onto landing craft | anything that closes | M | 3 |
| exp_repair_hover | Mender | repairs vehicles and boats | fragile | M | 2 |
| exp_survey_hover | Survey Hover | scout/recon; reveals ash richness | fragile | M | 1 |

### Naval (9)
| id | name | role | counters / weak to | size | T |
|---|---|---|---|---|---|
| exp_launch | Patrol Launch | cheap scout/skirmisher | destroyers, shore guns | M | 1 |
| exp_landing_craft | Landing Craft | transport 8 infantry or 2 vehicles (4 slots); beachable | everything; keep escorted | M | 1 |
| exp_destroyer | Destroyer | all-round warship, depth charge + AA | cruisers, subs, bombers | M | 2 |
| exp_missile_sub | Missile Submarine | submerged, guided anti-ship and land strike; invisible without sonar | picket, destroyer depth charges | M | 2 |
| exp_cruiser | Cruiser | shore bombardment, long-range gun | carriers, subs, air | L | 3 |
| exp_carrier | Carrier | produces and recovers aircraft at sea (4 slots), AA | subs, bombers | L | 3 |
| exp_supply_tender | Supply Tender | convoy ship, carries shipments, unarmed | everything | M | 1 |
| exp_command_ship | Command Ship | start unit; deploys into depot ashore | everything | L | start |
| exp_minelayer | Minelayer | lays sea mines (optional, cut if scope) | sonar, mine-sweeping | M | 2 |

### Air (6)
| id | name | role | counters / weak to | size | T |
|---|---|---|---|---|---|
| exp_gyro | Gyro Scout | rotary scout, designator | AA, fighters | M | 1 |
| exp_skyhook | Skyhook | VTOL transport, 6 infantry/2 light vehicles | AA | M | 2 |
| exp_jet | Jet Fighter | air superiority | AA, bombers' escorts | M | 2 |
| exp_gunship | Gunship | rotary ground attack | AA, jets | M | 2 |
| exp_bomber | Bomber | heavy bombs vs buildings and ships | fighters, AA | M | 3 |
| exp_skyfreighter | Skyfreighter | air-supply tender for landlocked maps | everything | L | 3 |

### Structures (about 20)
| id | name | role | size | T |
|---|---|---|---|---|
| exp_landing_depot | Landing Depot | conyard (deployed Command Ship) | 3x3 | start |
| exp_pile_house | Pile House | power | 2x2 | 1 |
| exp_dredge_works | Dredge Works | refinery | 3x2 | 1 |
| exp_tank_farm | Tank Farm | silo | 1x2 | 1 |
| exp_billet | Billet | barracks | 2x2 | 1 |
| exp_hover_yard | Hover Yard | vehicle factory | 3x2 | 1 |
| exp_quay | Quay | small naval yard (needs water adjacent) | 3x2 | 1 |
| exp_beacon | Landing Beacon | extends build zone | 1x1 | 1 |
| exp_plotting_room | Plotting Room | radar, tier gate, designation range | 2x2 | 2 |
| exp_shipyard | Shipyard | warships (needs water adjacent) | 3x3 | 2 |
| exp_flight_deck | Flight Deck | aircraft, pads | 3x3 | 2 |
| exp_anchorage | Anchorage | shipment receiver (water adjacent) | 2x2 | 1 |
| exp_airhead | Air Head | shipment receiver for air freight | 2x2 | 3 |
| exp_gun_battery | Gun Battery | coastal defence vs ships and ground | 2x2 | 2 |
| exp_picket | Sonar Picket | detects submerged, reveals sea | 1x1 | 2 |
| exp_sam | SAM Mount | AA | 1x1 | 2 |
| exp_pillbox | Seawall Emplacement | cheap turret | 1x1 | 1 |
| exp_core_hall | Core Hall | tech, research, valve-computer | 3x3 | 3 |
| exp_drydock | Drydock | heavy ships, repairs | 4x3 | 3 |
| exp_salvo_array | Salvo Array | superweapon | 3x3 | 3 |

### Upgrades (8, researched at Core Hall; Plotting Room for first two)
`exp_sonar_nets` (picket range +; subs revealed to team), `exp_hardened_hulls` (ships +armour), `exp_vacuum_cores`
(vehicle ROF +), `exp_composite_armour` (hover/infantry armour), `exp_guided_shells` (artillery accuracy),
`exp_fast_charging` (shipment interval -15 %), `exp_deep_survey` (skimmer capacity +), `exp_veteran_drill` (vet
threshold lower). All are plain `Upgrade` effects; no new engine rules.

### Superweapon
**Salvo Array** (recommended): a fleet-wide cruise-missile barrage on a target area. Charge 6 min (tune); fires 12
missiles over 6 s in radius 4 cells, 150 damage each (tune); long visible ship launch trails give a 4 s warning.
Counter: a SAM screen, jamming, and killing the Array. **Alternative (stretch, Q6):** "The Long Tide", a seabed
charge that floods a coastal band temporarily (needs terrain mutation in the sim).

## 6 · Naval systems contract sketch
Goal: a list of choices the lead can turn into contracts. Each choice has options and a **Rec**. Nothing is decided.

### 6.1 Terrain and locomotion
Today `terrain.toml` has one `water` land type (hard, impassable to land, per design 05 section 1) and a
locomotion x land-type speed table. Choices:
- **A. Split water.** Add land types `shallow` (ford, wadeable) and `deep`, plus `coast` (beach, land with the
  `landing` flag); `water` remains an alias for `deep` so old maps load. **Rec A.**
- B. One water type with a per-cell depth byte. More flexible, but bigger map format change. Not rec for v1.
- **Locomotion:** append `Naval` and `Hover` to the enum (appended, not renumbered). Foot units: shallow 40 %
  speed, deep 0. Wheel/Track: shallow 0 (rec: impassable, to keep ships relevant; Q2). Hover: everywhere except
  cliff/rock walls (80 % beach, 100 % water). Naval: deep 100 %, shallow 100 % for boats (M) / 0 for large (L), coast 0.
  Air unchanged. Burrow: water hard, as today.
- **Map format:** needs a version bump (cells already carry land type; adding types is additive). Procedural maps (D-087)
  must emit water/shore consistently with the generator rules (Glass Coast, Ash plains/rivers).
- **Buildable.** Structures cannot be placed on deep or shallow water except piers/quays flagged
  `water_building` (Quay, Shipyard, Anchorage, Drydock). **Rec:** these straddle one land and one water row
  (footprint has a `water` mask) so they sit on the shoreline.

### 6.2 Ship movement and pathing
- Same `IPathfinder` with a locomotion parameter; cost grid built per locomotion class (Foot, Track, Hover, Naval,
  Air-free). Cached per class and map version counter (buildings change navigable cells).
- **Choice:** ships are M (1 cell) only, with L ships using a *visual* 2-cell draw but a 1-cell collision body.
  **Rec: 1-cell collision, simple.** Alternative: true 2x2 footprints with a bigger pathing clearance mask
  (accurate but doubles the pathfinding complexity). Q3.
- **Region ids.** Per locomotion class, label connected components of navigable cells at load and after buildings
  change (flood fill in cell index order). Orders to an unreachable goal are refused instantly (no search spam).
  Needed because "no path" is now common (enclosed lake, tributaries).
- **Turning.** Ships turn rate low (`turn_per_tick`), movement uses existing angle/lepton fields; do not add inertia
  (determinism cost, no gain). Rec: reuse vehicle movement, with `turn_per_tick` as data only.
- **Blockage:** water cells use the same occupancy bits (vehicle bit); the free/moving/stopped classes apply.
- **Large rivers:** Ash rivers map type (D-087) gives narrow naval lanes; keep the one-cell rule off water so ships can
  be blocked on purpose (choke points), but gaps remain for hover and air.

### 6.3 Landing and unloading
- **Reuse transports** (D-078): `passengers` capacity and `Enter`/`Deploy`-unload. Landing craft use `Enter` to load at
  a quay/shore and a new **`Unload`** command (Target = shore cell) to land. **Rec** (a): sail adjacent to the
  target shore, unload passengers one per `unload_ticks` onto the nearest free land cells. Option (b): craft
  *beaches* onto a `coast` cell, then `Deploy`; faster for vehicles, needs a "beached" mission and a cell lock.
  Rec: (a) for shallow shores, (b) only as a polish item.
- **Vehicles:** landing craft can unload vehicles only at `landing`-flagged coast/shallow cells (a `landing` terrain
  flag, rec), infantry anywhere shore-adjacent.
- **Hover/Amphibious:** the Hoverlander and Skyhook bypass the problem (their reason to exist).
- **Defender view:** no new fog rules; landings are normal unit exits. Enemy gets a "landing alert" cue only if the
  craft is visible.

### 6.4 Naval vs land targeting
- Weapon `targets` currently ground | air | both. **Rec:** make it a flag set `Ground | Air | Naval | Submerged`
  (data uses names; default `Ground|Naval`, meaning old land weapons now hit surface ships too).
- Armour: add `ship_light`, `ship_heavy`, `submerged` rows/columns to the warhead x armour matrix. Torpedo, depth
  charge, naval shell, bomb and missile warheads fill them (data only).
- **Submerged:** submarine behaviour mirrors the Directorate's burrowed unit (invisible unless detected, cannot be
  attacked): flag `submerges = true`; detected by `sonar_radius` (picket, destroyer) like Seismograph `seismic_radius`.
  Rec: reuse that machinery, so the new work is a rename plus a second detection source.
- **Shore bombardment:** indirect weapons already target ground cells; cruisers use `min_range` and `range`. Fog-honest
  spotting: a target must be visible to the owner's team (pathfinder, gyro, scout) or the order is refused; no new command.
- **Aircraft/ships:** carrier hangar uses the aircraft slot logic of the Mooring (`produces aircraft`); recommend a
  generic `aircraft_slots` field on the carrier plus sortie/return missions. This is the largest naval item.

### 6.5 New state parts, commands and mechanics (append-only)
New state parts appended after the last registered part (M3's "effects" and any later ones; exact order fixed at
contract time and pinned by the order test):
- `"supply"`: per-player shipment timer, decay step, in-flight convoys, spawn edge. (`SimRandom` is not used; the spawn
  cell is a deterministic selection.)
- `"beachhead"`: per-player build-zone mask (cells), recomputed event-driven in index order when a source building is
  created or destroyed (like hardening in D-076).
- `"naval"`: region labels per locomotion class, plus ship-specific mission data (unload queue, submerge flag). If it is
  small, merge it into the `"ground"` part instead (lead's call).
New `CommandKind`s appended (numbers tentative; the lead assigns after the highest in use, `Upgrade = 21` today):
`Unload` (Subject = transport, Target = cell), `Submerge` (Subject = sub; or reuse `Deploy` as toggle; **Rec reuse
`Deploy`**, no new kind), `Designate` (**not needed**: use `Attack` on a cell). So a minimal set is **one new kind
(`Unload`)**, with `Special` reused for the Salvo Array and `Deploy` for the Command Ship. New `MissionKind`s: Unload,
Embark, Submerge, Sortie, Return, ConvoySail. Existing `Enter` reused for loading.

### 6.6 Determinism notes
- Region labelling, build-zone masks, convoy spawns: integer only, scan order by cell index, no hash maps.
- Convoy timers are sim-tick counters; shipment value uses integer per-mille decay.
- Sonar detection recomputed every N ticks like Seismograph; `Submerged` visibility is part of the state hash.
- Pathing cache keys include the locomotion class; version counter increments with building changes and with any
  terrain mutation (relevant only if "Long Tide" is approved, Q6).
- Hover and ships use the same movement code: no float; speeds in leptons per tick.
- Replays record `Unload` like any command; no UI-only state.
- Save round trip: the new parts get field-by-field serialisation and the existing save-round-trip test.

### 6.7 Map and generator requirements
Skirmish maps need an **ocean edge** and at least one shore start for the Expedition (the generator guarantee, rec),
`convoy_edge` spawn cells, and ash fields reachable by hover. Maps without water auto-switch to Air Head supply
(Q1). The editor (D-087) needs water brushes and shore auto-tiling (art).

## 7 · AI notes
- Uses the Recruit framework. Strategy: *Establish* (deploy depot, beacons on a chosen shore), *Convoy* (schedule
  escorts and protect the anchorage), *Strike* (small groups of elites: hover + air + naval gunfire, then retreat to
  repair), *Withdraw*.
- Tuning for few units: the AI must value units by cost (retreat at 40 % HP, keep reserves) rather than trade
  freely. Tiers 2-5 add micro: focus fire, kiting, sub hunting, carrier sorties.
- Fog-honest: sonar and plotting same as the human. Shipment timer visible to the AI only as the player sees it.
- Danger: an AI against sea-less maps must switch to air supply (Q1).
- LLM opponent summary adds: `shipment_eta`, `beachhead_radius`, `convoy_in_danger`, `naval_threat`.

## 8 · Art direction (atompunk 1962)
Palette: **deep navy, lagoon white, brushed chrome with copper trim**, signal orange for markings and landing lights,
reserve team colour on hull stripes and fin flashes. Forms: streamlined, finned and bubble-canopied; radomes,
turbine nacelles, glass cockpits; drawing-board curves rather than angular brutalism; ship superstructures like
1960s ocean liners with gun turrets; hovercraft with rubber skirts. Weathering light (they are new in the dust) but
tarnished by ash with a visible grey tidemark. Text on hulls in a clean grotesque, hull numbers white. UI/briefings:
teletype-printed *glossy* typed telex headers with a thin double rule (the Authority has stencils, the Directorate
loudspeakers). Sprites follow the same pilot protocol as D-088: one unit (Glide Tank) and one building (Landing Depot)
as the pilots.

## 9 · Items (for the lead, suggested split)
M7-02 contracts (terrain split, locomotion, flags, command, parts) · M7-03 ships + pathing · M7-04 landing/unload ·
M7-05 submerge/sonar · M7-06 supply line · M7-07 beachhead zone · M7-08 data roster · M7-09 carrier/air · M7-10 AI ·
M7-11 art fan-out · M7-12 superweapon · M7-13 maps/generator support · M7-14 balance.

## 10 · Questions (with recommendations)
1. **Landlocked maps.** Rec: the generator guarantees a coast for an Expedition player; fallback air supply
   (Air Head, smaller value) for hand-made maps. Alternative: disable the faction on landlocked maps.
2. **Can land vehicles cross shallows?** Rec: no (impassable), foot at 40 %, hover free, so navies and hover matter.
3. **Ship sizes.** Rec: 1-cell collision, visual 2-cell for L ships. Alternative: true multi-cell hulls.
4. **Faction unit cap.** Rec: soft cap 40 units (a "few but elite" lever); or none and rely on cost.
5. **Shipment decay.** Rec: as in 2.2; alternative: fixed value with a growing lull. Needs playtest.
6. **Superweapon.** Rec: Salvo Array. The Long Tide needs terrain mutation, so postpone.
7. **Veterancy.** Rec: introduce generic veterancy (3 levels) for all factions; or Expedition only.
8. **Water in the existing maps.** Rec: `water` alias for `deep`; old maps stay valid; no migration.
9. **Land faction vs ship targeting default.** Rec: `Ground|Naval` default so old weapons hit boats.
10. **Submarines.** Rec: keep in M7; if scope hurts, cut `exp_missile_sub` and `exp_minelayer` first.
