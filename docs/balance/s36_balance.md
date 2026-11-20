# Session 36: Ash Plains start bias (M13-31) and the commander ablation (M5-14)

All runs on the home cluster (laptop k3s-cluster folder `claude/rts/s36/`: build*.sh, fair.sh, glass.sh, abl.sh, guard*.sh, test.sh), every
node including the Pis (the Pis played 62 of the 158 fairness pieces that logged a time). Mirror probe and ablation option: commits f987d15 and f47b748 on integ-s36.

## M13-31: why Ash Plains layout 1 favours one start
`Tools ai mirror` (s32) imaged player 1 by a half turn only; it now images by the map's own symmetry (rot2, mirror_x or mirror_y, the one with
no differing cells). On Ash Plains (mirror_y: starts at the top and bottom centre, (47,6) and (47,89) on a 96 map) the two sides stop being
mirror images at tick ~960, on the first power plant: the top AI puts it behind its yard (y 2), the bottom AI in front of its yard (toward the
enemy). The cause is that **every building exits and docks to the south** (`Exit`/`Dock` and the lanes PlacementSearch keeps free run south):
the top base's yard, factory and refinery lanes face the enemy and the open middle; the bottom base's face the map edge 4-6 rows away, so it
builds toward the enemy and its units leave toward the edge. On Mountains (mirror_x, starts left and right) the same south exits are mirror
images of each other, and the s35 screen found it fair. D-270 (s32) had already named south-facing buildings as the remaining start bias.

## M13-31: the screen (job f36)
Mirror matches (same faction both sides), tiered:3 v tiered:3, size 96, layout 1, 30 000 ticks, seeds 1-64 x both starts (128 games per
faction row, 512 per map). Cell = wins from start 0 v start 1 (draws left out); z from 50 %.

| Map, symmetry | Authority | Directorate | Expedition | Left Behind | All four |
|---|---|---|---|---|---|
| Ash Plains, mirror_y (main) | 80-38 (67.8 %) | 62-46 (57.4 %) | 85-43 (66.4 %) | 91-28 (76.5 %) | 318-155, **67.2 % (z +7.5)** |
| Ash Plains, **mirror_x** | 55-65 (45.8 %) | 56-56 (50.0 %) | 56-55 (50.5 %) | 58-44 (56.9 %) | 225-220, **50.6 % (z +0.2)** |
| Ash Plains, rot2 | 64-54 (54.2 %) | 65-51 (56.0 %) | 64-48 (57.1 %) | 73-43 (62.9 %) | 266-196, 57.6 % (z +3.3) |
| Glass Coast, rot2 (main) | 45-62 (42.1 %) | 47-36 (56.6 %) | 65-44 (59.6 %) | 58-38 (60.4 %) | 215-180, 54.4 % (z +1.8) |
| Glass Coast, mirror_x | 49-52 (48.5 %) | 37-35 (51.4 %) | 50-56 (47.2 %) | 49-52 (48.5 %) | 185-195, 48.7 % (z -0.5) |

- **Ash Plains goes to mirror_x** (D-277): the lean disappears (67.2 % -> 50.6 %). rot2 only halves it (diagonal starts still put one base's
  exits toward the middle). No balance-table row plays on Ash Plains, so the table is unchanged.
- **Glass Coast stays rot2.** mirror_x removes its milder lean too, but the three Glass Coast rows of the balance table (job g36, 128 games
  each, same cells as s35) move a lot: Expedition v Authority 52.8 % -> **19.8 %** (24-97-7), Expedition v Directorate 54.8 % -> 50.5 %,
  Expedition v Left Behind 69.0 % -> 59.3 %. Balance is parked (D-269) and the Glass Coast lean is within noise (z +1.8 over 395 decided
  games), so the change was taken back. If balance work reopens, the Expedition on a left/right Glass Coast is the place to look.
- Layout 2 has its own symmetry (rot2 or mirror_diag per seed) and is unchanged; 4-player maps stay rot4.
- Tests: the two layout-1 symmetry pins and the Ash Plains layout-1 map hash were updated; the whole suite passed on the cluster otherwise.

## M5-14: which commander directives cost games (job a36)
The D-241 set: tier 3 v tier 3, default factions, map 64, 30 000 ticks, side A commanded, seeds 1-64 x both starts (128 games per row). New
option `ai commander --random-directives --random-only <fields>`: the random commander still draws every value (same stream), but only the
listed fields reach the game, the rest are the plain values (balanced, army, auto mix, no strong mix, no expand, no focus cell, no orders of
the other kinds). So every row is paired game for game with the "none" row (a directive with plain values). `--random-only all` reproduces the
old control byte for byte. Score = (wins + draws/2) / games for side A.

| Random fields kept | W-L-D | Score | Paired v none: better / worse games |
|---|---|---|---|
| none (plain values) | 62-43-23 | 57.4 % | |
| plain AI, no commander (`ai match`) | 58-51-19 | 52.7 % | |
| posture | 55-51-22 | 51.6 % | 31 / 42 (z -1.3) |
| posture + focus cell | 53-54-21 | 49.6 % | 27 / 43 (z -1.9) |
| **focus** | 38-69-21 | 37.9 % | 24 / 51 (z -3.1) |
| mix | 73-40-15 | 62.9 % | 34 / 25 (z +1.2) |
| mix + strong | 70-43-15 | 60.5 % | 34 / 29 (z +0.6) |
| expand | 67-46-15 | 58.2 % | 38 / 37 (z +0.1) |
| whole directive | 26-80-22 | 28.9 % | 18 / 62 (z -4.9) |
| AttackAt orders | 58-50-20 | 53.1 % | 18 / 26 (z -1.2) |
| **HoldCell orders** | 25-64-39 | 34.8 % | 11 / 48 (z -4.8) |
| Raid orders | 63-53-12 | 53.9 % | 21 / 26 (z -0.7) |
| ExpandTo orders | 64-53-11 | 54.3 % | 13 / 21 (z -1.4) |
| **TechTo orders** | 46-69-13 | 41.0 % | 23 / 48 (z -3.0) |
| **Reserve orders** | 34-78-16 | 32.8 % | 11 / 48 (z -4.8) |
| all orders | 27-92-9 | 24.6 % | 13 / 62 (z -5.7) |
| everything (the old control) | 25-94-9 | 23.0 % | 11 / 62 (z -6.0) |

- A directive with plain values costs nothing (57.4 % v 52.7 % for the plain AI, within noise), so the loss in the s29/s35 control is in the values.
- The four costly parts all **hold credits or the army back**: HoldCell stops every wave while it stands; Reserve keeps up to 1000 credits from
  units, structures and research; TechTo puts the tier gates in at once, regardless of army; and the focus values (economy, tech, air,
  superweapon) keep a 150-300 credit unit reserve and pull the gates forward. Posture, mix, expand, AttackAt, Raid and ExpandTo are harmless
  or slightly useful.

## M5-14: the guard (jobs gd36, gd36c)
Same paired set and rows; each variant is a code change in `src/AI/Recruit/DirectiveGuard.cs` (s36). Score and paired counts v the unguarded
row of the same fields (a36) and v "none".

| Variant | hold | reserve | tech | focus | everything |
|---|---|---|---|---|---|
| unguarded (S24) | 34.8 % | 32.8 % | 41.0 % | 37.9 % | 23.0 % |
| G1: drop hold/reserve/tech/focus-reserve while the army is **behind** the seen enemy army | 35.9 % | 39.5 % | 41.0 % (no game changed) | 39.5 % | 23.8 % |
| G2: G1, and HoldCell only moves the rally (never stops waves); run cut short: hold 125, tech 101, focus 106 games | 44.8 % | 39.5 % | 42.6 % | 39.2 % | (cut short) |
| G3: G2, and the parts also wait for the AI's own **army_before_tech** army | 43.8 % | **57.8 %** | **56.6 %** | **56.6 %** | 50.8 % |
| **G4 (kept)**: G3, and HoldCell also waits for that army | **57.4 %** | (= G3) | (= G3) | (= G3) | **53.1 %** |

- G1 triggers rarely and with no clear gain (paired v unguarded z +0.2 to +1.8; TechTo games identical): the costly parts do their damage early, before the AI has seen an enemy army.
- The AI's own rule (s12: no tech spending before `army_before_tech` units; Left Behind exempt) is the right gate. G3 paired v unguarded:
  reserve 49 better / 12 worse, tech 47 / 22, focus 50 / 20, everything 53 / 11 (z +5.2). G4 v G3 on hold: 34 better / 9 worse (z +3.8).
- With G4 the full random commander scores 53.1 %, level with the plain AI (52.7 %) instead of 23 % below it: a commander's directives no
  longer cost the AI games by themselves, so a model's choices can now show whether they help.
- Plain AI games (no directive, no orders) are unchanged game for game (the base row of the guarded build equals a36's).
- The model rerun (qwen3:30b on the NAS, the s35 32-game set, job cm36) and its control (cc36) were started at the end of the session; results
  in the next session (`/work/out/s36cmd/`).
