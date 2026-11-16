# Session 29 balance round (src-level AI levers, Pi cluster)

Same cells as sessions 27-28: `ai match` tiered:3 v tiered:3, size 96, layout 1, 30 000 ticks, `--income 1`, seeds 1-32 x 2 start swaps = 64 games
per row (screen). Baseline = main seeds 1-32 (the s27 VE cells; main 0f467f2c reproduces the s28 main games exactly: checked on two 16-game cells,
same W-L-D and average end tick). Landing rule (D-257): the target row moves toward 50 % on 128 games and no other row moves more than 3 points away
from 50 %. Variant branches `bal/s29-*` are measurement only. Cluster scripts: k3s-cluster folder `claude/rts/s29/` (`mkseries.py`, `screen*.sh`, `sum28.py`).

## New keys (landed in PR #95, all default off or equal to today's constants, so no game changed)
Per faction, in `[faction.<id>]` of data/ai/difficulty.toml: `army_before_tech`, `army_before_adapt`, `adapt_cash`, `adapt_max_production`,
`tier2_gate_tick`, `adapt_build` (helper s29-auth), `siege_commit_ratio`, `aa_needs_air` (helper s29-rust).

## Diagnosis
- **Authority** (s29-auth traces): the tier-3 profile never runs AdaptBuild (only tier 5 has the flag), hence the new `adapt_build` key. Earlier
  tech (army_before_tech 18) brings tier 2 from tick 4350-4900 to 3180; adapt variants push tier 2 back to 7500-11000 (production eats the money).
- **Directorate v Expedition rust draws** (s29-rust traces): (1) every Expedition wave is called off at about half health: RecruitAi forces
  ExpeditionTactics.RetreatPct = 40 for every Expedition tier (difficulty.toml cannot lower it), so 8 waves in a row walked home from a Directorate
  base with no army, which repaired between waves; (2) a third of the Expedition tier-2 vehicle bag is the Sparker (600, hits only aircraft); traced
  games held 6-15 of them (up to 9000 credits) against a Directorate with no aircraft, so the "3-4x army value" lead is about half real.
- **Expedition v Left Behind glass draws**: an endgame search failure. The Left Behind is down to a deployed Mother Rig that packs, moves and unpacks;
  the Expedition mop-up (MopupPlan, revisit 6000 ticks) does not find it. No key built.

## Screen (64 games per row; main in brackets)
| Variant | Change | Result |
|---|---|---|
| a1 | Authority army_before_tech 18 | Auth v Dir 19 % (38), Auth v LB 60 % (66), Auth v Exp 48 % (41), Exp v Auth glass 69 % (58) |
| a2 | army_before_tech 12 | Auth v Dir 22 %, Auth v LB 40 %, Exp v Auth glass 77 % |
| a3 | adapt_build 1, army_before_adapt 12, adapt_cash 1500, max production 3 | Auth v Dir 19 %, Auth v LB 45 %, Exp v Auth glass 85 % |
| a4 | a1 + a3 | Auth v Dir 9 %, Auth v LB 50 %, Exp v Auth glass 92 % |
| a5 | tier2_gate_tick 2400 + army_before_tech 18 | identical to a1 (the army reaches 18 after tick 2400) |
| a6 | adapt_build 1, army_before_adapt 0, adapt_cash 1200, max production 4 | Auth v Dir 10 %, Auth v LB 30 %, Exp v Auth glass 83 % |
| l1 | Left Behind adapt_build 1 | Auth v LB 61 % (66), Exp v LB glass 70 % (60), Dir v LB 46 % (49) |
| l2 | l1 + adapt_cash 1000, max production 3 | Auth v LB 65 %, Exp v LB glass 84 %, LB v Exp 62 % (56) |
| r4 / r5 / r6 | Expedition siege_commit_ratio 150 / 200 / 300 | Dir v Exp 69 / 72 / 73 % (73); glass rows up 2-13 points |
| e3 | Expedition aa_needs_air 1 | Dir v Exp 39 %, Auth v Exp 19 % (41), LB v Exp 27 % (56), Exp glass rows 75-81 % |
| e4 | e3 + siege_commit_ratio 300 | as e3, Dir v Exp 32 % |
| c1 | e3 + shipments value 900 (from 1100) | Dir v Exp 40 %, Auth v Exp 30 %, LB v Exp 32 %, glass 66-79 % |
| c2 | e3 + shipments 750 | Dir v Exp 40 %, Auth v Exp 33 %, LB v Exp 37 %, Exp v Auth 57 %, Exp v Dir 75 %, Exp v LB 53 % |
| c3 | e3 + shipments 600 | Auth v Exp 35 %, LB v Exp 56 %, Exp v Auth 45 %; Exp v Dir glass 72 % (48 games), Dir v Exp 34 % (32 games); the Exp v LB glass row was not finished |

Earlier miss: r1-r3 and e1-e2 had the key written into a comment line's position (outside `[faction.expedition]`) and failed to load; rebuilt as
r4-r6 and e3-e4 (lesson in HANDOFF).

## Decision
No balance change landed (D-261). Every Authority lever weakens the Authority on all rows (tier 2 earlier or more production = a smaller army at the
first fights), worst against the Directorate. The Left Behind adapt variants help Authority v Left Behind by at most 5 points and push Expedition v Left
Behind glass up 10. The siege key alone does not move Directorate v Expedition. `aa_needs_air` fixes a real AI waste (air-only units against an enemy
with no aircraft) and turns the Expedition from the weakest to the strongest faction: Directorate v Expedition overshoots to 39 %, and lower shipment
values pull the glass rows back but leave Expedition v Directorate glass at 72-75 % and the rust rows Expedition-favoured.

Next steps (HANDOFF): land `aa_needs_air` together with an Expedition strength pass aimed at the Directorate matchups (Glide Tank numbers or the
Directorate's anti-armour), measured on all nine rows; or a softer version (one Sparker per bag cycle instead of none). Make the Expedition 40 %
retreat floor a data key. A mop-up fix for relocating bases (Mother Rig) for the glass draws.
