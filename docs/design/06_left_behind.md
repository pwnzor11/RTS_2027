# Design 06 · The Left Behind (third faction): design draft
From D-087 (new factions before the campaign) and D-086 (lore approval). Everything here is a **proposal for the
owner**; nothing is final until it is in DECISIONS. Ids use the prefix `lb_`. All numbers are starting values to be
marked `(tune)` in TOML; names are working names. Mechanics vocabulary follows design 05 (tunnels, radiation,
Blackout, hardening, upgrades, auras, transports, air, engineer, superweapon). Lore is in `docs/lore/left_behind.md`.

## 1 · Identity and play-style pitch
- **Who:** the descendants of the Glass Coast evacuees abandoned in 1990. Thirty-odd years on the dunes have made
  them nomads: ash-scavengers living on crawler-rigs, wearing cloth over sheet metal, running on what others threw away.
- **Feel:** a travelling town that is also an army. The whole base can pack up and leave; nothing is ever built twice.
- **Strengths:** mobility, a second income from wrecks, cheap and fast raids, ash and dust cover, field repair.
- **Weaknesses:** brittle and light armour, no hardened walls, lowest raw firepower per cost, a base that is
  vulnerable while it is packed, and an economy that starves if nobody is fighting.
- **Versus the others:** the Authority digs in and out-lasts; the Directorate hides below. The Left Behind refuse to
  stay anywhere, so the Authority's walls are wasted on them and the Directorate's tunnels are a rival road.

## 2 · Core mechanics (each: system extended, new sim work)

### 2.1 Rig bases (pack / unpack)
Core structures are **rigs**: a building that can pack into a slow, large vehicle and unpack elsewhere.
- *Extends:* the **Deploy** contract (`deploys_into`, design 05 sec. 4; land-train to Command Shaft is the existing
  one-way case) and the Directorate's mobile HQ idea. We add the reverse direction.
- *New sim work:* a `Pack` command (new `CommandKind`, appended) and `packs_into` on structures. Pack and unpack take
  `pack_ticks` (tune: 160 to 300); HP ratio carries over; production queues pause while packed and are kept; power
  is lost while packed; the footprint cells are cleared and re-validated on unpack (placement rules as for a Hatch).
  A packed rig is a `Locomotion.Tracked` actor with `packed_from` set. Determinism: integer HP ratio, fixed tick
  counts, no new randomness. Pathing and AI need "relocate base" logic (see sec. 8).
- *Counterplay:* a packing rig is vulnerable and cannot produce. Authority artillery and Directorate emergence
  punish bad timing. Rigs are never hardened, since hardening needs fixed enclosure (design 05 sec. 2).

### 2.2 Salvage (wrecks as a second income)
Destroyed vehicles and structures (any team) leave a **wreck** for `wreck_ticks` (tune: 1200 = 60 s).
- *Extends:* the `"ground"` state part (per-cell data with decay timers, design 05 sec. 3) and the harvester
  economy (Gleaner, Silo, refinery docking).
- *New sim work:* a `"wrecks"` entry in the ground part (cell, scrap value, ticks left; value = type cost times
  `salvage_percent`, tune 25 percent, floor and cap per type). A `salvage` order (new `MissionKind`) lets a Hauler or
  Tinker drain a wreck into its hopper over time; hoppers dock at the Yard or any refinery rig exactly as ash does.
  Value is integer permille, decays linearly, wrecks are cleared in deterministic cell order. Wrecks are
  non-blocking (view-level debris, sim entity not required) to avoid pathing churn.
- *Design intent:* the more the fighting goes on, near the Left Behind, the richer they get. They want fights, even
  lost ones, near their own rigs.

### 2.3 Raiding bonuses (plunder)
- *Extends:* the **Engineer** `Enter` rule (capture / repair) and the Silo/refinery stored-ash model.
- *New sim work:* `raider = true` on infantry and vehicles. A raider that `Enter`s an enemy **Silo or Refinery** below
  full storage steals `plunder_percent` (tune 20 percent, capped) of the stored ash and is consumed only if it is
  a one-use raider (Hook Runner survives and withdraws). A kill bonus `spoils_permille` pays the Left Behind a share
  of the victim's cost when a raider-flagged unit kills a harvester. A new `"effects"` entry tracks per-actor
  withdraw state. All integer arithmetic; no float.
- *Counterplay:* defended silos, gates, and killing Hook Runners (fragile) before they enter.

### 2.4 Dust veil and ash-cloaks
- *Extends:* **Blackout** (`blackout_ticks`, `sees_through_blackout`, design 05 sec. 3) and **Stealth** (Informant,
  `stealth_delay_ticks`) and **radiation resist** (`rad_resist_permille`).
- *New sim work:* only data plus one small rule: stealth on a Veilwalker is shortened (`stealth_delay_ticks` 30)
  while standing on a cell with ash density above a threshold (reads the existing ash field). Dust Sail vehicles
  drop small Blackout clouds. Left Behind infantry carry partial radiation resistance (rad_resist 300 to 450); the
  Ash Wraps upgrade raises it. No new part; the ash-density lookup is read-only.

### 2.5 Patchwork field-mods
- *Extends:* **Upgrades** (`upgrades.toml`, `ProductionCategory.Upgrade`) and **auras** (`aura_radius` family).
- *New sim work:* a **Tinker** (engineer-flagged) aboard a vehicle or beside a rig lends an `aura_repair_permille`
  heal-over-time aura (new field in the aura recompute, every 10 ticks, no new part). Upgrades are cheap, faster to
  research (about half Authority build times) and several apply to already-built units by writing bits in the
  existing upgrade mask. No new unit data structures.

### 2.6 Convoy transports
- *Extends:* **Transports** (`passengers`, `Enter`, `Deploy` to unload). The Tow Truck carries six infantry and can
  also tow a packed rig at normal speed (new `tow_bonus_permille` aura on the packed rig; reuses aura multipliers).
- *New sim work:* minimal; aura targeting a packed rig actor.

## 3 · Economy
- **One currency (ashfall concentrate) with two sources.** Gleaners scoop ash from drifts and haul to a Refinery Rig
  (as Authority scrapers do). Haulers and Tinkers drain wrecks into the same hopper. A **Yard** (fixed building)
  is the cheap tier-one drop-off, while the **Refinery Rig** can pack and follow the army to a new field.
- **Income shape:** lower base ash yield than the Authority (Gleaner hopper 60 percent of a Scraper), but wrecks and
  plunder make mid-fight income spike. Starving is possible in a peaceful game: this is the intended pressure to raid.
- **Power:** Dynamo Rigs (burn scrap and ash) are cheap but small (low output each) and pack up. Losing power slows
  production but does not stop the packed-rig mobility.
- **Costs:** structures cheap, units cheap, upgrades cheap, but build times are uneven so the player is rewarded for
  moving rather than turtling (tune).
- **Start kit (proposal):** `lb_mother_rig` (packed) + 2 Scavengers + 1 Gleaner.

## 4 · Three-tier tech tree
| Tier | Unlocks | Gate (building) |
|---|---|---|
| **T1 Camp** | Scavenger, Hook Runner, Gleaner, Buggy, Hauler; Yard, Dynamo Rig, Muster Rig, Spike Nest, Scrap Wall | Mother Rig |
| **T2 Caravan** | Slinger, Tinker, Harpoon Wagon, Organ, Tow Truck, Dust Sail; Garage Rig, Lookout Mast, Harpoon Post, Flak Net; upgrades Scrap Plate, Ash Wraps, Quick Pack | Garage Rig + Lookout Mast |
| **T3 Convoy** | Veilwalker, Rolling Fort, Rotor, Kite; Rotor Pad, Tinker's Bench (tech); upgrades Grapple Hooks, Salvage Rights, Patchwork; **The Second Drift** | Tinker's Bench + Rotor Pad |

## 5 · Roster
Size class S / M / L follows the existing footprint convention (design 05 structure column). Counters are
rock-paper-scissors hints for the balance pass, not guarantees.

### Infantry (6)
| Id | Name | Role | Strong vs | Weak vs | Size |
|---|---|---|---|---|---|
| lb_scavenger | Scavenger | Cheap rifle infantry, can salvage wrecks slowly | Light infantry | Flame, artillery | S |
| lb_hook_runner | Hook Runner | Fast raider, steals from silos, survives | Silos, harvesters | Defended bases | S |
| lb_slinger | Slinger | Thrown shaped charge, anti-armour | Vehicles | Infantry, fast units | S |
| lb_tinker | Tinker | Engineer + repair aura + salvage | Sustained fights | Anything shooting | S |
| lb_veilwalker | Veilwalker | Stealth scout / sniper in ash | Isolated targets | Searchlight, detectors | S |
| lb_drifter_brute | Drifter Brute | Heavy flame-hammer infantry, tanks the dust | Light infantry, buildings (low) | Armour | S |

### Vehicles (9)
| Id | Name | Role | Strong vs | Weak vs | Size |
|---|---|---|---|---|---|
| lb_gleaner | Gleaner | Ash harvester (light hopper) | n/a | Everything | M |
| lb_hauler | Salvage Hauler | Wreck collector, wreck-to-hopper | n/a | Everything | M |
| lb_buggy | Dune Buggy | Fast scout with a light gun, raider-flagged | Harvesters | Armour | S |
| lb_harpoon_wagon | Harpoon Wagon | Anti-armour, tethers (slows) targets | Vehicles | Infantry | M |
| lb_organ | Organ | Rocket-rack artillery, area, inaccurate | Groups, structures | Fast units | M |
| lb_tow | Tow Truck | Transport (6) + tows packed rigs | Logistics | Everything | M |
| lb_dust_sail | Dust Sail | Wind-rigged Blackout layer, support | Detection denial | Air, artillery | M |
| lb_rolling_fort | Rolling Fort | Heavy patchwork tank, armour of mixed plate | Mid-game push | Mass anti-armour | L |
| lb_rig_crawler | Packed Rig (generic) | Packed form of any rig (see 2.1) | n/a | Everything | L |

### Air (2)
| Id | Name | Role | Strong vs | Weak vs | Size |
|---|---|---|---|---|---|
| lb_rotor | Rotor | Autogyro light gunship, low altitude | Infantry, harvesters | Flak, rocket nests | M |
| lb_kite | Kite | One-use glider bomber; death weapon on impact | Structures, clusters | Any anti-air | S |

### Structures and rigs (12 + 1 superweapon)
| Id | Name | Role | Packs? | Size |
|---|---|---|---|---|
| lb_mother_rig | Mother Rig | Conyard, builds everything | Yes | L |
| lb_yard | Salvage Yard | Fixed refinery and silo, cheap | No | M |
| lb_refinery_rig | Refinery Rig | Mobile refinery | Yes | M |
| lb_dynamo_rig | Dynamo Rig | Power | Yes | M |
| lb_muster_rig | Muster Rig | Infantry production | Yes | M |
| lb_garage_rig | Garage Rig | Vehicle production | Yes | L |
| lb_lookout | Lookout Mast | Radar + minimap jam (reuses jam) | Yes (T2 upgrade) | S |
| lb_bench | Tinker's Bench | Tech, researches upgrades | No | M |
| lb_rotor_pad | Rotor Pad | Aircraft production | No | M |
| lb_spike_nest | Spike Nest | Basic defence | No | S |
| lb_harpoon_post | Harpoon Post | Anti-armour defence | No | S |
| lb_flak_net | Flak Net | Anti-air defence | No | S |
| lb_scrap_wall | Scrap Wall | Cheap wall piece | No | S |
| lb_second_drift | The Second Drift | **Superweapon** (see 6) | No | L |

### Upgrades (6)
| Id | Name | Effect | Researched at |
|---|---|---|---|
| lb_up_scrap_plate | Scrap Plate | +armour for vehicles | Bench |
| lb_up_ash_wraps | Ash Wraps | Infantry radiation resist up | Bench |
| lb_up_quick_pack | Quick Pack | Rig pack time -40 percent | Bench |
| lb_up_grapple | Grapple Hooks | Hook Runner steals more, breaks walls briefly | Bench |
| lb_up_salvage_rights | Salvage Rights | Wreck value x1.5, hauls faster | Bench |
| lb_up_patchwork | Patchwork | Tinkers also repair vehicles at range | Bench |

**Counts:** infantry 6, vehicles 9 (incl. the packed-rig actor), air 2, structures 13 plus 1 superweapon, upgrades 6.

## 6 · Superweapon: The Second Drift
A ground-effect storm rather than a blast, in keeping with the faction. Long charge (tune: 1200 s of game
time), fired with `Special` on a target cell.
- *Extends:* `superweapon = <id>` (design 05 sec. 4), **Blackout** (`blackout_ticks`) and radiation (`contaminate`).
- *Effect:* for 40 s (tune) a large circular ash storm sits on the target. It applies Blackout to the area, applies
  `contaminate` in low steps (never above level 64, so it denies building but does not kill outright), and slows
  vehicles via a movement-speed aura. Left Behind units inside gain stealth bonus and are immune to the slow.
- *New sim work:* a persistent "storm" effect with a centre, radius and ticks remaining, recomputed every 10 ticks
  in the existing effects part; no projectile. Counterplay: Decon Trucks, Searchlights (see through Blackout).

## 7 · Interactions with existing systems (check list)
- **Tunnels:** Left Behind units cannot use Directorate tunnels. The Seismograph still reveals nothing of them.
- **Hardening:** rigs are never hardened; a packed rig inside an enemy enclosure can still unpack only on free soft cells.
- **Radiation:** partial resist for infantry, none for vehicles; Decon is not required for the faction.
- **Gates:** Left Behind units treat enemy gates as walls, as Directorate does.

## 8 · AI personality notes
- **Archetype:** the *Raider Warlord*. Wins by harassing economies and relocating before retaliation.
- **Behaviour:** early Buggies and Hook Runners toward the nearest enemy Silo; keeps the Mother Rig packed when threatened;
  prefers to unpack beside a wreck field or a fresh drift; retreats rather than trades evenly.
- **Tier ladder (five tiers, design 05):** low tiers raid only; mid tiers add Slinger/Harpoon pushes and relocation; top
  tier combines Dust Sails with Kites and chains The Second Drift with a raid.
- **LLM leader (D-087):** speaks as an elder of the caravans (see lore); strategy hooks: raid / relocate / defend /
  mass / drift. Orders go through the normal command log (D-051).
- **New AI work:** relocate-base planner (pick a destination cell, check free footprint and safety), salvage scheduling
  for Haulers, wreck-aware target choice.

## 9 · Art direction
- **Palette words:** dust ochre, sun-bleached canvas, rust red, faded teal paint, ash grey, bone, a little hazard yellow.
  Never clean. Warm where the Authority is cold green-grey and the Directorate is red and bone-white.
- **Silhouettes:** wide, lopsided, stacked. Rigs read as a pile: a flatbed chassis with lean-to roofs, stovepipes,
  awnings and antenna bundles. Vehicles have asymmetric patch plating and lashed-on barrels. Infantry are layered:
  cloth wraps, goggles, respirators made from filter tins; tall silhouettes with a hood or mast-flag.
- **Materials:** salvaged road signs, bus-body sheet metal, tarpaulin, sailcloth, cable and rope, glass lenses.
  Rivets and welded patches show different metal tones. Hand-painted numerals and family marks (no logos).
- **Atompunk consistency (1962):** period vehicles (old trucks and buses, tube radios, bakelite dials) pushed
  into survival mode; reactor-era parts reused in odd places (a lantern powered by a spent fuel pellet).
- **Faction readability:** warm yellow-ochre team colour accent on canvas flags; each rig has a unique flag set.
- **Sound hint (for later):** wind, flapping cloth, clanking chains, an out-of-tune accordion on the rig's loudspeaker.

## 10 · Open questions for the owner (Q-drafts)

**Q-A · Should a packed rig be able to produce?**
Options: (1) no, production pauses while packed (recommended); (2) infantry only; (3) full production (strongest,
risks removing all downside of moving).
Recommendation: option 1. Keeps packing a real cost and is simplest to implement.

**Q-B · Wreck lifetime and visibility.**
Options: (1) 60 s, visible to all (recommended); (2) 30 s, only to the Left Behind; (3) permanent until salvaged.
Recommendation: option 1. Visible to all so enemies can choose to deny wrecks, giving counterplay.

**Q-C · Air roster.**
Options: (1) Rotor and Kite (as drafted, recommended); (2) Kite only (cheaper to build, avoids overlap with
Authority air); (3) no air (strengthens the ground identity, leaves the faction exposed to no flyer-counters).
Recommendation: option 1, but review in art after the Directorate/Authority air pass.

**Q-D · Superweapon style.**
Options: (1) The Second Drift ground storm, non-lethal denial (recommended); (2) a more standard blast weapon;
(3) a summoned allied raid (many Kites).
Recommendation: option 1. Differentiates from the Sunrise Device and the Authority artillery; fits the setting.

**Q-E · Tunnel interaction.**
Options: (1) none, they cannot use or see tunnels (recommended); (2) can capture a Hatch; (3) can plunder
Directorate Smelter Vents.
Recommendation: option 1 for M6, revisit after balance.

**Q-F · Campaign role.**
Options: (1) a playable third campaign; (2) a mid-campaign ally of the Authority or Directorate; (3) skirmish only.
Recommendation: decide later (the campaign is written once with all factions, D-087), but keep names and
backstories in the Archive from day one.

**Q-G · Naming "Left Behind" in-universe.**
Options: (1) the faction calls itself "the Left Behind" with pride (recommended); (2) uses "the Caravans" and others
call them "the Left Behind"; (3) a different formal name.
Recommendation: option 1 or 2; either fits the lore draft.

**Q-H · Faction prefix.** Confirm `lb_` for ids and `[faction.left_behind]` in game.toml. Recommendation: yes.
