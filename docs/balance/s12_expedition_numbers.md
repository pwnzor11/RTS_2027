# S12 Expedition rules-number variants (M7-18, measure only)

Goal: Expedition wins 40-60 % of decisive games at tier N vs each faction at tier N. D-152: rules numbers change only with owner approval, so nothing here is committed to `data/rules`; each variant was an uncommitted one-number edit, reverted with `git checkout -- data/rules` after its runs.

## Method
- `Tools ai match --a tiered:N --b tiered:N --faction-a expedition --faction-b <f> --size 96 --ticks T --seeds S` (S seeds x 2 start swaps). Release build of the branch start, `ExpeditionPlan` unchanged.
- W-L-D = Expedition wins-losses-draws. Decisive % = W / (W + L). Noise: n=16 about +-12 points, n=32 about +-9 on the win %; decisive % on 5-30 decisive games is wider still.
- Raw logs in `scratch/` (ignored): `base24/`, `screen_base/`, `v*/`, `full_*/`, summary `results.txt`.

## 1. Baseline at 24000 ticks (20 min), 16 seeds (32 games)
| Cell | W-L-D | Decisive % |
|---|---|---|
| t3 vs Directorate | 9-12-11 | 43 % |
| t3 vs Authority | 14-13-5 | 52 % |
| t4 vs Directorate | 12-7-13 | 63 % |
| t5 vs Authority | 11-9-12 | 55 % |
| t5 vs Directorate | 16-4-12 | 80 % |

Reading: the t3 vs Directorate failure at 18000 ticks (0-8-24) is largely a timing effect. Given 20 minutes the Expedition's economy catches up and t3 sits at 43 %. At 24000 only t5 vs Directorate is outside 40-60 % (high, 80 %).

## 2. Variant screen: t3 vs Directorate, 18000 ticks, 8 seeds (16 games)
| Variant | File / key | Old -> new | W-L-D | Decisive % |
|---|---|---|---|---|
| (base) | none | none | 0-4-12 | 0 % |
| V1 | infantry.toml `[exp_marine]` cost | 150 -> 110 | 3-2-11 | 60 % |
| V2 | vehicles.toml `[exp_glide_tank]` cost | 900 -> 700 | 2-3-11 | 40 % |
| V3 | vehicles.toml `[exp_skimmer]` harvest_capacity | 5 -> 8 | 2-4-10 | 33 % |
| V4 | game.toml `[shipments]` value | 2000 -> 2600 | 1-3-12 | 25 % |

Only 4-6 decisive games each, so this is a rough ordering. V1 and V2 went on.

## 3. Full runs, 18000 ticks, 16 seeds (32 games)
| Variant | Cell | W-L-D | Decisive % |
|---|---|---|---|
| V1 marine 150 -> 110 | t3 vs Directorate | 4-5-23 | 44 % |
| | t3 vs Authority | 13-13-6 | 50 % |
| | t1 vs Authority | 17-8-7 | 68 % |
| | t5 vs Authority | 4-10-18 | 29 % |
| V2 glide tank 900 -> 700 | t3 vs Directorate | 8-6-18 | 57 % |
| | t3 vs Authority | 26-4-2 | 87 % |
| | t1 vs Authority | 26-4-2 | 87 % |
| | t5 vs Authority | 16-8-8 | 67 % |
| Reference, no change (same method) | t3 vs Directorate | 0-8-24 | 0 % |
| | t3 vs Authority | 11-12-9 | 48 % |
| | t1 vs Authority | 16-10-6 | 62 % |
| | t5 vs Authority | 3-9-20 | 25 % |

Reference rows are from `s12_expedition_ai.md` (not re-run here).

- V1 fixes t3 vs Directorate (0 % -> 44 %) and leaves t3 and t1 vs Authority in or near range (50 %, 68 %). t5 vs Authority stays low (29 %, was 25 %, within noise), so it does not make that cell worse but does not fix it.
- V2 fixes t3 vs Directorate (57 %) and lifts t5 vs Authority (67 %), but overshoots hard at t1 and t3 vs Authority (87 %), so it breaks cells that were fine. A smaller cut (for example 800) is untested.
- Draws stay high (18-23 of 32) at 18000 ticks in the Directorate cell with either variant.

## Recommendation (owner approval needed, D-152)
- `data/rules/infantry.toml` `[exp_marine]` `cost`: 150 -> 110 (V1). Best measured; no overshoot.
- Not recommended: `exp_glide_tank` cost 900 -> 700 (overshoots vs Authority at t1 and t3). `exp_skimmer` harvest_capacity and `[shipments]` value showed no clear gain at screen.
- Open: t5 vs Authority (25-29 % at 18000 ticks, 55 % at 24000) is a time-limit effect more than a number problem. If the owner judges balance at 20 minutes, the five-cell baseline above is already near range apart from t5 vs Directorate (80 %), and V1 may not be needed at all.

## Limits
- Variant effects at 8-16 seeds are within noise of each other (V1 vs V2 vs V3 in the screen). V1 was not re-run at 24000 ticks.
- Only single-number changes; no combos (e.g. marine 120 with glide 800) were measured.
- Left Behind cells and tiers 2 and 4 were not run for the variants.
