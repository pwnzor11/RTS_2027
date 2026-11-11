# S19 numbers pass (README 2.3, D-205) and Q-70 layout 2 money

Applied only where a report names a concrete value. Everything else is under QUESTIONS.

| id | key | old -> new | source line |
|---|---|---|---|
| FAD-02 / MATH-03 | vehicles.toml `[dir_agitprop] aura_rof_permille` | 1250 -> 800 | wave1/faction-math.md:60, wave2/v-sim-faction.md:25, :112 |
| LE-08 | vehicles.toml `[lb_tow] tow_bonus_permille` | 1000 -> 1750 (middle of 1500-2000) | wave1/faction-le.md:91, wave2/v-sim-faction.md:119 |
| LE-07 | warheads.toml `auth_small_arms`, `dir_small_arms`, `lb_small_arms` ship_light / ship_heavy | 100 / 100 -> 70 / 30 (copy of `exp_small_arms`) | wave1/faction-le.md:83, :86 |
| LE-07 | warheads.toml `auth_ap`, `lb_ap`, `lb_harpoon`, `dir_rocket`, `dir_slug` ship_light / ship_heavy | 100 / 100 -> 90 / 70 (copy of `exp_guided_ap`) | wave1/faction-le.md:86 |
| FAD-14 | infantry.toml `[auth_marksman] cost` | 100 -> 200 | wave1/faction-ad.md:215 |
| FAD-14 | infantry.toml `[auth_marksman] speed` | 12 -> 10 | wave1/faction-ad.md:215 |
| Q-70 | ProceduralBuilder.Zones.cs `MoneyPermille`, Glass Coast | 300 -> 220 (floor, see below) | owner answer (a) |
| Q-70 | same, Rust Belt | 420 -> 378 (-10 %) | owner answer (a) |

Notes
- The report gives no per-warhead values for LE-07 ("fill those three columns deliberately", wave1/faction-le.md:86). The only numbers it quotes are the Expedition's own (small arms 70 / 30), so the nearest Expedition warhead of each damage class was copied. The Submerged column stays 100 (those weapons cannot target a dived vessel; the sub armour belongs to s19-b-combat).
- Q-70: the money is set by `ScaleDensity` via `MoneyPermille` in the zoned (layout 2) builders only; layout 1 never calls it (its four biome map hashes are pinned and unchanged).

### Q-70 measured field money (sum of density x2 for pitchblende, Medium, 2 players, seeds 1-8)
| seed | Glass before (300) | Glass after (220) | Rust before (420) | Rust after (378) |
|---|---|---|---|---|
| 1 | 352 | 312 | 470 | 424 |
| 2 | 346 | 306 | 470 | 424 |
| 3 | 352 | 312 | 470 | 424 |
| 4 | 336 | 290 | 470 | 424 |
| 5 | 338 | 296 | 470 | 424 |
| 6 | 348 | 300 | 470 | 424 |
| 7 | 350 | 304 | 470 | 424 |
| 8 | 336 | 286 | 470 | 424 |
| total | 2758 | 2406 (-12.8 %) | 3760 | 3392 (-9.8 %) |

Rust Belt hits -10 % (within 2 points). Glass Coast cannot reach -20 % with this knob: every resource cell has density >= 1 and permille 100 to 220 all give the same 2406, so -12.8 % is the floor (every cell at density 1). A further cut needs fewer or smaller Glass Coast field cells (a layout change that moves more than money). Open for the owner.

## QUESTIONS (no concrete number in any report, or the report gives alternatives)
1. Non-Expedition HE / area / flame / demolition warheads vs ship armour (`auth_he`, `auth_seismic`, `dir_ash`, `dir_demolition`, `dir_rad_shell`, `lb_he`, `lb_flame`, `lb_rocket`, `lb_kite_blast`): still 100 / 100. The closest Expedition analogue (`exp_naval_shell_he`) is also 100 / 100. Give values or keep.
2. Bulwark = Tank (FAD-07): the report names no number. Options: more HP or cost, slower speed, own weapon.
3. Howitzer weaker than the tier-2 mortar (FAD-07): no number (mortar 28 damage, range 9; howitzer 20, range 10).
4. Aircraft at 0.62 cells/s (FAD-07): no number (Rotor 2.34, Jet 4.69 cells/s for scale).
5. Tier-1 defences outranged (FAD-08): options only, "pillbox range 4-5 or a Directorate tier-1 anti-vehicle emplacement; turret range 6; Rocket Truck to 6 cells or 60 damage / 900 cost".
6. Reactor Crawler v Land-Train (FAD-12): "range 5-6 and 60 damage, or about 1100 cost".
7. Commissar has no aura (FAD-09): "e.g. radius 3, aura_rof_permille 850" but it needs a new `aura_targets` list and the report asks whether the aura belongs to the Agitprop or the Commissar (wave1/faction-ad.md:306). Not applied.
8. Repair auras 1 %/10 ticks under fire (LE-12): "cap the heal in HP or make it flat", no number.
9. Left Behind has no mobile anti-air (LE-11, MATH-08): options "both-target Slinger / Harpoon, flak wagon" or Flak range 6+. Adds a weapon or unit, not done.
10. Directorate structures copy the Authority's (FAD-20): "e.g. -20 % HP and -15 % cost and build time", but the building pairs are not named exactly (Hatch/Barracks, Bore/Factory, Cistern/Silo, Bunker/Wall, Pillbox/Turret, Smelter/Refinery, Vent/Power, Shaft/Conyard). Say yes and I apply it to those eight pairs.
11. Refinery Rig dominated by the Yard (LE-16) and the LB yard sell loop (MATH-11): "cut the price or add a free Gleaner", no number.
12. Tutorial cash trap (UXG-02): "start with more cash, grant cash at the harvest step, or stop after one rifleman" (mission data, not rules).
13. Recruit AI wiped two openings at 4-5 min (UXG-05): scripted play, needs a hand check; no number.
14. Salvo Array about a quarter of the other superweapons per charge minute (LE report question 6): no target value.
