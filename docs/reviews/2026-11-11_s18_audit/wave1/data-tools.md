# data-tools: src/Data, src/Tools, tools/, data/mapgen|maps|theaters — findings
Scope read: all of src/Data rules loaders, RulesHash, MapFile, MapValidator, MapGenerator and MapGen core (Zones, Rot2, LayoutVariant, GenGrid, OceanEdgePass, LayoutChecks first 300 lines), SkirmishStart, src/Tools (AiCommands except ~560-700, MapCommands, Cli, MatchSetup), GameLoader map resolution, tools/measure scripts, head of tools/maps/make_rustbelt_m2.py, s17_layout1_baseline.md, s16_layout2.md (120 lines). Ran the prebuilt Tools dll read-only (map gen/report/validate/ascii, ai timeline) and a ~240-map layout 2 + ~120-map layout 1 sweep (runs/). NOT read: MapAnalysis/MapReport/Skin/Decor/Shell, tools/art/audio/fonts/voice beyond a path grep, data/theaters, design 16/17 in full, old 2026-11-03 review, tests.

## Summary
1. SkirmishStart places the conyard and start units by a fixed scan order, so the second start's army is a translation of the first, not its symmetry image. Most likely cause of the open start-0/1 skews (Glass Coast layout 1 Expedition 55 v 5, Mountains Directorate/Left Behind). Not yet proven by a controlled experiment.
2. `ai timeline` never requests ocean/landing markers and only accepts Authority/Directorate (M12-12b still open).
3. Maps loaded by the game, saves, missions or replays never pass MapValidator; hand-made maps with bad starts load unchecked.
4. `ai match` start-share label is hard-coded to "start 0 (4,4) / start 1 (mirror)", wrong on biome maps; PowerShell summary scripts regex-depend on that text.
5. Layout 2 generation is bounded (40 attempts) and never failed or hung in the sweep, but it silently ships the best failing attempt (3 of 240 maps had a FAIL check) and averages 6.4 attempts, 11 maps used 30+ attempts, 1 hit 40.
6. Minor: stale comments, m7_income_agg.py crash, implicit `<superweapon>_blast` reference, test-only effects shipped in techs.toml, `rustbelt_m2` few_fields warnings.

## Findings
### DATA-TOOLS-01 · major · SIM · confidence medium
**What:** SkirmishStart scans cells in the same dx/dy order for every start, so on a mirror or half-turn map the second player's conyard position and start-unit layout are translations, not images, of the first player's.
**Where:** src/Data/SkirmishStart.cs:68-70 (conyard `cx = s.X - FootprintW/2`, dy-then-dx ring search), :93-94 (unit rings 1..8, dy then dx ascending), :115 (fallback pass to ring 16).
**Evidence:** Prebuilt dll, Mountains layout 1 (mirror_x, seed 3, Directorate): P0 conscripts at x 4..7, P1 at x 87..90 in the same left-to-right order (translation, not mirror). Glass Coast layout 1 Expedition seeds 1 and 2: P0 Command Ship at (9,9), P1's at (86,78) for starts (13,13)/(82,82), not the half-turn image; marine positions differ per side. docs/balance/s17_layout1_baseline.md reports 55 v 5 by start on Glass Coast and 15 v 33 on Mountains Directorate; s16_layout2.md reports Mountains layout 2 skew (30/16, 29/13) although layout 2 starts are exact images.
**Impact:** Systematic first-mover/geometry advantage for one start on symmetric maps, in exactly the places the owner has an unexplained skew. Any fix changes match outcomes and hashes.
**Fix idea:** Place units for start k by mapping the start-0 offsets through the map's symmetry (or sort candidate cells by an image-invariant key), and give the conyard the same treatment; then re-run the S17 matrices. Size M.

### DATA-TOOLS-02 · major · CODE · confidence high
**What:** `ai timeline` builds the map without an ocean request or landing markers and refuses non-Authority/Directorate factions, so Expedition cannot be traced (M12-12b).
**Where:** src/Tools/AiCommands.cs:372 (`MapGenerator.Generate(tc.Params(size, 2, (ulong)seed))`, no OceanEdges, no landing), :377 error text "must be authority or directorate"; compare the working path at :749-760 in PlayPair.
**Evidence:** PlayPair requests `OceanEdges.North | South` and passes `gen.Meta.Landing` for Expedition; Timeline has neither.
**Impact:** Cannot debug Expedition stalls (Ash Plains, Mountains) with the timeline tool.
**Fix idea:** Reuse the PlayPair map-building block and relax the faction check. Size S.

### DATA-TOOLS-03 · major · CODE · confidence medium
**What:** MapValidator only runs from `map validate`, the generator and the editor; every game path that calls MapFile.Load skips it.
**Where:** src/Game/Saves/GameLoader.cs:81, SkirmishGame.cs:163/271, MapPicker.cs:91 call Load only; validator callers are Tools/MapCommands.cs:162, MapGenerator.cs:268, editor ValidationReport. MapFile.LoadWithMeta checks sizes, starts-on-map, layer lengths, land and overlay range, but not height/density/template values, duplicate starts, or max_players vs start count.
**Evidence:** grep of `MapValidator.Validate` callers; reading LoadWithMeta.
**Impact:** A hand-made or edited map with an unreachable start or duplicate starts loads and plays; failure shows up as an AI stall, not a message naming the map.
**Fix idea:** Run Validate (errors only) at load for non-curated maps and show the first error. Size S.

### DATA-TOOLS-04 · minor · CODE · confidence high
**What:** The start-share line is hard-coded as "start 0 (4,4) N, start 1 (mirror) M", wrong for biome maps where starts are not at (4,4).
**Where:** src/Tools/AiCommands.cs:118; consumers tools/measure/run_s15*.ps1, run_s16*.ps1, run_s17*.ps1 regex on that text ("Larger start share" column).
**Impact:** Misleading label; fixing it silently breaks the PowerShell summaries unless the regex is changed together.
**Fix idea:** Print real start coordinates, update the three regexes in the same change. Size S.

### DATA-TOOLS-05 · minor · CODE · confidence high
**What:** m7_income_agg.py assumes an `econ:` line follows each income line; `--income` without an Expedition side crashes.
**Where:** tools/measure/m7_income_agg.py:24 (`pat_eco.search(lines[i + 1])` returns None, then `e.groups()` raises AttributeError).
**Fix idea:** Skip when `e` is None. Size S.

### DATA-TOOLS-06 · minor · CODE · confidence medium
**What:** Layout 2 generation retries up to 40 times and, when all fail, silently returns the best attempt; failures travel through a `[ThreadStatic]` channel.
**Where:** src/Data/MapGenerator.cs (GenerateZoned, `MaxZonedAttempts = 40`, `FailedChecks`).
**Evidence:** Sweep (runs/l2_report.txt, 240 reports): mean 6.4 attempts, 11 maps at 30+, 1 at 40; FAIL checks remained on glass_coast medium 3p seed 10 (obstacles 23.3 %, band 10..19), glass_coast medium 4p seed 10 (same), mountains small 3p seed 7 (enemy path / side 0.62, band 0.8..1.2). No hang or crash; all rc=0.
**Impact:** A few seeds ship a map that the generator's own checks call unfair, with no warning in the game; up to ~5.8 s generation on the worst seeds.
**Fix idea:** Surface FailedChecks in the setup screen or re-roll the seed upward after 40. Size S.

### DATA-TOOLS-07 · minor · DATA-REF · confidence medium
**What:** Superweapon blast warhead is found by naming convention (`<superweapon>_blast`, then `dir_meltdown_blast`, then a flat 100 % table) and the loader never checks it, so a typo silently changes damage.
**Where:** src/Sim/Simulation.Superweapon.cs:226-232.
**Fix idea:** Validate the name in RulesReferences or add an explicit key. Size S.

### DATA-TOOLS-08 · minor · DATA-REF · confidence medium
**What:** Test-only effects `test_armor_up` and `test_unlock_turret` ship in the real rules file with no upgrade using them.
**Where:** data/rules/techs.toml:7, :14.
**Fix idea:** Move them into test fixtures, or confirm tests need them in the shipped file. Size S.

### DATA-TOOLS-09 · minor · CODE · confidence medium
**What:** Faction CLI flags use `Enum.TryParse` without `Enum.IsDefined`, so numeric strings are accepted; seed, tick and unit flags use culture-sensitive `int.TryParse`/`ulong.TryParse` (digits only, so low impact); GameLoader.cs:122 likewise.
**Where:** src/Tools/Cli.cs, src/Tools/AiCommands.cs:139, :377; src/Game/Saves/GameLoader.cs:122.
**Fix idea:** Pass `CultureInfo.InvariantCulture`, check IsDefined. Size S.

### DATA-TOOLS-10 · minor · TEXT · confidence high
**What:** Stale comments/messages: MapFile.Load doc says "(format 1 to 3)" while FormatVersion is 4; AiCommands.cs:753 says "Layout 2 has no ocean edges" while the next lines request sea (v5); Timeline error text only names two factions.
**Where:** src/Data/MapFile.cs (Load doc), src/Tools/AiCommands.cs:753, :139, :377.

### DATA-TOOLS-11 · minor · DATA-REF · confidence medium
**What:** The curated default map `rustbelt_m2` validates with 0 errors but emits `few_fields` warnings for both starts (fewer than 2 fields within 14 cells).
**Where:** data/maps/rustbelt_m2.map.toml; src/Data/MapValidator.cs:80; generator tools/maps/make_rustbelt_m2.py.
**Impact:** The default map is the one the validator itself rates as thin on economy. Check if intended (docs/balance).

### DATA-TOOLS-12 · idea · CODE · confidence low
**What:** Rules `base` inheritance loads the base as a real entry (no abstract flag); no shipped rule uses `base`, so it is dormant. 3-player layout 1 maps are validated as 4-start maps; `LayoutChecks` uses doubles (`n/96.0`) then int casts (IEEE-safe, low risk); `List.Contains` O(n^2) in Band/Largest.
**Where:** src/Data/RulesInheritance.cs; src/Data/MapGenerator.cs (3p `empty = (int)(p.Seed % 4)`); src/Data/MapGen/LayoutChecks.cs.

## Open questions
- Is the translation-not-image unit placement (01) deliberate? Please run a controlled experiment (same seed, swap which faction sits in start 0 vs 1, with SkirmishStart symmetrised) before sizing the fix.
- Does the owner want game-time validation (03) to hard-fail or warn?
- Are the 3 layout 2 failing seeds (06) acceptable, since layout 2 is off by default?
- tools/art scripts use /home/claude and /tmp/claude-0 default paths; fine for the cloud box, not run on the owner's Windows machine; confirm none are expected there.

## Things checked and found sound
- Duplicate-id detection across rule files; inheritance cannot cycle (one level, same file).
- Unknown-key detection (CheckKeys) on own keys; reference checks for weapons, warheads, prerequisites, effect targets, upgrades, tier provides, tender types, start kits (lazy but resolves for shipped data).
- RulesHash covers every public property of Vehicle/Infantry/Building/Weapon/Warhead/Upgrade by name; tier-sugared prerequisites included.
- Generator retries bounded (10 / 40); no hang or crash across ~360 generated maps; builder uses integer maths; symmetry by construction through GenGrid.
- All five curated maps validate with 0 errors; PlayPair start-win attribution has no off-by-one.
- Old map format versions 1-3 still load.
