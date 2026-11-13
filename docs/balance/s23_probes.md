# Session 23 balance probes (M13-19, after the s22 laptop series)

Source series: the owner's laptop `balance_runs\s22after\s22after_summary.md` (commit 3f55b423, 64 games per row). Probes below ran on the
2-CPU cloud box: `ai match` tier 3 v tier 3, `--seeds 8` (16 games per cell), size 96, layout 1, `--income 1`, 30 000 ticks. A 16-game cell
resolves about plus or minus 14 points, so these probes only pick a direction; the laptop re-run decides.

## What s22after showed (decisive games, target 45-55 % per pair)

Fixed: Auth v Exp Rust Belt 30-30 (was 12-51), Auth v LB 27-25, LB mirror Mountains L1 start share 53 % (was 74 %).
New outliers: Dir v LB Rust Belt 13-49, LB v Exp Rust Belt 48-9, Exp v LB Glass Coast 16-37; also Auth v Dir 34-19, Dir v Exp 39-18,
Exp v Dir Glass Coast 38-19.

## Probe results (A wins - B wins, 16 games, draws left out)

| Variant (on top of main ec3ddd9) | Dir v LB RB | Auth v Dir RB | LB v Exp RB | Exp v LB GC | Auth v Exp RB | Exp v Auth GC | Exp v Dir GC |
|---|---|---|---|---|---|---|---|
| base (s22 data) | | | 11-2 | | | | |
| D1: `dir_rocket` splash 1, `dir_rocket_salvo` damage 75, range 1536 | 1-14 | 5-7 | | | | | |
| D2: `dir_rocket` splash 1 | 5-8 | 6-7 | | | | | |
| E1: shipments value 1700 | | | 12-4 | 4-9 | 6-10 | | |
| E2: shipments floor_permille 450 | | | 11-3 | | | | |
| H: D2 + `lb_harpoon_gun` targets ground | 5-8 | 6-7 | 11-2 | 4-10 | | | |
| R: Refinery Rig 900 | | | 11-2 | | | | |
| S: shipments value 2000 | | | 9-5 | | | | |
| M: base with `--mopup off` | | | 11-2 | | | | |
| T: repair auras 10 | | | 10-5 | | | | |
| Old: all data/rules from 9d479f9e (s21) on today's code | | | 7-8 | | | | |
| F2: D2 + repair auras 10 | 7-6 | | 10-5 | 4-9 | 5-11 | 9-6 | 6-8 |
| F: F2 + shipments 1700 | 7-6 | | 11-5 | 6-8 | 2-13 | 10-4 | 5-6 |

Readings:
- The Rocket Truck splash is the Directorate lever: splash 1 alone (D2) brings Dir v LB and Auth v Dir near even. Cutting the truck's range to 6
  cells (D1) wrecks the Directorate (and would also drop the truck out of the AI's artillery class, `SiegePlan.ArtilleryCells` = 7).
- H, R and M play exactly the same games as base: in these rows the AI never uses the Harpoon's anti-air, the Rig price or the mop-up.
- LB v Exp is a data effect (s21 data on today's code is even), spread over shipments and repair auras; each lever alone moves it only a little,
  and both move the Expedition against every faction at once (F2 / F put Auth v Exp Rust Belt back at 5-11 / 2-13). No data lever found that fixes
  LB v Exp without undoing the Authority fix.
- Trace (seed 1, base): the Left Behind fields 80-90 cheap units against 17-28 Expedition units of similar total value, and wins the trade. The
  Expedition's siege targets were checked against a map dump: they are Left Behind base structures (Mother Rig, Yard, Spike Nest), not a bug.

## Applied (D-229)
`dir_rocket` splash 0 -> 1 (back to the s21 value). Shipments, repair auras and the Harpoon stay as D-223 set them.

## Open (M13-22)
Left Behind v Expedition (Rust Belt 48-9, Glass Coast 37-16 for LB): needs unit loss tables for the matchup (which Left Behind units kill which
Expedition units) before choosing a matchup-specific change.
