# Session 47: map layout 2, status and tuning plan (A-06, D-335)

Owner goal (D-335): finish layout 2 to the layout-1 fairness bar (six faction pairings pooled 40-60 %, every per-map row 25-75, aim 35-65). It stays an
opt-in lobby option; layout 1 stays the default (`MapGenerator.CurrentLayout = 1`). This note only prepares the levers and the series; no balance number moved.

## 1. What layout 2 generates today

2 players, Medium and up: a **half-turn skeleton** (start B is start A turned 180 degrees, so the map itself is symmetric; any unfairness is faction against geometry).
One S-shaped river runs between the bases, two decks (bridges) and a ford at the centre, three monotone lanes (north-deck lane, ford lane, south-deck lane), a plaza
at the ford with a gravel-bar field, a contested yard by each deck, a natural-expansion yard on 40 % of seeds, and the whole map then turned by one of the eight square
symmetries (`LayoutVariant`). 3-4 players use a different four-start zone skeleton (`ProceduralBuilder.Zones.cs`), not covered here. The main's shell is drawn per seed from a
roll of 12: **soft shell** 8/12 (leaky, no ramp), **walled main with two gates** 2/12, **walled main with one east ramp** 2/12 (Mountains: always walled, 7/12 two gates, 5/12 one ramp).
Main fields: two strips along the far east wall, at least 7 cells from the start, richness 2 against 4 elsewhere (`Layout2Fields`).

| Biome | Painting (`ProceduralBuilder.Skin.cs`) | Render |
|---|---|---|
| rust_belt | ruined town: rubble blocks as lane walls, road grid | img/s47_layout2_rust_belt.png |
| glass_coast | dune crests with rough flanks, lagoons, sea behind the mains (water edge up to the main) | img/s47_layout2_glass_coast.png |
| mountains | boulders and scree, cliff ridge instead of a river, plateau natural; mains always walled | img/s47_layout2_mountains.png |
| ash_plains | ash ground, crater fields, burnt groves, a broken causeway beside each bridge (D-198 half-turn) | img/s47_layout2_ash_plains.png |

Renders: `map render --biome <b> --size m --players 2 --seed 1 --layout 2 --png ... --scale 5` (seed 1, Medium, looked at all four: the half-turn symmetry,
river with deck and central ford, the two main field strips inside each walled main and the yards are all visible).

### The three owner blockers (D-197), status checked in code and the newest tables
| Blocker | Status |
|---|---|
| 1. Expedition coast in every biome | Built: when an Expedition is in the match the zoned generator puts sea behind every main (`_sea`, Rot2 `amax`). Ash Plains stall fixed in s17. Smoke today (this package): Expedition v Authority tier 3 on ash_plains and mountains layout 2, 2 seeds x 2, 12 000 ticks: games play, no stuck side (A ahead on the draw tiebreak 3 of 4 on ash). Layout 1 Mountains Expedition is the known near-dead case (not layout 2). **Met** (re-check inside the series: income column). |
| 2. Start side <= 60 % in tier-3 mirrors | s38 mirrors: start-0 share 43-53 % Rust, 42-47 % Glass, 44-53 % Mountains; Ash row never finished. **Met on three biomes, Ash open** (D-198 half-turn should fix its 64 %; unmeasured). |
| 3. Income within +-10 % of layout 1 at 3 / 5 min (10 min live games, D-201) | Last measure s16 (v5), v6 (D-199) never re-measured: +40 to +56 % at 10 min Rust/Mountains Authority and Directorate, Glass +100 %. **Unmet / unmeasured.** Lever: section 3. |

## 2. Why the old table was unfair, and the levers

Old table (s38, tier 3 v 3, 128 games per row; Auth v Dir stored as Authority win %): Auth v Dir 14-30 %, Auth v LB 84-89 %, Auth v Exp 44-55 %, Dir v LB 78-88 %, Dir v Exp 86 %, LB v Exp 34-41 %.
Layout 1 at the time (s37/s46 r45, pooled): Auth v Dir 34 %, Auth v LB 50 %, Dir v LB 45 %, Dir v Exp 49 %. So layout 2 turned the **Directorate from the weakest into the strongest faction** (it wins 70-88 % against Authority, Left Behind and Expedition) and put the **Left Behind at the bottom**. Reading, with evidence level:
- Measured (s16): layout-2 economy is far richer (+40-56 % per minute at 10 min). The Left Behind gains most (+53-74 %) and the Directorate's gain is capped by its storage (s46 note), so income does not explain Dir's win alone.
- Probable (not tested): the map is a defender's map. Lanes are long, monotone and narrow with walls (`_wL1` 6-8, `_wL2` 13-16 cells), mains sit behind one or two gates, the fords are the only vehicle crossings. That favours the Directorate's range and static defence, and punishes
  the Authority and Left Behind pushes through the chokes. s16 saw 10-15 of 32 draws in Directorate mirrors (defended-base stalemate).
- Rules changed since s38 (D-286, D-294, D-305, D-330: Directorate warheads, Authority ash resist, Marine and Glide Tank costs). **The s38 table is stale: the first step is a fresh baseline, not a tuning step.**

Levers that exist (all produce layout-2 maps only; layout-1 hashes verified unchanged, section 3):
| Lever | Where | Expected direction |
|---|---|---|
| `shell_soft` / `shell_gates2` (new, twelfths) | biomes.toml `[biome.<k>.layout2]` | more soft shells = fewer walled mains: Directorate (turtle) down, Authority and Left Behind pushes up. Pairings that should move: Auth v Dir up, LB v Dir up, Dir v Exp down. Largest expected effect on Rust, Glass, Ash (Mountains stays walled). |
| `main_share`, `main_cells_pct`, `main_dist` (now in data) | same table | less money in the main / farther fields = income toward layout 1. Faction with the most to lose: the Left Behind and the Expedition (income-bound) ; Directorate least (storage cap). Expect LB v Dir and LB v Auth to fall slightly, Auth v Exp up. |
| `natural_pct` (now in data) | same table | 0: no expansion yard, everyone mains-bound (long games, fewer swings); 100: all get one. Weak, a diagnostic. |
| `main_bands` (now in data) | same table | 1 strip along the east wall: fields concentrated, more raid-able; secondary. |
| Biome keys shared with layout 1 (`rock_permille`, `ford_spacing`, ...) | biomes.toml | NOT usable: they change layout 1 (hash pins). |
| Lane widths, gate widths, deck width (`_wL1`, `_wL2`, `_gapE`, `_bridgeW`, `_hw`) | code constants in `Rot2.cs` | not data; a wider lane/gate would favour the pushing factions but needs a new key. Only if the shell mix is not enough (see QUESTIONS). |
| `--handicap-a/-b` | `ai match` | a measuring tool, not a fix. |

## 3. Lever added (small, data only)

`[biome.<key>.layout2]` in `data/mapgen/biomes.toml`, optional; each key absent = today's value; table absent = today's map byte for byte.
Keys (ranges validated, listed in problems like the other biome keys): `main_dist` 1..20 (7), `main_bands` 1..2 (2), `main_share` 1..16 (2), `main_cells_pct` 20..200 (100),
`natural_pct` 0..100 (40), `shell_soft` 0..12 (8; Mountains 0 and must stay 0), `shell_gates2` 0..12 (2; Mountains 7); `shell_soft + shell_gates2 <= 12`, the rest is the one-ramp walled main.
The seed roll `_rng.Next(12)` is unchanged, so only the thresholds move. Reader: `BiomeCatalog.Layout2FieldsFor` (replaces the hard-coded switch; `Layout2Defaults` keeps the old values),
used by `ProceduralBuilder.Rot2.cs` / `Zones.cs`. Test: `tests/Sim.Tests/Map/MapGenLayout2KeysTests.cs` (defaults equal v6 values; overrides parse; bad values listed;
24 map hashes, 4 biomes x seeds 1-3 x layout 1 and 2, captured before the change, still equal). Shipped biomes.toml has no layout2 table.

## 4. Series for the lead

Common: `ai match --a tiered:3 --b tiered:3 --faction-a <A> --faction-b <B> --layout 2 --biome <b> --size 96 --ticks 30000 --income 1 --seeds 8 --first <1+8k> --out ...`;
Medium (96), 2 players, tier 3 v 3, 20 ticks per second, income on (prints harvested credits at 3 / 5 / 10 min), chunks k = 0..7 give seeds 1-64 = 128 games per row (like s46). One game is about 10 s per core; layout-2 draw rate may be high, hence 30 000 ticks.
Run `dotnet run --project src/Tools -c Release -- ai match ...` or the built dll. Variants are applied by editing the `data/mapgen/biomes.toml` copy the tool finds above its binaries (one copy of the build per variant).

Rows (24 pairing rows): factions {authority, directorate, left_behind, expedition} pairs (A v B): auth/dir, auth/exp, auth/lb, dir/exp, dir/lb, lb/exp, each on rust_belt, glass_coast, mountains, ash_plains.
Mirror rows (start fairness, 16): X v X for the four factions on four biomes, 64 seeds. Gate: pooled pairing 40-60, row 25-75 (aim 35-65), mirror start share <= 60 %.
Income reference: the layout-1 numbers in s17_layout1_baseline.md are stale; re-run 8 seeds of layout 1 (`--layout 1`) mirror rows for income.

Screen order (data only, one key set each, chunks 0-7, pooled over biomes first, then per biome, like s46):
- R0 baseline: defaults, all 24 + 16 rows. Everything else is read as a diff against R0.
- S1 `shell_soft = 12, shell_gates2 = 0` (Mountains: `shell_gates2 = 12`): all soft shells. Tests the walled-main hypothesis. Expect Auth v Dir and LB v Dir up.
- S2 `shell_soft = 0, shell_gates2 = 0` (Mountains: `shell_gates2 = 0`): all one-ramp. Bracket opposite S1; run on 2 biomes only.
- I1 `main_share = 4, main_cells_pct = 70`: income toward layout 1 (blocker 3); read with the `--income` columns.
- I2 `main_dist = 10, main_cells_pct = 100`: farther fields, income only.
- N1 `natural_pct = 0` and N2 `natural_pct = 100`: only after S1/I1, as a fine knob.
Then combine the winners (e.g. S1 + I1), decide on 512 games per row as in s46. If R0 shows Directorate still above 60 % everywhere with S1, the next lever is code (lane and gate widths), see QUESTIONS.
Cost: R0 = 40 rows x 128 games = about 14 core-hours at 10 s a game; each screen variant (24 rows) about 8.5 core-hours.
