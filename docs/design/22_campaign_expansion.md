# Design 22 · Campaign expansion: 4 to 12 missions per faction (M16-C-02, D-333)

Status: **outline for the owner to review**. No code. Every story choice below is a proposal (assumed — confirm) until the owner
answers section 1; nothing here changes a built mission. Names are existing lore names only (Q-G, D-154); places come from
docs/lore. All numbers are `(tune)`. One pilot mission (section 6) is built afterwards by another package from this outline.

Inputs: D-030 (10-12 per side, one spine, 2-3 choice points that rejoin, map reuse, mission mix about 1/2 build-and-destroy,
1/4 defend/escort, 1/4 commando/no-base), D-087 (written once for four factions), D-154/D-155 (one shared 2001 told four ways,
own hero each, own victory and ending, Left Behind hostile to all), D-163 (the A1 template), D-164 (writer choices kept),
D-169 (act stills), D-222 (ash on; Q-75 win targets), D-271 (Field Manual approved as drafted), D-284 (in-universe text,
telegram briefings), D-289 (par = bot tier-3 median x 2.5). Design 12 (§2-3), the four campaign pages in docs/missions, the
mission files in data/missions and data/campaigns, docs/missions/api.md, docs/lore.

## 1 · Summary and owner questions

**The proposal in one paragraph.** Each campaign grows to **12 missions in four acts of three** (the schema's `act` field is
already 1..4). The four built missions stay where they are in the story and keep their files: mission 1 opens act 1, mission 2
opens act 2, mission 3 sits in the middle of act 3, mission 4 is still the finale. That gives 8 new missions per faction, 32 in
all, every one slotted so that the tech climb the built missions expect still holds (section 2). The new missions do three jobs:
(1) they fill the big tech jumps (auth_01 unlocks one Turret, then auth_02 hands over factory, tanks, engineers and rocketeers at
once), introducing each item in story; (2) they add the mission types the built campaign lacks: there is **no true no-base
commando mission today**, and D-030 wants a quarter of them; (3) they turn the leaders' Archive "secrets" (docs/lore/leaders.md,
expedition.md, left_behind.md) into mission beats: Dray's defection, Fenn's sealed depot, Tern Point, Kell's rescue flight,
Larkin's rewritten order, Order Seven's buried warheads. Two missions per faction are marked as the **cut line**, so the owner
can stop at 10 without reshaping anything. Two slots per faction are marked for C-03 choice points, built later.

### Owner questions (numbered; options with trade-offs; my recommendation last)

**Q1 · Length per faction.**
(a) 10 per faction: 24 new missions, acts of 3/3/2/2; smaller, but act 4 stays as thin as today.
(b) 12 per faction: 32 new missions, four even acts of three; the most story room, the most work (about 32 x 10-12 human-equivalent
hours by the pilot report, plus sweeps).
(c) Design 12, build in act waves, with two cut-line missions per faction (marked below) dropped if the owner wants 10.
**Recommendation: (c).** The outline costs the same either way, and the cut line is chosen so dropping it moves no tech.

**Q2 · Do the four campaigns interlock?**
(a) Stay parallel, as now (D-154): each story stands alone, no shared events beyond the four acts.
(b) Mirrored pairs: some battles are played from both sides on the same map (e.g. Tern Point by the Authority and by the Left
Behind), cameo radio lines cross, and Archive crossings reward playing more than one campaign. No result carries between campaigns,
so there is still no canon winner.
(c) Hard interlock: what you did in one campaign changes another (progress flags). Contradicts D-154's "no canon winner" and
multiplies testing.
**Recommendation: (b).** It is what D-030's map reuse already does (auth_01 and lb_01 are the same convoy); section 4 lists the pairs.

**Q3 · When does Lieutenant Dray defect?** (03_setting names "a defector mid-campaign"; today he only appears in auth_04.)
(a) Act 2 (Authority mission 5, "The Defector"): he becomes a recurring voice for acts 2-4 and drives two later missions. The
auth_04 briefing line "LIEUTENANT DRAY HAS CROSSED TO OUR SIDE" still reads true; a later package may soften it to "WILL SPEAK ON
THE RADIO".
(b) Act 4 (mission 10, just before the finale): matches auth_04's wording exactly, but he is a one-act character.
(c) Act 3: a middle path; the pilot would move to the Glass Coast map.
**Recommendation: (a).** The pilot (section 6) is written for (a); with (b) it moves to ash_reckoning and grows from S to M.

**Q4 · New unlocks and the built missions.** The campaign tech set carries forward (CampaignService.AllowListFor adds it to every
later mission's allow-list), so anything a new mission unlocks also appears in the built missions after it.
(a) New missions may unlock roster items no mission uses today (auth_seismic_mortar, auth_blockhouse, auth_foam_truck, dir_volunteer,
dir_agitprop, dir_delver, dir_ash_sower, dir_informant, dir_redoubt, dir_bunker, lb_tow, lb_dust_sail, lb_scrap_wall,
exp_barrage_lander, exp_repair_hover, exp_minelayer, exp_picket, exp_canister_skiff, exp_gyro, exp_survey_hover, exp_pioneer), and the
built missions after them are re-swept to the Q-75 targets (12 seeds, Pi cluster, D-253).
(b) No leaks: new missions only introduce items the next built mission already starts with; the unused roster stays skirmish-only.
(c) Re-spread the whole climb so every mission unlocks 1-3 items, editing the built missions' starts_with too (cleanest curve; every
built mission retuned).
**Recommendation: (a).** About twenty units and buildings never appear in a campaign today; the sweeps are cheap on the cluster.

**Q5 · Upgrades in the campaign.** As far as I read the code, upgrades are **not** mission-gated today: `Simulation.CanResearch`
does not consult `UnlockFilter`, so any upgrade whose prerequisites are met can be researched in any mission.
(a) Leave it: upgrades are free in missions.
(b) Gate upgrades like units (small code change: CanResearch asks the filter; the mission allow-list holds upgrade ids) and make
upgrades the reward of several new missions.
(c) Gate only tier-3 upgrades.
**Recommendation: (b)**, as its own small package before the new missions; the outline lists upgrade rewards marked "(if Q5 b)".

**Q6 · Maps.** Three story maps exist (rustbelt_m2 64x64, glass_coast_e1 96x96, ash_reckoning 96x96); story maps stay hand-made
(design 16 §3.6), made by freezing a generated map and editing it.
(a) Reuse the three only, with new zones and starts: cheapest; 12 missions per faction on three maps gets repetitive.
(b) Add three frozen-and-edited maps: **rustbelt_mill** (rust_belt, 64x64, a ruined mill town on the rail line), **tern_point**
(glass_coast, 64x64, the 1990 railhead on the shore), **annex_foothills** (mountains, 96x96, the old shafts under the Annex range).
Each is used by 3-6 missions across factions.
(c) A new map per act and faction (16+ maps): best variety, the largest cost.
**Recommendation: (b).** The mountains biome is unused by the story so far and is where the Annex is.

**Q7 · Choice points (C-03, later).** D-030 asks for 2-3 two-way choices per spine that rejoin.
(a) None in this expansion; decide later.
(b) Reserve two slots per faction now (mission 6 and mission 10, marked below), each a natural dilemma; the alternative mission is
written and built in C-03, rejoining at the next mission.
(c) Build one choice per faction now with the expansion.
**Recommendation: (b).** The slots cost nothing now, and design 12 §3.6's `next` table is enough to wire them later.

**Q8 · When do the Left Behind and the Expedition open?** Today: after auth_02/dir_02 and after auth_03/dir_03 (`open_after`).
In the 12-mission order those are missions 4 and 8.
(a) Keep the ids: no data change; the new factions open a little later in play hours.
(b) Move to the new act ends (missions 6 and 9).
(c) All four open from the start.
**Recommendation: (a).**

**Q9 · Leader secrets as mission beats.** Several new missions are built around an Archive secret (Fenn's depot, Aldersey's log,
Kell's flight, Larkin's rewritten orders, Harrowgate's death, Roque's cores, Strand's recall orders).
(a) Use them openly in briefings and radio.
(b) Use them as beats, but on screen only hint; the Archive entry, unlocked by that mission, states the secret.
(c) Keep secrets Archive-only; write other beats.
**Recommendation: (b).** It rewards the Archive and keeps the endings' reveals intact.

## 2 · Structure rules (apply to every faction)

- **Play order and acts.** Act 1 = missions 1-3 (spring 2001), act 2 = 4-6 (early summer), act 3 = 7-9 (late summer/autumn),
  act 4 = 10-12 (autumn/winter). Built missions sit at **1, 4, 8, 12** in every campaign, so the cross-faction timeline lines up.
- **Ids.** Built ids stay (progress files, Archive unlocks, ending conditions and `open_after` name them). New ids continue the
  numbering in play order: `<faction>_05` .. `<faction>_12`; locale prefixes follow the id number (a5..a12, d5..d12, l5..l12,
  e5..e12). The campaign list file, not the id, sets play order.
- **Tech slot-in rule.** A new mission's `starts_with` is a subset of what the previous mission ends with. A new mission placed before
  a built one introduces items the built one already starts with (no change there). New unlocks outside that set follow Q4.
- **Stills (D-169).** The act still moves to each act's first mission: act 2 stays on the built mission 4; act 3 moves from
  `<f>_03` to mission 7; act 4 from `<f>_04` to mission 10. One line each in data/campaigns, done by the build packages.
- **Mission shape.** Every new mission follows the D-163 template: one leader on the radio, primary objectives plus one hidden
  bonus, at least one visible timer or clear clock, enemy pressure by unit count and AI tier only (D-061), bot test at tiers 3 and
  5, 12-seed sweep to the Q-75 targets (tier 1 >= 10/12, tier 3 >= 8/12, tier 5 >= 3/12), par set after measurement (D-289).
- **No-base missions.** A player who never had a base is never defeated by the sim (EverHadBase), so commando scripts must lose
  explicitly with `Trigger.OnAllDestroyed(0, ...)`.
- **Curve.** The built missions are fixed points. New missions step between them: a breather (S) right after a built mission, a
  ramp before the next one. Curve column: Intro, Low, Mid, Mid+, High, Peak.
- **Size.** S = reused map, under 150 lines of Lua, no new mechanics; M = reused or new map, 150-250 lines, two fronts or phases;
  L = new map plus a mechanic not yet used in a mission. No L is proposed; three new maps are counted separately.

## 3 · The four campaigns

Columns: play position, id, title, type (B&D build-and-destroy, D/E defend or escort, CMD commando/no-base), map, unlocks on win,
curve, size. **Cut** = cut-line candidate for a 10-mission campaign. **C-03** = reserved choice slot.

### 3.1 Authority (hero Brigadier Wrenfield; Pell by teletype; Fenn, Dray)

| # | Id | Title | Type | Map | Unlocks on win | Curve | Size |
|---|---|---|---|---|---|---|---|
| 1 | auth_01 | Survey Convoy (built) | escort, then build | rustbelt_m2 | auth_turret | Intro | built |
| 2 | auth_05 | Hatch Country **(Cut)** | CMD | rustbelt_m2 (north) | auth_engineer, auth_rocketeer | Low | S |
| 3 | auth_06 | The Mill Town | B&D | rustbelt_mill | auth_tank, auth_scraper, auth_silo, auth_ration_hall, auth_seismograph | Low | M |
| 4 | auth_02 | The Junction (built) | defend | rustbelt_m2 | auth_engineering (T2) and four more | Mid | built |
| 5 | auth_07 | The Defector (**pilot**) | CMD / escort | rustbelt_m2 | auth_seismic_mortar (Q4 a) | Mid | S |
| 6 | auth_08 | Shaft Four **(C-03)** | B&D | rustbelt_mill | auth_blockhouse, auth_foam_truck (Q4 a); auth_up_sodium_lamps (Q5 b) | Mid+ | M |
| 7 | auth_09 | Fenn's Map **(Cut)** | CMD | glass_coast_e1 (south dunes) | auth_up_flak_vests (Q5 b) | Mid | S |
| 8 | auth_03 | The Coast Road (built) | escort, two fronts | glass_coast_e1 | auth_signals_hq (T3) and seven more | Mid+ | built |
| 9 | auth_10 | Tern Point | B&D | tern_point | auth_up_composite_armour (Q5 b) | High | M |
| 10 | auth_11 | The Buried Warheads **(C-03)** | CMD / escort | ash_reckoning (north bank) | auth_up_tungsten_shot (Q5 b) | High | M |
| 11 | auth_12 | The Ford | B&D | ash_reckoning (north bank) | auth_up_signals_net (Q5 b) | High | M |
| 12 | auth_04 | The Sunrise Device (built, finale) | B&D | ash_reckoning | auth_atomic_battery | Peak | built |

Mix: B&D 6 (01, 06, 08, 10, 12, 04), D/E 3 (02, 03, 07), CMD 3 (05, 09, 11). Matches D-030.

- **auth_05 Hatch Country.** The convoy held, but the raiders came from nowhere. Wrenfield sends a recon column (jeeps, riflemen,
  the first combat engineer) into the hatch country north of the pit to find where. Three surface hatches and a loudspeaker on a
  pole: close the hatches (engineer capture or demolition), knock down the loudspeaker that reads out the column's name. No base;
  a jeep that strays is lost to the ash. Introduces the engineer and the rocketeer that auth_02 hands over today.
- **auth_06 The Mill Town.** A Directorate forward shift has dug in under a ruined mill town on the rail line. Pell needs the rail
  line for the winter grain. First full base since the survey pit: factory, the first tanks, scrapers on a pitchblende spur, a ration
  hall for the supply cap. Destroy the shift's hatches and vents; hidden bonus: the town's old grain elevator, still standing, kept
  intact. Sets up auth_02's "grain and pitchblende on one road".
- **auth_07 The Defector.** A Directorate engineer signals by lamp from the Shaft Four spoil heaps: Lieutenant Anselm Dray, unarmed,
  carrying a reactor log. Bring him across the river before the Commissariat misses him. Full spec in section 6.
- **auth_08 Shaft Four.** Dray's log gives the tunnel bearings. The army goes underground's front door: break the Shaft Four tunnel
  heads around the mill town, with moles surfacing behind the lines, first tier-2 fight (Engineering, marksmen, searchlights).
  Pell's teletype admits, without saying why, that "the office has known of a carrier signal for some time" (Q9 b: the 1974 hum file
  is the Archive unlock). **C-03:** storm the shaft head now (this mission) or seal it and starve it (alternative B, a defend
  mission), rejoining at auth_09.
- **auth_09 Fenn's Map.** Chief Surveyor Fenn walks the launch-site dunes ahead of the army, as she did in 1990. A survey party
  (Fenn as an engineer, a carrier, riflemen) must reach four survey markers; Left Behind watchers shadow them (hostile cameo,
  Calloway-Ross on their band). The last marker is a sealed Authority depot that is on no map (Fenn's secret, Q9 b). Ties into
  auth_03's hidden "Fenn's survey cache".
- **auth_10 Tern Point.** The coast road is open, and raiders strike it from the old Tern Point railhead, where the Authority's
  last 1990 convoy never returned. Pell orders the railhead cleared; Wrenfield goes in person. Left Behind bands and a Directorate
  salvage team hold the stranded trains; first howitzers and airfield. Hidden bonus: the platform ledger, the names list. It sets up
  the ending slide where Wrenfield reads the four thousand names. Wrenfield's brother is never named (auth campaign note 3).
  Mirror of lb_10.
- **auth_11 The Buried Warheads.** Dray knows where Order Seven's warheads lie: three vaults under the old launch sites by the
  Reckoning river (his secret). Escort his engineers to each vault and hold an engineer on the vault for a count (`OnHold`) while
  Directorate bore crews race to arm them. Mirror of dir_12. **C-03:** disarm the vaults (this) or let them be and strike the device
  directly (alternative B, a shorter, harder auth_04 setup).
- **auth_12 The Ford.** The device sits across the river. Break the Directorate rocket-nest line on the north bank, seize the ford,
  and get the first six units across: the start state auth_04 already assumes. First real air fight: interceptors against
  dirigibles.

### 3.2 Directorate (hero the Voice, Odette Larkin; Thrall; Lund)

| # | Id | Title | Type | Map | Unlocks on win | Curve | Size |
|---|---|---|---|---|---|---|---|
| 1 | dir_01 | The First Hatch (built) | defend | rustbelt_m2 | dir_sapper | Intro | built |
| 2 | dir_05 | Night Shift **(Cut)** | CMD | rustbelt_m2 (north) | dir_smelter_vent, dir_scraper | Low | S |
| 3 | dir_06 | The Assembly Bore | B&D | rustbelt_mill | dir_assembly_bore, dir_rocket_truck, dir_cistern, dir_bunk_gallery | Low | M |
| 4 | dir_02 | The Convoy Roads (built) | B&D raid | rustbelt_m2 | dir_isotope_works (T2), dir_mole, dir_rocket_nest | Mid | built |
| 5 | dir_07 | Shaft Seven | defend | annex_foothills | dir_volunteer (Q4 a); dir_up_tunnel_bracing (Q5 b) | Mid | M |
| 6 | dir_08 | A Redistribution of Effort **(C-03)** | CMD / escort | annex_foothills | dir_informant (Q4 a) | Mid | S |
| 7 | dir_09 | The Glass Road | B&D | glass_coast_e1 (west) | dir_agitprop (Q4 a); dir_up_lead_lining (Q5 b) | Mid+ | M |
| 8 | dir_03 | The Launch Sites (built) | two fronts, hold | glass_coast_e1 | dir_commissar, dir_rad_mortar, dir_mast, dir_mooring, dir_dirigible | Mid+ | built |
| 9 | dir_10 | Strangers on the Dunes **(Cut)** | B&D | tern_point | dir_redoubt, dir_bunker (Q4 a) | High | M |
| 10 | dir_11 | Fuel for the Fleet **(C-03)** | B&D | ash_reckoning (south shore) | dir_delver, dir_ash_sower (Q4 a); dir_up_hot_cores (Q5 b) | High | M |
| 11 | dir_12 | Order Seven | escort / tunnel | ash_reckoning | dir_up_blackout_canisters (Q5 b) | High | M |
| 12 | dir_04 | The Sunrise (built, finale) | hold, then fire | ash_reckoning | dir_committee_hall (T3), dir_sunrise and three more | Peak | built |

Mix: B&D 5 (02, 06, 09, 10, 11), D/E 5 (01, 03, 07, 12, 04), CMD 2 (05, 08). One short on commando; dir_10 can become a no-base
dirigible raid if the owner wants the exact D-030 mix.

- **dir_05 Night Shift.** The Voice reads a lullaby and a shift list. One conscript shift and two sappers surface at night to cut the
  tenancy's power: three Authority power plants feed a searchlight line along the road. No base; the hatch closes behind them.
  Hidden bonus: bring every sapper home. Introduces the smelter vent and scraper economy dir_02 starts with.
- **dir_06 The Assembly Bore.** First full shift on the surface. Dr. Perpetua Lund speaks for the first time (the Isotope Works
  chief, "keep the reactors warm"): build the smelter vent, cistern, bunk gallery and assembly bore under a scrap field in the mill
  town and clear the Authority survey camp that has found it. First rocket trucks. Mirror of auth_06 (same town, other side).
- **dir_07 Shaft Seven.** Shaft Seven flooded in 1994 and its reactor failed. Lund wants its core: hold the shaft head in the
  mountain foothills while moles drag the core out against an Authority column. Halfway through, Thrall announces on every speaker
  that "an engineer of Shaft Four has walked into the light" (Dray, mirror of auth_07) and that informants will be posted.
- **dir_08 A Redistribution of Effort.** A battalion that would not advance is to be "redistributed". The Voice reads the order, and
  then a weather report that tells the player, in plain words to anyone listening, to bring the battalion home the long way (Larkin's
  secret, Q9 b). Escort the conscripts to a far hatch past Authority patrols and Commissariat informants (a scripted hostile slot).
  Hidden bonus: the informants' notebook. **C-03:** follow the broadcast (this) or follow the order (alternative B, a grim B&D
  against the battalion's position; Larkin's ending line changes).
- **dir_09 The Glass Road.** The bore line must reach the Glass Coast, but bores cannot surface through the fused glass. Find soft
  ground (scout zones), drive the line across, and destroy the Authority coast-watch relays that would warn the pads. A Left Behind
  caravan plays a Directorate broadcast back with the words changed (the Kettle's retuned speaker, cameo). First Agitprop Crawler.
- **dir_10 Strangers on the Dunes.** The caravans are hunting the loudspeaker crawlers. Break the band camped on the Tern Point
  railhead and recover the speakers; first dirigibles over the dunes. Mirror of lb_09. Cut-line: dir_03's unlocks cover dir_11.
- **dir_11 Fuel for the Fleet.** Commissioner Strand asked the Directorate to leave the fleet alone (dir_03). Thrall does not agree:
  destroy the Expedition's dredge works and beachhead on the ash plain before its carrier group arrives. Dirigibles against SAM
  mounts; Lund asks on the radio what the fuel is for. **C-03:** strike the fleet (this) or let it dredge and take the fuel later
  (alternative B: raid a loaded tanker convoy instead).
- **dir_12 Order Seven.** The Sunrise Device needs a finished hatch near its target (dir_04's rule). Drive the tunnel line under the
  river to the south bank: escort bores through Authority patrols and stand up the tunnel-head hatch that dir_04 opens with. The
  committee that writes the Director's addresses is hinted (Harrowgate's secret, Q9 b). Mirror of auth_11.

### 3.3 Left Behind (hero war-captain Jessup Calloway-Ross; Speaker Marrow Tindall; Old Ottoline Vane)

D-155 order kept: the Authority in acts 1-2, the Directorate in act 3, the Expedition in act 4; victory is taking over.

| # | Id | Title | Type | Map | Unlocks on win | Curve | Size |
|---|---|---|---|---|---|---|---|
| 1 | lb_01 | The Refused (built) | ambush, B&D | rustbelt_m2 | lb_garage_rig | Intro | built |
| 2 | lb_05 | Under One Banner | B&D | rustbelt_m2 (west) | lb_buggy, lb_gleaner | Low | M |
| 3 | lb_06 | Salvage Rights **(Cut)** | CMD (economy) | rustbelt_mill | lb_hauler, lb_lookout (T2) | Low | S |
| 4 | lb_02 | Road Posts (built) | B&D | rustbelt_m2 | lb_bench, lb_harpoon_wagon, lb_tinker, lb_drifter_brute | Mid | built |
| 5 | lb_07 | A Name for the Brigadier | ambush / escort | rustbelt_m2 | lb_tow (Q4 a); lb_up_ash_wraps (Q5 b) | Mid | S |
| 6 | lb_08 | Kettle Flat **(C-03)** | defend / evacuate | rustbelt_m2 (south) | lb_scrap_wall (Q4 a); lb_up_quick_pack (Q5 b) | Mid+ | M |
| 7 | lb_09 | Strangers with Loudspeakers **(Cut)** | CMD raid | glass_coast_e1 | lb_dust_sail (Q4 a) | Mid | S |
| 8 | lb_03 | Hatch Field (built) | two fronts | glass_coast_e1 | lb_rotor_pad (T3) and four more | Mid+ | built |
| 9 | lb_10 | The Platform | defend, two fronts | tern_point | lb_up_scrap_plate (Q5 b) | High | M |
| 10 | lb_11 | The Ones Who Flew **(C-03)** | B&D | glass_coast_e1 (north shore) | lb_rotor, lb_kite, lb_flak_net | High | M |
| 11 | lb_12 | The Second Drift | B&D | ash_reckoning (coast) | lb_second_drift; lb_up_grapple (Q5 b) | High | M |
| 12 | lb_04 | The Banner (built, finale) | B&D | ash_reckoning | (finale) | Peak | built |

Mix: B&D 6 (01, 05, 02, 11, 12, 04), D/E 4 (07, 08, 10, 03), CMD 2 (06, 09).

- **lb_05 Under One Banner.** Not every band answered the war-captain. The rig *Nobody's Daughter* and two others hold a well at
  the west end of the Belt and fly their own flags. Bring them under the banner: break their dynamo and muster rigs (an enemy Left
  Behind player, scripted). Tindall asks on the radio that their names stay on the flag. Hidden bonus: take their Mother Rig
  without destroying it. Shows the infighting of 1996-2000 and the warlord's ruthlessness.
- **lb_06 Salvage Rights.** An Authority supply train lies wrecked on the rail line by the mill town. Haulers strip it (salvage
  order) before the recovery crews and their escort arrive on a visible timer. The rig stays packed; no main base. First lookout and
  hauler.
- **lb_07 A Name for the Brigadier.** Brigadier Wrenfield sends an unarmed carrier across the Drift with a white flag and one
  question: a name. Calloway-Ross orders the column's fuel trucks taken; Tindall asks that the carrier be let go. Primary: capture
  or destroy the fuel trucks; hidden bonus: the envoy carrier leaves the map alive. The brother is never named; the radio says only
  "the caravans have a name for him".
- **lb_08 Kettle Flat.** The Authority's reprisal for the road posts comes for the Gathering itself. Pack every rig and get it off
  the far edge while the war band holds the columns: nothing gets left behind this time. Uses `Actor.CountPacked` and
  `Reinforce.Rig(..., parkOnly)` for stragglers. Hidden bonus: the old *Kettle*'s loudspeaker comes too. **C-03:** run (this) or
  stand and fight at the Flat (alternative B, a defend mission; Tindall's line in the ending changes).
- **lb_09 Strangers with Loudspeakers.** Directorate agitprop crawlers are on the dunes, telling the caravans to register for
  service. Buggies and harpoon wagons hunt them before they reach the stranded rigs; hidden bonus: bring one in intact and retune it.
  Mirror of dir_10.
- **lb_10 The Platform.** Back at Tern Point, where it began. The Directorate wants the dead locomotives' cores; an Authority
  column comes to clear the railhead (mirror of auth_10). Hold the platform on two fronts with the first Rolling Forts. Old
  Ottoline Vane speaks on the radio for the first time and says nothing about what she saw. Hidden bonus: the platform ledger.
- **lb_11 The Ones Who Flew.** In summer an Expedition flight lifted people off a stranded rig (Kell's flight, mirror of exp_09).
  Calloway-Ross calls it theft and goes to take them back from the Expedition's north-shore beacon. First rotors and kites against
  gunships. **C-03:** take them back (this) or let them go and raid the fleet's stores instead (alternative B).
- **lb_12 The Second Drift.** Seed the storm on the Expedition anchorage and raid it under cover, blinding the fleet before the
  counter-landing of lb_04. Uses `Trigger.OnSuperweaponFired` (S45 C-05), so the Second Drift finally gets an objective (closes the
  D-164 "until a superweapon-shot API exists" note).

### 3.4 Expedition (hero Commissioner Strand by telex; Aldersey, Roque, Kell)

| # | Id | Title | Type | Map | Unlocks on win | Curve | Size |
|---|---|---|---|---|---|---|---|
| 1 | exp_01 | First Landing (built) | defend, build | glass_coast_e1 | exp_anchorage | Intro | built |
| 2 | exp_05 | The South Coast **(Cut)** | CMD | tern_point | exp_dredge_works, exp_skimmer, exp_tank_farm | Low | S |
| 3 | exp_06 | Dredge Rights | B&D | tern_point | exp_hover_yard, exp_glide_tank, exp_hoverlander, exp_provision_store, exp_plotting_room (T2), exp_gun_battery | Low | M |
| 4 | exp_02 | The Supply Line (built) | defend, B&D | glass_coast_e1 | exp_quay, exp_landing_craft, exp_launch, exp_shipyard, exp_destroyer, exp_sparker | Mid | built |
| 5 | exp_07 | Convoy Weather | naval escort | glass_coast_e1 (west sea) | exp_minelayer, exp_picket (Q4 a); exp_up_sonar_nets (Q5 b) | Mid | M |
| 6 | exp_08 | The Hum **(C-03)** | B&D raid | annex_foothills | exp_flight_deck, exp_jet | Mid+ | M |
| 7 | exp_09 | Kell's Flight | escort / rescue | glass_coast_e1 (dunes) | exp_gunship, exp_sam, exp_skyhook | Mid | M |
| 8 | exp_03 | The Fleet Reaches the Coast (built) | two fronts, defend | glass_coast_e1 | exp_core_hall (T3) and six more | Mid+ | built |
| 9 | exp_10 | The Sealed Depot **(Cut)** | CMD | glass_coast_e1 (south dunes) | exp_barrage_lander, exp_repair_hover (Q4 a) | High | S |
| 10 | exp_11 | The Recall **(C-03)** | defend | glass_coast_e1 | exp_canister_skiff, exp_gyro (Q4 a); exp_up_hardened_hulls (Q5 b) | High | M |
| 11 | exp_12 | The River Mouth | B&D, amphibious | ash_reckoning (south) | exp_up_guided_shells (Q5 b) | High | M |
| 12 | exp_04 | The Fuel (built, finale) | B&D | ash_reckoning | exp_bomber, exp_salvo_array | Peak | built |

Mix: B&D 5 (06, 02, 08, 12, 04), D/E 5 (01, 07, 09, 03, 11), CMD 2 (05, 10). As for the Directorate, exp_08 can be played as a
no-base hover raid to reach the exact D-030 mix.

- **exp_05 The South Coast.** The first relief fleet of 1997 was "never heard from again". A survey hover finds the wreck of one of
  its ships on the south shore by the old railhead. Marines and the hover recover the signal log from Left Behind scavengers. Admiral
  Aldersey asks that the log come to him unread (his secret, Q9 b). No base.
- **exp_06 Dredge Rights.** The fleet must pay for itself: a dredge works and skimmers on a coastal ash field, the first hover yard
  and glide tanks, and an Authority coast battery that shells the shallows. Break the battery. Dr. Roque's first lines.
- **exp_07 Convoy Weather.** A home shipment of supply tenders rounds the cape past Directorate rocket nests on the cliffs and an
  Authority shore gun. Destroyers and launches escort; lose no more than one tender. Strand's telex mentions "correspondence from
  home that can wait" (her unsent recall orders, Q9 b).
- **exp_08 The Hum.** The Hydrographic Office's 1996 carrier signal came from the north-west, and so does the Directorate's 1974
  hum. A hover raid inland from a river mouth reaches a Directorate broadcast mast in the mountain foothills; first meeting with
  the Directorate. First Flight Deck and jets. **C-03:** destroy the mast (this) or seize it and listen (alternative B; the Archive
  gets the Voice's real broadcasts).
- **exp_09 Kell's Flight.** Air Marshal Kell takes a rescue flight into the Glass Coast against orders and finds people living there
  who were on no manifest (her secret, Q9 b). Skyhooks lift a stranded group out of a dune pocket while Left Behind bands and an
  Authority patrol close in; gunships cover. Mirror of lb_11 (its aftermath).
- **exp_10 The Sealed Depot.** Roque's sealed cores are failing faster than the specifications admit. Divers and Ironbound troopers
  slip into the sealed Authority depot under a launch site (the one Fenn found, auth_09) for its pre-Exchange stock. No base.
- **exp_11 The Recall.** A telex from home recalls the fleet. Strand does not forward it. While the tankers load, a Directorate
  dirigible raid hits the anchorage; hold it until the tankers sail. **C-03:** stay (this) or sail half the fleet home (alternative
  B: exp_12 and exp_04 are played with fewer ships; the ending's fleet slide changes).
- **exp_12 The River Mouth.** Seize the south landing on the Reckoning shore for exp_04: cruisers bombard the cliff guns,
  Ironbound go in with the landing craft, the beachhead goes up under fire.

## 4 · Cross-faction timeline, recurring characters, finales

### 4.1 Timeline (one shared 2001, told four ways; positions line up because built missions sit at 1, 4, 8, 12)

| Act / season | Authority | Directorate | Left Behind | Expedition |
|---|---|---|---|---|
| 1 Spring | 01 Survey Convoy, 05 Hatch Country, 06 Mill Town | 01 First Hatch, 05 Night Shift, 06 Assembly Bore | 01 The Refused, 05 Under One Banner, 06 Salvage Rights | 01 First Landing (14 Mar), 05 South Coast, 06 Dredge Rights |
| 2 Early summer | 02 Junction, 07 Defector, 08 Shaft Four | 02 Convoy Roads, 07 Shaft Seven, 08 Redistribution | 02 Road Posts, 07 A Name, 08 Kettle Flat | 02 Supply Line (22 Apr), 07 Convoy Weather, 08 The Hum |
| 3 Late summer, autumn | 09 Fenn's Map, 03 Coast Road, 10 Tern Point | 09 Glass Road, 03 Launch Sites, 10 Strangers on the Dunes | 09 Loudspeakers, 03 Hatch Field, 10 The Platform | 09 Kell's Flight, 03 Fleet at the Coast (9 Jul), 10 Sealed Depot |
| 4 Autumn, winter | 11 Buried Warheads, 12 The Ford, 04 Sunrise Device | 11 Fuel for the Fleet, 12 Order Seven, 04 The Sunrise | 11 The Ones Who Flew, 12 Second Drift, 04 The Banner | 11 The Recall, 12 River Mouth, 04 The Fuel (3 Sep) |

The Expedition's telex dates run ahead of the other campaigns' seasons (exp_04 is dated September, lb_04 is winter); that stands
under D-154 (parallel, no canon). New Expedition telexes take dates between the built ones.

**Mirrored pairs (Q2 b): the same event, two sides, the same map.**
| Event | Side A | Side B | Map |
|---|---|---|---|
| The survey convoy ambush | auth_01 | lb_01 | rustbelt_m2 (built) |
| The mill town | auth_06 | dir_06 | rustbelt_mill |
| Dray crosses | auth_07 | dir_07 (heard on the speakers) | rustbelt_m2 / annex_foothills |
| Tern Point railhead | auth_10 | lb_10 | tern_point |
| Loudspeaker crawlers on the dunes | dir_10 | lb_09 | tern_point / glass_coast_e1 |
| Kell's flight and its aftermath | exp_09 | lb_11 | glass_coast_e1 |
| Fenn's sealed depot | auth_09 | exp_10 | glass_coast_e1 |
| The warheads and the tunnel head | auth_11 | dir_12 | ash_reckoning |

Archive crossings: each pair's second mission can unlock the other side's entry (design 12 §2.3); the unlock map
(docs/archive/m8_13_unlocks.md) moves leader secrets to the mission that tells them (e.g. `leader_auth_dray` from dir_04 to
auth_07, `leader_exp_kell_secret` from lb_04 to exp_09). Proposal only; the Archive package decides.

### 4.2 Recurring characters (existing names only)
- **Authority:** Wrenfield (all 12, hero), Pell (teletypes in 06, 08, 10; Charter voice), Fenn (09 lead, 03 cameo), Dray (07 lead,
  08, 11, 04). Cameos: Calloway-Ross (09, 10), Thrall (07 enemy net, 11).
- **Directorate:** Larkin (all 12, hero), Thrall (07, 08, 11, 12, 04), Lund (06, 07, 11; her "way up without a war" seeds the
  ending), Harrowgate (only as recordings; his death is the 12 Archive unlock). Cameos: Fenn (03, 04), Strand (03, 11), Dray (07).
- **Left Behind:** Calloway-Ross (all 12, hero), Tindall (05, 07, 08; the split widens), Vane (10, first and only lines). Cameos:
  Wrenfield (02, 07), Thrall (03), Kell (11, 04).
- **Expedition:** Strand (all 12, hero), Aldersey (05, 11, 03, 04), Roque (06, 10, 02, 04), Kell (09 lead, 03). Cameos: Wrenfield
  (02), Larkin (08 on the mast, 03, 04), Calloway-Ross (09).

### 4.3 Finales
The four built finales stay the finales (D-154: each campaign ends in its faction's victory, then its ending slides). The new
missions feed them: auth_12 delivers auth_04's start, dir_12 builds dir_04's tunnel head, lb_12 blinds the fleet before lb_04,
exp_12 takes exp_04's landing. Suggestion for a later text package: one new conditional slide per ending keyed on a new hidden
bonus (the Tern Point ledger, the spared battalion, the Kettle's speaker, Aldersey's log), owner-approved text (D-086).

## 5 · Build waves, cost and changes to built files
- **Waves:** pilot (auth_07) → owner review → act 1 for all four (8 missions) → act 2 → act 3 → act 4. The three new maps come in
  the wave that first needs them (rustbelt_mill in act 1, annex_foothills in act 2, tern_point in act 1 for the Expedition).
- **Size totals:** S 10, M 22, L 0, plus three maps (M each). The cut line removes 8 (7 S, 1 M).
- **Built files later packages must touch (not this package):** each campaign list (order, `requires`, `still`), the `next` field
  of each built mission, the Archive unlock map, optionally the auth_04 briefing line (Q3), and re-sweeps of built missions after a
  leaked unlock (Q4 a).

## 6 · Pilot: auth_07 "The Defector"

**Why this one.** Cheapest of the 32 that shows the most: reused map (rustbelt_m2), no new art, every API function exists, no
base (so no economy tuning), about 150 lines of Lua. It shows all three things the expansion is for: a mission type the campaign
lacks (no-base commando and escort), a story beat that is only hinted today (Dray), and an item introduced in story (the
carrier and decon truck from auth_02's unlock list get a job; the Seismic Mortar is the reward). It also tests the slot-in rule
between two built missions. Runner-up: lb_08 Kettle Flat (best Left Behind theme, but rig packing in a defend mission is more risk).
The spec assumes Q3 (a) and Q4 (a); with Q4 (b) it unlocks nothing and nothing else changes.

### 6.1 Map, players, zones
- Map `rustbelt_m2.map.toml` (64x64; the river runs from the north-east corner to the south-west corner; road bridges near
  (46,18) and (17,44); the player's side is north-west, the Directorate's south-east). Schema 2, act 2, faction authority,
  `next = "auth_08"`, seed 2027, locale `locale.toml`, locale prefix `a7.`.
- Players: slot 0 Authority human, cash 0, no buildings. Slot 1 Directorate `role = "ai"`, cash 1500, faction directorate, the
  auth_02 base reused: `dir_shaft` (58,58) tag `enemy_shaft`, `dir_reactor_vent` (54,58), `dir_muster_hatch` (58,54), plus a
  `dir_hatch` near the spoil heaps tag `spoil_hatch` (about (44,36); the builder picks a legal cell).
- Zones (inclusive rects, `(tune)`; the builder checks each against the terrain):
  `home` [2,2,14,12] · `junction` [35,13,44,23] · `bridge_ne` [44,16,49,21] · `bridge_sw` [14,41,20,47] · `spoil` [38,28,44,33]
  (Dray's hiding place, south-east bank) · `culvert` [20,44,24,48] (hidden bonus, south-east end of the south-west bridge) ·
  `ambush_ne` [48,20,54,26] · `enemy_base` [52,52,62,62] · `spawn_e` [62,26,63,34] · `spawn_s` [30,62,40,63].

### 6.2 Starting forces (same at every tier, D-061)
Player, in `home`: 1 `auth_carrier` (tag `carrier`), 1 `auth_decon_truck`, 2 `auth_jeep`, 6 `auth_rifleman`, 2 `auth_marksman`,
2 `auth_rocketeer`. Dray is not on the map at start. He is created as an `auth_engineer` (no weapon, matching "refuses to wear a
weapon"; no hero type or new art) owned by slot 0, tag `dray`, when the player reaches `spoil`.
`[starts_with]` = auth_02's starts_with plus its unlocks (the act-2 kit; nothing is buildable without a yard, so it only keeps the
allow-list honest). `[unlocks_on_win] tech = ["auth_seismic_mortar"]` (Q4 a): Dray's log gives the tunnel bearings.

### 6.3 Objectives
- `reach` (primary): "Meet the defector at the spoil heaps". Target `spoil`.
- `bring` (primary, added when Dray is found): "Bring Lieutenant Dray back to the post alive". Target the tag `dray`, then `home`.
- `log` (hidden bonus, revealed by Dray's radio line): "Recover Dray's reactor log from the culvert". Target `culvert`.

### 6.4 Triggers and timeline (20 ticks a second)
1. `OnInit`: objectives; `Media.MessageKey("a7.start")`; Pell's teletype and Wrenfield's opening (`Dialogue.Say`);
   `Camera.Focus("spoil", 120)`; `AI.SetTier(1, Mission.Difficulty())`; `AI.Defend(1, "enemy_base")`; the bridge picket spawned with
   `Actor.CreateGroup` at the south end of `bridge_ne` (sizes in 6.5); `Mission.Timer("rendezvous", 4800, RendezvousMissed,
   "a7.timer.rendezvous")`; `Trigger.OnEnter("spoil", 0, DrayFound)`; `Trigger.OnEnter("culvert", 0, LogFound)`;
   `Trigger.OnAllDestroyed(0, ColumnLost)` (no base: the sim will not end the mission by itself).
2. `DrayFound`: `Actor.Create("auth_engineer", 0, Map.Center("spoil"), "dray")`; complete `reach`; cancel the rendezvous timer; add
   `bring` with `Objective.SetTarget("bring", "home")`; Dray's lines; `Objective.Reveal("log")`; `Trigger.OnDestroyed("dray",
   DrayLost)`; `Mission.Timer("alert", 600, ShaftAlert, "a7.timer.alert")` (30 s before Shaft Four misses him);
   `Trigger.Every(10, CheckHome)`.
3. `ShaftAlert`: Thrall on the enemy net (cameo); `AI.Wave(1, "alert", "bridge_ne")` (the base's whole army goes for the short
   bridge; its size follows the AI tier); `Reinforce.Zone(1, Hunters(), "ambush_ne", Map.Center("bridge_ne"))` (the north-east
   bridge is now closed hard: the player chooses to fight through or take the long south-west road over the ash, where the sealed
   carrier keeps Dray alive); then `Trigger.AfterDelay(1200, HuntEast)` and `Trigger.AfterDelay(2400, HuntSouth)`.
4. `HuntEast` / `HuntSouth`: `Reinforce.Edge` from `spawn_e` / `spawn_s` toward `Actor.Cell("dray")` (or `bridge_sw` if he is
   aboard and the cell is nil), with Wrenfield's warnings.
5. `CheckHome`: win when Dray's cell is inside `home`. Builder to verify what `Actor.Cell` returns for a soldier inside a carrier;
   if it is nil, the win needs him on foot in `home`, and the radio says "let him out at the post".
6. `LogFound`: reveal (if still hidden) and complete `log`; Dray's thanks line; debrief note.

### 6.5 Waves and tier scaling (all `(tune)`; `Pick({t1..t5})`; measured to the Q-75 targets by the 12-seed sweep)
| Group | When | Where | Tier 1..5 |
|---|---|---|---|
| Bridge picket | start | south end of `bridge_ne` | conscripts {2,3,4,6,8}, ashthrowers {0,0,1,1,2}, pillboxes {0,1,1,2,2} |
| Spoil guard | start | around `spoil_hatch` | conscripts {1,2,3,4,5} |
| Built-in AI wave | `ShaftAlert` | `AI.Wave` to `bridge_ne` | whatever the AI built at `AI.SetTier` (tier 1 small, tier 5 large) |
| Hunters | `ShaftAlert` | `ambush_ne` | conscripts {3,4,6,8,10}, ashthrowers {0,1,1,2,3} |
| East hunt | alert + 1200 | `spawn_e` | conscripts {2,3,4,6,8}, sappers {0,0,1,2,2} |
| South hunt | alert + 2400 | `spawn_s` | conscripts {0,2,3,5,7}, ashthrowers {0,0,1,1,2} |

### 6.6 Win, lose, par
- **Win:** Dray inside `home`, alive (`Mission.Win(0)`), Wrenfield's closing line.
- **Lose:** Dray destroyed (`DrayLost`); the rendezvous timer runs out before the column reaches `spoil` ("he has gone back
  down"); every player unit destroyed (`ColumnLost`). Each fails the open primaries and calls `Mission.Lose(0)`.
- **Par:** `par_seconds = 600` placeholder; replaced by the bot's tier-3 median win x 2.5 once measured (D-289).
- **Tests:** MissionBot wins at tiers 3 and 5 (D-163); script check with the `test_template.md` pattern; 12-seed sweep on the
  cluster; one mid-mission save and load (timers, the queued hunts, the tagged Dray).

### 6.7 Mission API functions used (all exist, docs/missions/api.md)
`Objective.Add`, `Objective.AddHidden`, `Objective.Reveal`, `Objective.Complete`, `Objective.Fail`, `Objective.Status`,
`Objective.SetTarget`, `Mission.Timer`, `Mission.TimerCancel`, `Mission.Difficulty`, `Mission.Win`, `Mission.Lose`,
`Trigger.OnEnter`, `Trigger.OnDestroyed`, `Trigger.OnAllDestroyed`, `Trigger.AfterDelay`, `Trigger.Every`, `Actor.Create`,
`Actor.CreateGroup`, `Actor.Cell`, `Reinforce.Zone`, `Reinforce.Edge`, `AI.SetTier`, `AI.Defend`, `AI.Wave`, `Dialogue.Say`,
`Media.MessageKey`, `Camera.Focus`, `Map.Center`, `Random.Next`. Nothing new.

### 6.8 Briefing draft (CRA teletype, the style of auth_01..04; D-284 keeps this style pending the owner's choice)
```
CRA FIELD COMMAND  //  RUST BELT SECTOR  //  PRIORITY URGENT
TO: COLUMN COMMANDER
RE: SHAFT FOUR SPOIL HEAPS, OPERATION OPEN DOOR

1. A DIRECTORATE ENGINEER HAS SIGNALLED BY LAMP FROM THE SPOIL HEAPS SOUTH OF THE RIVER. HE ASKS TO CROSS. HE SAYS HE CARRIES A REACTOR LOG. HE SAYS HE WILL NOT CARRY A GUN.

2. YOUR COLUMN: ONE SEALED CARRIER, ONE DECON TRUCK, TWO JEEPS, SIX RIFLEMEN, TWO MARKSMEN, TWO ROCKETEERS. NO YARD. NO FACTORY. WHAT YOU LOSE STAYS LOST.

3. MEET HIM AT THE SPOIL HEAPS BEFORE FIRST SHIFT. THE NORTH-EAST BRIDGE IS SHORT AND WATCHED. THE SOUTH-WEST BRIDGE IS LONG AND THE ASH LIES DEEP ON THAT ROAD. THE CARRIER IS SEALED. HE IS NOT.

4. WHEN THEY MISS HIM THEY WILL COME. BRING HIM BACK TO THE POST ALIVE. A DEAD DEFECTOR IS ONLY A RUMOUR.

5. THE ADMINISTRATOR ASKS THAT HE BE TREATED AS A GUEST. I ASK THAT HE BE TREATED AS CARGO UNTIL HE IS ACROSS.

-- END OF MESSAGE --
```

### 6.9 Radio drafts (owner to approve, D-086)
- `a7.radio.pell` (Pell, teletype): "Teletype, Administrator Pell. A man who walks out of the ground unarmed deserves to arrive.
  Please see that he does."
- `a7.radio.start` (Wrenfield): "Wrenfield. One man, one lamp, a lot of ash between us. Get to him before their shift changes."
- `a7.radio.dray` (Dray): "Dray. Lieutenant, Shaft Four engineering. I am unarmed. I would prefer not to cross the ash on foot."
- `a7.radio.log` (Dray): "I hid the log in the culvert by the south-west bridge. If they find it first, they will know what I told you."
- `a7.radio.thrall` (Thrall, enemy net): "Shaft Four. One of ours has walked into the light. Bring him back. Gently, if possible."
- `a7.radio.win` (Wrenfield): "He is across. Get him a coat and a cup of something. Then we talk about tunnels."

## 7 · Notes for the lead
- Lore inconsistency found while reading: docs/lore/leaders.md has Brigadier **Hester** Wrenfield ("Her own brother"), but
  docs/lore/left_behind.md (faction_lb_authority) says "**His** brother" and "the Brigadier has not yet asked". The briefings and the
  auth campaign page use "her". A one-word lore fix for the owner to approve; lb_07 is written for "her".
- Upgrade gating (Q5) is a code finding, read from `Simulation.CanResearch` and `MissionHost.IsTypeAllowed` (the Upgrade category
  maps to no id); a builder should confirm in play before relying on it.
- C-06 `Player.KeepAlive` (HANDOFF) would replace the far-corner reserve buildings several new missions would otherwise need.
