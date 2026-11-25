# Session 45 balance: finding the Expedition drop (Q-89, Q-90)

All rows tier 3 v tier 3, size 96, layout 1, 30 000 ticks, `Tools ai match`, win % = wins / (wins + losses). Builds: B1 = integ-s45 at f643146
(main + the `damage_taken_permille` attribute, default-identical), probe = B1 with M-22 reverted. Variants are data patches
(k3s-cluster `claude/rts/s45/`, patches in `/work/in/s45/patch/`). **Paired:** every variant row is compared with B1 main (`c45`) on the same
seed chunks (seeds 257-320 for Left Behind rows, 513-576 for Authority rows); one seed set is one set of maps (s41), so unpaired rows are not used.

## Q-89: Scrap Plate is not the cause
| variant | what | LB v Exp (ash / glass / mtn / rust), diff v main | other rows |
|---|---|---|---|
| sp800 | Scrap Plate as -20 % damage taken (B-08 attribute) instead of Heavy armour | +5.9 / +9.1 / -3.1 / +4.5 | Left Behind much stronger v Authority and Directorate (-11 to -15 for them) |
| sphp | s43 version (+20 % health) | -5.6 / +4.0 / -8.4 / 0.0 | (sp850 and sphp's other rows ran on unpaired seeds) |
| pw0 | Patchwork back to s43 (no +6 cells, no heal x4) | -2.4 / +1.9 / -2.0 / +1.3 | Authority v LB within 4 |
| sr0 | Salvage Rights wrecks back to x1 | -3.2 / -1.1 / 0.0 / +1.0 | Authority v LB within 3 |
So softening Scrap Plate to a damage-taken percent (the Q-89 recommendation) makes the Left Behind stronger, not weaker; neither Patchwork nor
Salvage Rights explains the drop.

## The cause: M-11 dropped two Expedition units from the Repair Hover
Bisect at the s44 seeds (rust Authority v Expedition / Left Behind v Expedition, 128 games): before the Diver merge 85-40 / 57-59; Diver merge alone
85-40 / 57-59 (identical: not a cause); Left Behind upgrades merge 91-34 / 89-30. In that merge, M-11 made repair auras honour `aura_targets`; the
Repair Hover's list was written before the Canister Skiff (D-234) and the Supply Tender existed, so both stopped being repaired.
| variant | what | Authority v Exp diff | LB v Exp diff |
|---|---|---|---|
| m11 | no aura_targets on the Repair Hover and the Tinker (M-11 off) | -9.4 / -3.1 / +0.8 / -6.6 | -8.4 / -8.7 / -6.6 / -9.7 |
| skiff | Canister Skiff + Supply Tender added to the Repair Hover list | same as m11, game for game | same as m11 |
**Landed:** `skiff` (data fix, test requires every non-aircraft Expedition vehicle on the list; commit 8d4b009).

## Q-90: M-22 and focus fire
- M-22 reverted (probe build): all 12 Authority rows identical to main game for game: the rule never fires in these games. Not a cause.
- The AI keys merge (A-02 focus fire for every faction, supply_margin as data, A-03 repair) alone moves rust Authority v Expedition 91 -> 101 of
  125 (bisect commit 8c03e3f = the s44 bis5 result). Tier-3 focus fire off for every faction (ff0): Authority v Expedition -4.4 / +0.5 / +1.9 / -4.0,
  Authority v Directorate +2.4 / +1.6 / +11.3 / +1.6, LB v Expedition +1.7 / -3.6 / +1.3 / +4.2: mixed and small. Not changed (owner call, Q-93).

## Still to measure (running at hand-off)
- **r45:** the 512-game D-305 table (24 rows, seeds 1-256) on 8d4b009, `/work/out/r45`; **sw45:** the D-222 campaign sweep on the same build,
  `/work/out/sw45`. Estimate from s44's table plus the paired skiff effect: pooled Authority v Expedition ~64 (still out), Left Behind v Expedition ~58,
  rust Authority v Expedition ~76; Authority v Directorate unchanged (33.6, out).
