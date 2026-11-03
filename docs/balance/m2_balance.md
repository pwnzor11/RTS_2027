# M2 balance pass (M2-14)

Goal (design 04): a skirmish against the Recruit AI lasts about 10 minutes, every unit has a job, power matters.
Method: `ai match` / `ai timeline` / `ai duel` (all in `src/Tools/AiCommands.cs`), Release build, real data in `data/rules`.
Time: 20 ticks = 1 second, so 9,600-15,000 ticks = 8-12.5 minutes. All numbers are integers.

Everything below can be reproduced with:

```
Tools ai match    --a recruit:1 --b recruit:1 --seeds 8 --ticks 30000 --starts alt
Tools ai match    --a recruit:1 --b idle      --seeds 4 --ticks 30000
Tools ai match    --a recruit:1 --b recruit:1 --seeds 24 --first 25 --ticks 30000      (new --first: first seed)
Tools ai timeline --a recruit:1 --b recruit:1 --seed 1 [--swap 1] [--trace 1000] [--dump 1]
Tools ai duel     --a auth_tank:3 --b auth_tank:1,auth_turret:1 --ticks 4000          (spec = id[:n][,id[:n]...])
```

In a mirror pair both games of a seed are the same game with the AIs' labels swapped (same corners, same per-slot AI
seeds), so "distinct games" below means one per seed. `--starts alt` trades corners between slots on even seeds (D-063).

## 1. Result tables (before / after)

### Recruit vs Recruit, `--seeds 8 --ticks 30000 --starts alt` (16 games = 8 distinct)

| | Games | A wins | B wins | Draws | Avg end tick | Decisive distinct games |
|---|---:|---:|---:|---:|---:|---:|
| Before | 16 | 5 | 5 | 6 | 22,215 | 5 of 8 (62%): seeds 3, 4, 8 drew |
| After | 16 | 8 | 8 | 0 | 15,094 | 8 of 8 (100%) |

End ticks per seed (distinct game). Before: 19,740 / 18,048 / draw / draw / 10,156 / 21,980 / 17,800 / draw.
After: 15,796 / 14,812 / 10,772 / 13,388 / 10,400 / 11,044 / 26,520 / 18,024.

Larger sample (8 seeds is too small to trust): 48 distinct games, seeds 1-48, same command with `--seeds 24 --first 1` and
`--first 25`, final data:

| Games (distinct) | Decisive | Draws | Avg end, draws counted at 30,000 | Avg end, decisive games only |
|---:|---:|---:|---:|---:|
| 48 | 45 (94%) | 3 (seeds 11, 21, 34) | 14,811 (12.3 min) | 13,799 (11.5 min) |

Slot and corner fairness stays even (8 vs 8 wins, 4 vs 4 by start corner, 4 vs 4 by slot in the 8-seed table).
The three draws are all "both scrapers dead, both armies gone, 8 structures each" (see section 5).

### Recruit vs idle, `--seeds 4 --ticks 30000`

| | Games | A wins | Draws | Avg end tick |
|---|---:|---:|---:|---:|
| Before | 8 | 8 | 0 | 6,697 (5.6 min) |
| After | 8 | 8 | 0 | 4,859 (4.0 min) |

Funds extremes per game (before and after): min 0, max 4,990-4,995 (the 5,000 start cash less the first plant), i.e.
the build order spends the whole start cash within the opening and plays on income afterwards. Structures built per game
were 8-10 before and 8-9 after; "units ever seen" per side went from 10-39 before to 54-140 after (the economy pays for
5-6x more units, see section 4).

### Funds and income curves (seed 1, Recruit vs idle for "before"; mirror for "after"; cash + stored)

| Tick | Before: funds | Before: cumulative income | After: funds (A / B) | After: cumulative income (A / B) |
|---:|---:|---:|---:|---:|
| 1,200 | 610 + 1000 | 930 | 1700 + 1000 / 1655 + 1000 | 976 / 976 |
| 2,400 | 0 + 300 | 2,545 | 0 + 320 / 0 + 220 | 2,076 / 2,076 |
| 3,600 | 0 + 590 | 3,045 | 0 + 760 / 0 + 1617 | 6,674 / 8,090 |
| 4,800 | 0 + 0 | 3,545 | 0 + 1155 / 0 + 700 | 11,303 / 11,354 |
| 6,000 | 0 + 790 | 4,435 | 0 + 394 / 0 + 810 | 13,873 / 15,514 |

Before, one scraper earned about 0.5 credits per tick over the game (a 500-credit load every ~1,000 ticks, then stalls
of thousands of ticks when its home patch was used up); after, about 2 per tick on a rich map. Seed 1 is a rich map
(858 density units); poor maps (378 units, e.g. seed 8) run dry sooner, see section 5.

## 2. Time-to-first-X (target: income within ~60 s, first tank 2.5-3.5 min)

Final data, seeds 1-8, both slots (16 values). One tick = 0.05 s.

| Event | Min | Median | Max | Target |
|---|---:|---:|---:|---|
| First harvester income (credits arrive) | 1,012 (51 s) | 1,054 (53 s) | 1,081 (54 s) | within ~60 s: met |
| Factory finished | 2,044 | 2,224 | 3,244 | - |
| First tank | 2,604 (2.2 min) | 3,084 (2.6 min) | 4,524 (3.8 min) | 2.5-3.5 min: median met; 12 of 16 inside 2.2-3.3 min, 3 outliers (4,400-4,524) where the factory or cash came late |
| First jeep | 3,024 | 3,324 | 6,684 | - |
| First damage anywhere (first contact) | 3,076 (2.6 min) | 3,692 | 4,724 | - |

Before (original data and AI, seeds 1-3, both slots): first income 1,004-1,073; first tank 3,464 / 4,888 (seed 1),
~4,544 (seed 2), 3,816 / 5,924 (seed 3), i.e. 3,464-5,924 (2.9-4.9 min); first damage 3,828-5,356. The old AI built
two jeeps before its first tank and starved its own economy for 1,000-3,000 ticks when the home patch ran out.

## 3. Per-unit role check (1v1 / group duels, `ai duel`, flat map, groups 12 cells apart, attack-move toward each other)

Costs: rifleman 100, rocketeer 300, jeep 350 (was 400), tank 800, turret 400. "Left" = survivors (hp left / hp at start).

| Role | Matchup | Before (original data) | After (final data) |
|---|---|---|---|
| Rifleman: cheap anti-infantry | 3 rifleman vs 1 rocketeer | rifles win, 3 left | rifles win, 3 left (120/150 hp) |
| | 1 jeep vs 1 rifleman | jeep wins | jeep wins (130/150) |
| | 5 vs 5 riflemen | mutual kill | mutual kill (D-065: no slot-order bias) |
| | 8 rifleman vs 1 tank | tank wins (262/400) | tank wins (237/400): riflemen are not an anti-tank answer |
| Rocketeer: anti-armour | 1 rocketeer vs 1 tank | tank wins (220/400) | rocketeer wins by a hair (6/60): roughly even at 300 vs 800 credits |
| | 2 rocketeers vs 1 tank | tank wins (40/400) | rocketeers win, both alive (84/120) |
| | 6 rocketeers vs 3 tanks | tanks win, 2 left | rocketeers win, 1 left (33/360) |
| | 3 rifleman vs 1 rocketeer | see above | rifles win: the rocketeer needs an escort |
| Jeep: fast scout / harass | 1 jeep vs 1 rocketeer | jeep wins (60/150) | jeep wins (75/150) |
| | 1 jeep vs 1 rifleman | jeep wins | jeep wins |
| | 3 rifleman vs 1 jeep | jeep wins (38/150) | jeep wins (46/150): needs 4+ riflemen, so harass lone infantry, avoid groups |
| | 3 jeeps vs 1 tank | tank wins (250/400) | tank wins (280/400): jeeps cannot fight armour |
| Tank: main battle unit | 1 tank vs 2 jeeps | tank wins | tank wins (338/400) |
| | 5 tanks vs 5 tanks | 3 left (360) | 3 left (300): decided by focus, simultaneous damage |
| | 3 tanks vs 3 rocketeers | (not run) | tanks win, 2 left: tanks close the gap, rocketeers need range and escorts |
| Turret: holds a choke vs a few units, not an army | 1 tank vs 1 turret | tank wins untouched (400/400, the turret never fired: tank range 1280 > turret 1152) | tank wins narrowly (136/400) |
| | 2 tanks vs 1 turret | tanks win, untouched | tanks win (602/800) |
| | 6 tanks vs 1 turret | tanks win, untouched | tanks win (2304/2400), 8 s |
| | 2 jeeps vs 1 turret | turret wins (338/400) | turret wins (305/400) |
| | 3 or 8 riflemen vs 1 turret | turret wins untouched | turret wins untouched (392 / 373 of 400) |
| Mixed wave vs defended base | 6 tanks + 4 rifle + 3 rocket vs 4 tanks + 2 rocket + 3 rifle + turret | attackers win, 4 left | attackers win, 7 left (21 s) |

The turret is a one- or two-light-unit stopper and a speed bump for armour; it counters infantry and jeeps (splash
`auth_he`) but a handful of tanks takes it down in seconds. With low power it is off (section 6).

## 4. Changed numbers (before -> after, one-line reason)

### `data/rules/game.toml` [economy]

| Key | Before | After | Reason |
|---|---:|---:|---|
| credits_ashfall | 25 | 150 | One 20-unit load was 500 credits and took ~1,000 ticks round trip: a tank every ~1,600 ticks and 6-11 unit armies. 150 per unit (3,000 per load) gives 12-20 unit waves and a ~10 minute game. |
| credits_pitchblende | 60 | 300 | Keeps the "rich tier" at twice ashfall (it was 2.4x at 25/60, and would be below ashfall at 150). |
| regrowth_interval_ticks | 0 | 1200 | Off meant used cells never recovered and the home patch ran dry by ~2,000 ticks (seed 8: income froze at 2,035 credits). On: cells that still hold ash gain 1 density per minute. |
| build_ticks_per_100_credits | 20 | 40 | Doubles every cost-derived build time (tank 160 -> 320 ticks) so the opening reads as a build-up and the first tank lands at ~2.6 min instead of ~1.7. |
| starting_cash, harvest/unload ticks, sell/repair, build_radius, max_queue, low_power_slowdown | unchanged | | Already fine: 5,000 funds the opening, [1500, 2500, 4000] is the C&C curve (x1.5 / x2.5 / x4), see section 6. |

### `data/rules/buildings.toml`

| Id | Key | Before | After | Reason |
|---|---|---:|---:|---|
| auth_refinery | build_time (new) | derived 800 at 40/100 | 400 | Keeps first income inside ~60 s despite the slower generic build times (scraper ships with the refinery). |
| auth_conyard | max_health | 1500 | 1000 | Shorter finish: after a won fight an army needs ~1 minute, not ~3, to end the game (all structure hp -30% to -35%). |
| auth_power | max_health | 500 | 400 | same |
| auth_refinery | max_health | 900 | 700 | same |
| auth_barracks | max_health | 600 | 450 | same |
| auth_factory | max_health | 1000 | 800 | same |
| auth_silo | max_health | 300 | 250 | same |
| auth_turret | weapon (see weapons) | | | turret and wall hp, costs, power draws unchanged |

### `data/rules/vehicles.toml` and `infantry.toml`

| Id | Key | Before | After | Reason |
|---|---|---:|---:|---|
| auth_scraper | speed | 14 | 18 | Round trips to a far field were the income limit. 16 is NOT usable: a scraper with speed 16 never harvests (section 7). |
| auth_jeep | cost | 400 | 350 | Cheap fast harasser; 3 riflemen should still beat it, tanks must beat 2 of them. |
| auth_rifleman | speed | 10 | 12 | Infantry arrived 2-3 times later than tanks; waves strung out and died piecemeal. |
| auth_rocketeer | speed | 8 | 10 | same |

### `data/rules/weapons.toml` and `warheads.toml`

| Key | Before | After | Reason |
|---|---:|---:|---|
| auth_rocket damage | 60 | 100 | A tank is four rockets; makes 2 rocketeers (600) beat a tank (800). |
| auth_rocket range | 1024 | 1280 | Equal to the tank cannon: before, a tank got free shots and 6 rocketeers lost to 3 tanks. |
| auth_rocket rate_of_fire | 60 | 75 | Keeps a lone rocketeer about even with a tank at 300 vs 800 credits. |
| auth_rocket projectile_speed | 64 | 128 | A shell/rocket detonates on the cell the target was in when it was fired; half the flight time halves the misses on moving targets. |
| auth_tank_cannon damage | 40 | 60 | Tank vs tank in 12-15 s instead of ~20; larger fights decide faster (Lanchester), fewer mutual annihilations. |
| auth_turret_gun damage | 30 | 20 | At 30 and splash, a turret beat 6+ riflemen and made every base impregnable to waves of 10 (draws). |
| auth_turret_gun range | 1152 | 1280 | Was shorter than the tank's 1280, so a tank killed it from outside its range without a scratch. Now equal. |
| auth_ap versus Unarmored | 25 | 15 | A projectile hits everyone in the impact cell and infantry stand 5 to a cell, so a cannon shell hit a whole squad; 15 stops tanks erasing rocketeer squads. |

### `data/ai/difficulty.toml` (recruit row only)

| Key | Before | After | Reason |
|---|---:|---:|---|
| attack_threshold | 13 | 10 | Base trigger for a wave; the AI adds a per-game bias and per-wave jitter on top (section 5), so the effective trigger is 10-14. Other tiers untouched. |

Unchanged on purpose: all other costs (rifleman 100, rocketeer 300, tank 800, scraper 700, structures), health of
units and turret/wall, power numbers, tech chain, small-arms and HE warheads, jeep and rifle weapons, scout/decision
numbers.

## 5. Recruit AI changes (`src/AI/Recruit/**`, Recruit only, deterministic, still honest)

1. **Seeded variation** (the AI had an `ai`-seeded generator and used it nowhere). Per game: a patience bias 0-3 extra units
   every wave waits for; per wave: +0..2 jitter on the army-size trigger and 0-1,000 extra ticks on the timed trigger (base 3,000
   ticks plus 200 per bias point); the vehicle and infantry lanes build from shuffled bags (vehicles: a tank first, then
   three tanks and a jeep per bag; infantry: two riflemen and two rocketeers per bag). Two Recruits in one game
   therefore rarely send equal armies, which turns mirror games decisive.
2. **Waves are rosters with a staging point** (`MilitaryManager`). A wave takes the units alive at launch; units built later
   wait at the rally cell for the next wave (before, each new unit trickled to the front alone and died). The roster first
   gathers 16 cells short of the target (70% arrived or 1,800 ticks), then attack-moves to the nearest known enemy building.
   Timed wave minimum 6 -> 8 units.
3. **Stalled scraper probes** (`EconomyManager`). The sim's harvester only searches 16 cells around its last field, so a
   scraper whose home patch ran out (or was built over) sat still for ever. When a scraper on a Harvest mission has stood
   on one cell for 400 ticks, it gets a Harvest order with a probe cell (coarse 8-cell grid, nearest first, not next to the
   enemy start) as its new search centre. It never reads the overlay, it just goes and looks.
4. **No scraper left = no hoarding** (`ProductionManager`, `MilitaryManager`). The unit lanes drop the reserve they keep
   for the next structure or tank and build the dearest bag item the money covers; a side with at least 3 units and no
   scraper sends them out at once. Recruit still never replaces a scraper (design 02 section 7).
5. The first vehicle is a tank (was jeep, jeep, tank): jeeps are a minority role now.

Why these drive the targets: with the old data and AI, equal waves arrived piecemeal against a base with a turret that
out-ranged nothing, lost, and the survivors sat on cash they would not spend; 3 of 8 maps froze economically before the
first real fight.

## 6. Power

Unchanged data, verified in the sim code: a power shortage of any size sets `PowerBand >= 1`, which (a) multiplies build
ticks by `low_power_slowdown_permille[band-1] / 1000` (x1.5 below 100% output, x2.5 below 75%, x4 below 50%; a tank goes
320 -> 480 / 800 / 1,280 ticks with one factory) and (b) makes `needs_power` defences hold fire
(`RunBuildingCombat`). A full base uses 120 (power 100 each): with one plant a base is at 83% and its turret is silent,
so power plant #2 is in the Recruit build order (and the AI inserts a plant when the next structure would run short).
Killing power plants is a real attack on a base: base with 100/120 in the 48-seed runs had its turret offline and
production x1.5 until it rebuilt.

## 7. Findings that need a sim or rule change (not done here, `src/Sim` is not mine)

1. **Scraper speed 16 deadlocks harvesting.** With `auth_scraper` speed = 16 (exactly 16 ticks per cell) the scraper never
   delivers (seeds 1, 2: first income never; seed 3: tick 5,154 instead of ~1,000). 14, 18 and 20 work. Probably a
   position/arrival edge case in `MoveMission`/dock; needs a sim test with `speed = 16`.
2. **`HarvestMission.SearchRadius = 16`** (cells around the last field or the current cell) means a human player's scraper also
   goes idle when the home patch is used up and the next field is further away. The Recruit works around it with probe
   orders; the game should at least fall back to a wider or map-wide nearest-field search.
3. **Projectiles hit every eligible actor in the impact cell** (`Detonate`, splash 0) and infantry stack 5 per cell, so a tank
   shell is a free 5-target splash on a squad. Compensated with `auth_ap` versus Unarmored 25 -> 15; worth a design note.
4. **Finite resources.** Ashfall does not spread and an emptied cell stays empty (D-067.5); maps hold 378-918 density units,
   i.e. 57k-137k credits at 150 per unit shared by both sides, which is enough for a ~12 minute game but not for 25 minutes.
   Late-game stalemates (draws) are two economies that have run dry. Seed markers / spreading would cure it.
5. `EconomyRules` XML summary says the low-power slowdown is "added to 1000"; the code (and the design) use it as the
   multiplier itself (1500 = x1.5). The comment is stale.
6. `PlayerView` offers no overlay (resource) accessor, so no AI can see a field before it drives near it.

## 8. Tools added (`src/Tools/AiCommands.cs`)

- `ai duel --a <spec> --b <spec> [--count N] [--count-b N] [--ticks T] [--gap G]`: two groups of units (and optionally a
  powered turret) attack-move toward each other on a flat map; prints winner, time and survivors.
- `ai timeline --a .. --b .. --seed S [--swap 0|1] [--trace N] [--dump 1]`: one game with first-income tick, first tick each
  type was owned, first damage, funds and income every 1,200 ticks, per-slot army/structures/scraper trace, harvester state.
- `ai match --first N`: first seed (to run seeds 25-48 as a second pass, or two halves in parallel).

## 9. Status

| Target | Result |
|---|---|
| Recruit vs Recruit decisive in >= 75% of games | 94% (45 of 48 distinct games); 100% on the 8-seed table |
| Average end 9,600-15,000 ticks | 14,811 over 48 games with draws at 30,000; 13,799 for decisive games; the 8-seed table is 15,094 (one long game, seed 7: 26,520) |
| First harvester income within ~60 s | 51-54 s |
| First tank 2.5-3.5 min | median 2.6 min (range 2.2-3.8) |
| Unit roles | section 3 |
| Power matters | section 6 |
| Recruit beats idle | 8 of 8, avg 4,859 ticks (AI tests: 28 of 28 pass) |

## 10. After M2-19 (sim fixes from section 7)

Fixed in the sim: (1) speed 16 harvesting (a diagonal step landing exactly on a cell corner dropped the movement reservation,
so any speed dividing 256 could freeze a vehicle); (2) the harvester field search now widens ring by ring to the whole map
(8 rings per tick) when the near search (16 cells) finds nothing, so the Recruit's probe orders are redundant but harmless
(`src/AI/Recruit/EconomyManager.cs` may drop them); (3) a splash-0 shell hurts one actor of the impact cell (its target if it is
still there, else the first eligible by slot order); (4) effects events; (5) the stale `EconomyRules` comment.

AP vs Unarmored: `ai duel --a auth_tank:3 --b auth_rifleman:10` took 11 s with 1122/1200 hp left under the old rule (value 15).
With single-hit shells and value 15: 45 s, 877 hp left. With single-hit shells and the original 25: 31 s, 992 hp left.
Tanks are not useless against infantry either way, so the compensation was reverted: **`auth_ap` vs Unarmored is 25 again**.

`ai match --a recruit:1 --b recruit:1 --seeds 8 --ticks 30000 --starts alt` (Release): 16 games, A 8 / B 8 / draws 0,
average end tick 14,829 (was 15,094), longest game 23,224 (seed 7; was 26,520); wins by start corner 6 / 10.
