PACKAGE v-sim-faction (verify only). Branch `wp/s18-v-sim-faction`, worktree `/home/claude/wt/s18-v-sim-faction`. No file under `src/` or `data/` was changed; 13 new files, all in `tests/Sim.Tests/S18/`. Nothing pushed.

Every test runs on the real shipped `data/rules` (loaded through `RulesLocator`), using a small harness `S18World.cs`.

## COMMITS
- 4acd8da test(sim): pin that the Authority Atomic Battery can never fire on shipped data [S18-FAD-01]
- cee452c test(sim): pin the Agitprop aura slowing conscript reload on shipped data [S18-FAD-02]
- 1620532 test(sim): pin Expedition never-defeated and second Command Ship defeat [S18-FACTION-LE-02] [S18-FACTION-LE-19]
- 246b14f test(sim): pin Left Behind structure lanes building while the Mother Rig is packed [S18-FACTION-LE-03]
- a85aa4b test(sim): pin instant Hero rank and 1000 wreck from placeholder costs [S18-FACTION-LE-04]
- 20c138b test(sim): pin the Tow Truck bonus having no effect [S18-FACTION-LE-08]
- 35310a9 test(sim): pin dived-sub immunity and surfaced-sub submerged armour [S18-FACTION-LE-05] [S18-FACTION-LE-06]
- ce34be8 test(sim): pin splash ignoring air/ground and zero-damage sprays dealing 1 [S18-FAD-06] [S18-FAD-13]
- ff8173a test(sim): pin the Kite one-use accident [S18-FACTION-LE-14]
- ec1f5fc test(sim): pin five upgrades doing less than their text [S18-FACTION-LE-13]
- d1581dc test(sim): pin no attack-ground for the Decon Truck and needs_power ignored by Searchlight and Mast [S18-FAD-05] [S18-FAD-19]
- 68ccff8 test(sim): pin non-Expedition warheads at 100 % vs ship armour [S18-FACTION-LE-07]

## VERDICT TABLE
Nothing is fixed: every item is "no: awaiting owner" (sim outcome, rules number or design). Each confirmed item has `Audit_` tests that pass today and, unless noted, a skipped `Desired_` test.

| Finding | Verdict | Test class (tests/Sim.Tests/S18) | Smallest fix | Kind | Decisions touched |
|---|---|---|---|---|---|
| FAD-01 | CONFIRMED | AtomicBatteryAuditTests | buildings.toml:570 `superweapon_entrance_range` 8 → 0 | NUMBERS (data) | D-132(4) same fix for Left Behind; D-164 (auth_04 reward) |
| FAD-02 / MATH-03 | CONFIRMED (mechanic); duel does not show it | AgitpropAuraAuditTests | vehicles.toml:130 `aura_rof_permille` 1250 → 800 | NUMBERS | D-152 approval path |
| LE-02 | CONFIRMED (cases b and c) | DefeatAuditTests | `UpdateDefeat` (Production.cs:524-530): count a live founder (`DeploysInto >= 0`) as a base, or set `EverHadBase` from the start kit | SIM | D-062, D-173 |
| LE-19 | CONFIRMED | DefeatAuditTests | same line as LE-02 | SIM | D-173 (says founders count) |
| LE-03 | CONFIRMED | RigLaneAuditTests | `RigLanePaused` (Rigs.cs:99): test "has a rig that packs and no live unpacked conyard-root building" instead of `Produces == category`; fix the RigTests fixture | SIM | D-102 ("a packed rig cannot produce") |
| LE-04 / MATH-04 / SIM-RULES-08 | CONFIRMED | KillValueAuditTests | cap kill value in Veterancy.cs:39-45, or add a vehicle `buildable = false` and set real costs | SIM or NUMBERS | D-127, D-126 |
| LE-08 | CONFIRMED | TowAuditTests | vehicles.toml:476 `tow_bonus_permille` 1000 → e.g. 2000 | NUMBERS | D-134 confirmed the 1000 (via s9_tables) |
| LE-06 | CONFIRMED | SubmarineAuditTests | `ArmorAt` (Actors.cs:79): submerged armour only while dived, plus `armor = "ship_light"` on the sub | SIM + data | D-143 |
| LE-05 / MATH-02 | CONFIRMED | SubmarineAuditTests | owner choice; smallest is one check in `Fire` (a dived sub cannot fire) | DESIGN | D-143 (confirmed by the later "all assumed up to D-152" entry), D-124 |
| FAD-06 | CONFIRMED (both directions) | SplashAuditTests | `Detonate` (Combat.cs:356): skip actors whose air/ground state differs from the primary's | SIM (hash re-pin) | D-048 (3) "splash as built" |
| FAD-13 | CONFIRMED | SplashAuditTests | `Hurt` / `ComputeDamage` (Combat.cs:258): return early when weapon damage is 0 | SIM | D-048 (3) "minimum 1" |
| LE-14 | PARTLY | KiteAuditTests (no `Desired_` test) | explicit one-use flag and a sane `rate_of_fire` | SIM | D-102 (air roster) |
| LE-13 | CONFIRMED (all five) | UpgradeTextAuditTests (no `Desired_` test) | reword tooltips, or: wreck value × permille (Salvage.cs:98); cap before bonus (Raid.cs:65-67); add `sonar_radius` as an effect attribute | SIM / TEXT | D-152 for numbers; wording is owner text |
| FAD-05 | CONFIRMED (Decon Truck tested) | GroundEffectAuditTests | new AttackCell command or auto behaviour | DESIGN | D-047/D-048 controls, D-077 |
| FAD-19 | CONFIRMED | GroundEffectAuditTests | power check in `CanSeeThroughBlackout`, `IsStealthRevealed`, `IsJammedFor` (Ground.cs:123, 220, 283) | SIM | none found |
| LE-07 | CONFIRMED | ShipArmourAuditTests | fill the three ship columns of the 20 padded non-Expedition warheads | NUMBERS | D-124 (decided the mask only), D-152 |
| FAD-07 | CONFIRMED (data and duels) | none (`compare.py`) | numbers pass on five types | NUMBERS | D-152 |
| FAD-08 | CONFIRMED | none (duels) | pillbox / turret range or Rocket Truck numbers | NUMBERS | D-152, D-174 |
| FAD-12 | PARTLY | none (duels) | Crawler range or cost | NUMBERS | D-152 |
| FAD-14 | CONFIRMED | none (data and duel) | Marksman cost or speed | NUMBERS | D-152 |

One-line notes per finding:
- **FAD-01:** a fully charged battery shows "ready" in the HUD, `CanFireSuperweapon` is false for all 4,096 map cells, and the Special order leaves the charge untouched.
- **FAD-02:** six conscripts with an Agitprop nearby do about 80 % of the damage (reload multiplier 1250). In the `ai duel` the side with the Agitprop still wins, because the 300 HP Agitprop soaks fire.
- **LE-02:** an Expedition player with nothing left alive is still undefeated after 2,700 ticks with NoRebuildDefeat on, and `GameOver` stays false. On an all-land map `SkirmishStart` silently spawns no Command Ship. A Left Behind player in the same position is defeated within 3 ticks.
- **LE-19:** killing the depot defeats the Expedition player while its second Command Ship floats; the Left Behind twin with a packed rig survives.
- **LE-03:** no shipped building has `Produces` = Structure or Defence, so the pause never matches. A Refinery Rig and a Spike Nest finished while the Mother Rig was packed.
- **LE-04:** one kill of a tender, Command Ship or packed rig gives kill value 6000 / 6000 / 4000 and rank 3; an 800-credit tank gives rank 1. A packed 200-credit Still Rig leaves a 1000 wreck; the same rig unpacked leaves 50.
- **LE-08:** the towed rig's position is identical to the untowed one and the speed multiplier is 1000.
- **LE-06:** all 16 Expedition weapons that can hit a surface ship compute to 1 point against the sub; a Destroyer took 3-8 HP off it in 10 s.
- **LE-05:** no auth_/dir_/lb_ weapon targets submerged and no land-faction type has sonar. A dived sub killed a coastal turret with a tank beside it and took 0 damage; surfaced, the same defenders shoot it.
- **FAD-06:** flak shooting an aircraft took 14 HP per shell off its own power plant underneath; a howitzer shell on a building hurt the aircraft above it.
- **FAD-13:** all five zero-damage weapons compute to 1 against every armour class; a Silo lost 5-13 HP to each of the three sprays.
- **LE-14:** true that there is no one-use rule: reload is 1 tick and half splash equals the Kite's 60 HP exactly. Not true that a veterancy rank makes it reusable: a Hero Kite still dies on its first drop, because rank raises its own bomb more than it lowers damage taken. I found no current rule that keeps a Kite alive.
- **LE-13:** Salvage Rights leaves the wreck at 200 and only speeds the drain. Grapple takes 400 → 600 at 2000 stored but 1000 → 1000 at 5000 or more. Sonar Nets moves sight 9 → 11 and leaves sonar at 9. Scrap Plate is +20 % HP, no armour change, and skips Rotor and Kite. Patchwork only extends 3 cells to 5.
- **FAD-05:** no order makes the Decon Truck spray bare hot ground; it cleans only when attacking an enemy building standing there. Foam Layer, Ash Sower and Dirigible share the code path but were not run in a scenario.
- **FAD-19:** with zero power the turret holds fire, but the Searchlight still sees through Blackout and reveals stealth, and the Mast still jams.
- **LE-07:** 20 of the 26 non-Expedition warheads have 100/100/100 in the ship columns and the other 6 are 0/0/0. Scavenger 1.3 → 9.3 dps and Rotor 2.5 → 17.5 dps (heavy tank vs heavy ship); the Expedition's own rifle is 30 % against ship_heavy.
- **FAD-07:** Bulwark differs from the Tank only in `tier`; Howitzer 20 damage against the mortar's 28; Interceptor and Bomber fly at 0.62 cells/s.
- **FAD-08:** a Tank kills a Pillbox and a Rocket Truck kills a Turret with no damage taken.
- **FAD-12:** the Land-Train dominates at equal cost, but one Crawler beat two Tanks on attack-move, so the "tank kites it" claim did not show.
- **FAD-14:** the only differences are tier and range 3 → 4.

## DUELS
These are measurements, not tests. Raw output is `/home/claude/audit/v-sim-faction/duels.txt`, the script is `duels.sh`, and the data comparison is `compare.py` / `compare.txt`. The duel map is all land and both sides attack-move.

| # | Duel | Expected | Observed | Reading |
|---|---|---|---|---|
| 1 | conscript:6 + agitprop vs conscript:6 | Agitprop side loses | Agitprop side wins at 292 ticks, both seatings (mirror without it: mutual kill at 320) | not confirmed by duel; the debuff itself is test-proven |
| 3 | exp_destroyer vs exp_supply_tender | Hero | no result (ships cannot move on the land map) | tool cannot do it; covered by KillValueAuditTests |
| 4 | lb_kite vs auth_refinery | Kite dies, 144 + 144 | invalid: the tool ends at tick 4 because an unarmed building counts as "no side left" | covered by KiteAuditTests (216 or 288 damage, Kite dead) |
| 5 | conscript:10 vs auth_tank | infantry win in about 30 s | win at 44 s, 5 left | confirmed, slower |
| 5 | lb_scavenger:13 vs auth_tank | win in about 30 s | win at 32 s, 9 left | confirmed |
| 6 | scavenger:10 vs rifleman:6 / conscript:8 / marine:4 | win, about half left | win, 7 / 7 / 6 left, in 9-10 s | confirmed, stronger than the model |
| 7 | howitzer:3 vs dir_pillbox; seismic_mortar:3 vs dir_pillbox | about 60 s; about 42 s | 61 s; 43 s | confirmed |
| 8 | exp_jet:2 vs auth_flak:3 / lb_flak_net:3 | flak never fires | flak wins both at 12 s | refuted under attack-move: jets fly into range |
| 8 | exp_jet:2 vs auth_interceptor:4 | interceptors never engage | interceptors win at 9 s, 4 left | refuted under attack-move |
| 9 | exp_cruiser vs howitzer:3 | cruiser outranges | cruiser wins, 728/1200 HP left | not a fair test: the ship is immobile on land |
| 10 | dir_sapper:3 vs auth_tank | sappers win | no result after 3000 ticks | groups walk past each other; see new finding 1 |
| 10 | dir_sapper:2 vs auth_turret | sappers win if one arrives | turret wins at 10 s | refuted |
| 11 | exp_pillbox:2 / auth_turret:1 / dir_pillbox:1 vs rifleman:8 | only the Expedition pair holds | all three hold (536/600, 373/400, 256/400 HP) | refuted |
| LE-07 | lb_rotor:4 vs exp_cruiser | about 17 s | 25.8 s including approach, rotors untouched | confirmed |
| FAD-07 | auth_bulwark vs auth_tank | identical | mutual kill at 288 ticks | confirmed |
| FAD-07 | auth_howitzer vs auth_seismic_mortar | mortar better | mortar wins | confirmed |
| FAD-08 | auth_tank vs dir_pillbox | about 32 s, no return fire | 35 s, tank 400/400 | confirmed |
| FAD-08 | auth_rocketeer:3 vs dir_pillbox | no return fire | 18 s, 180/180 | confirmed |
| FAD-08 | dir_rocket_truck vs auth_turret | about 33 s, no return fire | 35 s, truck 250/250 | confirmed |
| FAD-08 | dir_rocket_truck:4 vs auth_tank:4 | trucks win | trucks win at 10 s, 4 left | confirmed |
| FAD-12 | dir_landtrain:3 vs dir_reactor_crawler:4 (6000 each) | Land-Train | Land-Train wins, 3 left | confirmed |
| FAD-12 | auth_tank:2 vs dir_reactor_crawler:1 | tank kites | Crawler wins, 123/900 HP | claim not shown |
| FAD-14 | auth_marksman:6 vs auth_rifleman:6 | Marksman | Marksman wins, 3 left | confirmed |

Suspect 2 is covered by SubmarineAuditTests. Suspect 12 (AI matches with the army cap removed) was not run.

## CHECK
`flock /home/claude/wt/dotnet.lock timeout 580 dotnet test tests/Sim.Tests -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~Sim.Tests.S18" --blame-hang-timeout 5m`
- Last full S18 run: 45 passed, 14 skipped, 0 failed. One of the 45 was a temporary diagnostic test, since deleted, so the committed set is 44 passed, 14 skipped.
- The full suite was not run, per the brief.

## NEW FINDINGS
1. **Melee-range units never engage on attack-move or guard** · minor · SIM/DESIGN · confidence high. Auto-acquisition searches only within weapon range (`AcquireTarget` uses `w.Range`). Three Sappers (range 1 cell) on attack-move walked within 2 cells of a tank that was shooting them, never attacked, and both groups then sat idle out of sight for 3,000 ticks. Reproduced in my harness. Fix idea: acquire within sight and close in, at least for weapons shorter than sight.
2. **`ai duel` cannot measure three kinds of case** · minor · TOOL. An unarmed building counts as "side dead", so the duel ends at tick 4. Ships are spawned on land and cannot move. There is no dive option.
3. **Hero Kite** · info. A rank-3 Kite still dies on its first drop (see LE-14).
4. **LE-03 general form** · info. Nothing ties any faction's structure lane to a live conyard; `AdvanceLane` never rechecks prerequisites. I did not test the Authority case.
5. **Expedition with no building is immune to defeat even without the bug trigger** · info. `EverHadBase` stays false until the first building, so an Expedition player who simply never lands cannot lose.

## QUESTIONS FOR THE OWNER
1. **FAD-01:** should the Atomic Battery fire anywhere (range 0, as Left Behind and Expedition do), or keep a spotter rule with a new Authority spotter? I recommend range 0.
2. **FAD-02:** was 1250 meant as "+25 % rate of fire"? I recommend 800 (reload × 0.8). Even today the Agitprop helps in a straight fight as a damage sponge.
3. **LE-02 / LE-19:** should a live Command Ship count as a base, like a packed rig, and should an Expedition player with no founder and no building lose under D-173? I recommend yes to both.
4. **LE-03:** pause only Left Behind lanes while the Mother Rig is packed (D-102), or pause every faction's structure lanes when no conyard stands? I recommend Left Behind only for now.
5. **LE-04:** cap kill value per victim (for example 2000), or add a vehicle `buildable = false` and real costs? I recommend the flag plus real costs, which also fixes the wreck and the sidebar.
6. **LE-05:** D-143 "a dived sub can still fire" is confirmed, yet three factions cannot answer it. Options: a dived sub cannot fire; firing reveals it for N ticks; or each land faction gets one sonar and one depth weapon. I recommend firing reveals it.
7. **FAD-06 / FAD-13:** both touch D-048 (3) "splash as built, minimum 1". May splash respect air/ground, and may damage-0 weapons deal 0? I recommend yes to both; it needs a hash re-pin.
8. **LE-13:** fix the mechanics or reword the five tooltips? I recommend rewording Scrap Plate and Patchwork, and fixing Grapple (cap before bonus), Sonar Nets and Salvage Rights.
9. **LE-08:** which tow value? D-134 confirmed 1000, which does nothing. I recommend 1500-2000 after a measurement.
10. **FAD-07 / 08 / 12 / 14 and LE-07:** one numbers pass under D-152. The duels suggest starting with the Bulwark, Marksman, Howitzer, aircraft speed and Pillbox range.

## USER CHECK
No fixes were made, so there is nothing new to try in the game. To see the bugs yourself:
- Build an Atomic Battery, let it charge, and try to target it: every cell is refused.
- As Left Behind, queue a Refinery Rig, pack the Mother Rig, and watch the build finish anyway.

Memory: nothing from memory was used; nothing new worth saving.