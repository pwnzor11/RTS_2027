# Session 28 balance round (Pi cluster measurements)

Same cells as session 27: `ai match` tiered:3 v tiered:3, size 96, layout 1, 30 000 ticks, `--income 1`, seeds 1-32 x 2 start swaps = 64 games
per row unless marked, run on the Pi k3s cluster (k3s-cluster-code `claude/rts/s28/`: `mkseries.py`, `series.sh`, `sum28.py`). Baseline = main
42d0d3f (D-254, the session 27 VE numbers). Target 45-55 % per cross-faction row. Variant branches `bal/s28-*` are measurement only.

Noise: a 64-game row resolves about +-10 points. Conscript 95 and conscript 100 gave Authority v Directorate 45 % and 38 %, so single 64-game
cells were used to screen, and the candidates were re-measured on 128 games (seeds 1-64), together with main.

## Baseline (main 42d0d3f)
| Row | W-L-D | A share |
|---|---|---|
| Authority v Directorate rust | 19-31-14 | 38 % |
| Authority v Left Behind rust | 39-20-5 | 66 % |
| Authority v Expedition rust | 25-36-3 | 41 % |
| Directorate v Left Behind rust | 29-30-5 | 49 % |
| Directorate v Expedition rust | 35-13-16 | 73 % |
| Left Behind v Expedition rust | 31-24-9 | 56 % |
| Expedition v Authority glass | 34-25-5 | 58 % |
| Expedition v Directorate glass | 24-22-18 | 52 % |
| Expedition v Left Behind glass | 27-18-19 | 60 % |

## Diagnosis
- **Directorate v Expedition rust.** Traces (seeds 3, 5, 9, 12): the Directorate sends waves of 14-16 conscripts from about tick 1400; the Expedition
  sends its first wave at 3300-4600 and lives on shipments (12-13k of its income by 10 min). Its harvest income on rust_belt is a third of the
  Directorate's. Many draws are games where the Expedition is far ahead on army value (20k v 5k) but cannot break the Directorate base.
  Per 1000 credits a conscript squad has 625 hp and 169 dps against light armour; a Sparker pair 367 hp and 80 dps against infantry.
  The Skimmer is not the lever any more: heavy armour (W1) left the row at 70 % and swung Left Behind v Expedition to 30 %.
- **Authority rows** (helper s28-auth, 14 traced games): the Authority runs on one Scraper until expansion_tick 4800 and reaches tier 2 at
  4100-9200 (tier gate: 30 army units and tick 3600, a src constant), holding 2-3.5k unspent credits at tick 7200. Against the Directorate it trades
  worse (Tanks lost 86 %, rocket trucks the likely cause). Against the Left Behind it wins early while the Left Behind tier-3 army cap leaves ~3000
  credits unspent. The three rows form a cycle (Authority > Left Behind ~ Directorate > Authority), so no single Authority-wide change fixes both.

## Round 1 (64 games, rows the change can touch)
| Variant | Change | Effect |
|---|---|---|
| X1 | canister gun damage 11 -> 13 | Dir v Exp 64 %, but every glass row up 5-18 (Exp v Dir glass 70 %, v LB 74 %); Auth v Exp 32 % |
| X2 | Expedition tier-3 harvester_target 2 | Dir v Exp 74 %, LB v Exp 65 %: no |
| X3 | Expedition tier-3 attack_ratio_pct 85 | identical games: inert for the Expedition |
| X4 | Directorate conscript cost 80 -> 95 | Auth v Dir 45 %, Dir v Exp 71 %, Dir v LB 46 %, Exp v Dir glass 56 % |
| X5 | Sparker cost 600 -> 550 | glass rows up to 65-69 %, Dir v Exp 74 %: no |
| X6 | Directorate tier-3 attack_ratio_pct 130 | Auth v Dir 42 %, Dir v Exp 71 %, Dir v LB 50 %, Exp v Dir glass 57 % |
| X7 | Expedition tier-3 attack_threshold 6 | Dir v Exp 79 %: no |

## Round 2
| Variant | Change | Effect |
|---|---|---|
| Y1 | conscript 100 | Auth v Dir 38 %, Dir v Exp 61 %, Dir v LB 47 %, Exp v Dir glass 55 % |
| Y2 | conscript 95 + canister 12 | glass rows 58/60/70 %, Dir v Exp 70 %: no |
| Y3 | canister 13 + shipments 1000 | Exp v Dir glass 73 %: no |
| Y4 | conscript 95 + Directorate tier-3 attack_ratio_pct 130 | Auth v Dir 48 %, Dir v Exp 69 %, Dir v LB 46 %, Exp v Dir glass 58 % |
| V3 | Authority tier-3 harvester_target 2 (helper) | Auth v LB 54 %, Auth v Dir 41 %, Auth v Exp 57 %, Exp v Auth glass 69 % |
| Z1 | conscript 95 + V3 | as V3 and X4 combined |
| W1 | Skimmer armour heavy | LB v Exp 30 %, Dir v Exp 70 %: no |
| W2 | Z1 + Skimmer armour heavy | stopped after 32 games of one row once W1 failed (not counted in the 15) |

Helper-only 32-game checks (not on the cluster): Authority Tank cost 650 (Auth v Dir 54 % but lifts the Authority everywhere), expansion_tick
2400, attack_threshold 14, rifleman 85/90: dropped.

## Final check (128 games per row, seeds 1-64)
Main was re-measured on seeds 33-64 (cluster job s28m) and merged with the session 27 VE cells (seeds 1-32). Rows a variant cannot touch
give the same games as main (checked: F1 and Y4 match main on every untouched row), so Expedition v Left Behind glass for main is the F1/Y4 cell.

| Row | Main (128) | Y4 conscript 95 + Dir ratio 130 | V3 Authority harvester_target 2 | F1 = Y4 + V3 |
|---|---|---|---|---|
| Authority v Directorate rust | 46-58-24 (44 %) | 45-67-16 (40 %) | 36-66-26 (35 %) | 37-69-22 (35 %) |
| Authority v Left Behind rust | 83-35-10 (70 %) | same as main | 71-46-11 (61 %) | 71-46-11 (61 %) |
| Authority v Expedition rust | 53-69-6 (43 %) | same as main | 51-57-4 (47 %, 112 games) | 59-65-4 (48 %) |
| Directorate v Left Behind rust | 52-60-16 (46 %) | 50-63-15 (44 %) | same as main | 50-63-15 (44 %) |
| Directorate v Expedition rust | 69-29-30 (70 %) | 62-38-28 (62 %) | same as main | 62-38-28 (62 %) |
| Left Behind v Expedition rust | 61-53-14 (54 %) | same as main | same as main | same as main |
| Expedition v Authority glass | 66-52-10 (56 %) | same as main | 75-47-6 (61 %) | 75-47-6 (61 %) |
| Expedition v Directorate glass | 54-47-27 (53 %) | 67-44-17 (60 %) | same as main | 67-44-17 (60 %) |
| Expedition v Left Behind glass | 67-33-28 (67 %) | same | same | same |
| Sum of distances from 50 % | 87 | 92 | 88 | 88 |

On 128 games main is closer than the 64-game baseline said: Authority v Directorate is 44 % (not 38 %), Directorate v Expedition 70 % (not 73 %),
and Expedition v Left Behind glass 67 % (not 60 %). Every variant only moves distance between rows: the conscript nerf takes 8 points off
Directorate v Expedition and gives them to Authority v Directorate and Expedition v Directorate glass; the second Authority Scraper takes 9 off
Authority v Left Behind and adds 9 to Authority v Directorate and 5 to Expedition v Authority glass.

## Decision
No balance change landed (D-255). None of the 15 variants beats main once measured on 128 games. Open outliers at 128 games: Authority v Left
Behind 70 %, Directorate v Expedition rust 70 %, Expedition v Left Behind glass 67 %. Next steps that the data knobs cannot reach (helper
s28-auth): a per-faction `adapt_build` (the Authority hoards 2-3.5k credits with one barracks and one factory) and a per-faction tier gate
(`ArmyBeforeTech` 30 is a src constant; the Authority reaches tier 2 at 4100-9200). Both are src changes in src/AI/Recruit. Any further
variant is judged on 128 games (seeds 1-64) per row against the 128-game main table above.
