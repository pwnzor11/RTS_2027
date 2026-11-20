# Session 37: skirmish balance plan (un-parks D-269)

Owner, session 37: "un-park skirmish balance and finally fix it ... make a plan that tests various cost and effect changes on the units and
maps". Earlier rounds (s27-s32) tuned only AI keys (caps, ratios, thresholds); none touched unit costs or stats, and the three Left Behind
economy levers all failed. This round tunes the **rules** (cost, health, damage, build time, harvest) and the **maps**, with the cluster
doing the measuring.

## Target (proposed, D-279)
- Every cross-faction row (6 pairs x 4 biomes, layout 1, tier 3 v tier 3, size 96, 30 000 ticks) inside **40-60 %** of decided games over
  128 games (seeds 1-64 x 2 swaps). Layout 2 rows reported, not targeted.
- Mirror rows (start-bias): start-0 share inside 40-60 % on every biome/layout (map fixes, item 3).
- Campaign: all 45 mission-tiers still meet D-222 (tier 1 >= 10/12, tier 3 >= 8/12, tier 5 >= 3/12) after the package.
- Landing rule D-264 still applies to each step: a change moves its target rows toward 50 % and no other cross row moves 5 points or more away.

## Method
1. **Baseline** (job base37): 80 rows = 10 faction pairs (6 cross + 4 mirrors) x 4 biomes x 2 layouts, 128 games each, on main d3dd95d.
2. **Ledger** (job led37): what each faction actually builds and loses at tier 3 in every cross pair (rust, glass), so knobs target the units
   that decide games.
3. **Sensitivity screen**: one knob per variant (+/-10-15 % cost, health or damage of a deciding unit; harvest rate; refinery/harvester cost;
   build time), run on the rows the knob can touch (that faction's 3 cross pairs x 4 biomes, 64 games). Variants are data-only trees on
   the cluster (`mkvar.py`, same Tools.dll), so a variant costs no build. Output: win-% change per knob per row.
4. **Package solve**: pick the smallest set of knobs whose summed effects bring every row into 40-60 % (least squares on the screen,
   preferring cost changes over stat changes, and small steps).
5. **Decide** the package on all 48 cross rows at 128 games; iterate (step 3-5) at most three rounds.
6. **Campaign check**: 45 mission-tiers x 12 seeds on the package; retune a mission only if it leaves its band.
7. Land with the tables, every changed number marked `(tune) s37` in data and "assumed - confirm" in DECISIONS.

## Map fixes (item 3)
From the mirror rows of the baseline plus `ai mirror` symmetry checks on every biome x layout x player count: any cell outside 40-60 % gets
a layout/start fix (as D-277 did for Ash Plains), screened with a 4-faction mirror at 128 games per faction and then the cross rows on that map.

Results go in docs/balance/s37_balance.md.
