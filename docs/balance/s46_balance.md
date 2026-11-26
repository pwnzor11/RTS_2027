# Session 46 balance: Q-93 unit-number screens (in progress)

Owner (Q-93, s46): tune Authority v Expedition (r45 64.6 %) and Authority v Directorate (33.6 %) with unit numbers, screened against paired controls,
decided on 512 games. **Method:** data-only variants on build 8d4b009 (the r45 build; main's later commits touch only LAN, credits and docs). Every
variant row runs on chunks that r45 also played (8 seeds per chunk), so r45 itself is the paired control (`pair46.py`). Runner `/work/in/s46/var46.sh`
(copy of var45.sh), patches `/work/in/s46/patch/<tag>.py` via `setkey.py`; laptop copies in k3s-cluster `claude/rts/s46/`. Win % = wins / (wins + losses).

## v1: one key per variant, chunks 0-7 (seeds 1-64, 128 games per row), pooled over the four biomes, diff v r45 on the same chunks
| tag | change | Authority v Expedition | Directorate v Expedition | Authority v Directorate | Directorate v Left Behind |
|---|---|---|---|---|---|
| e1 | exp_glide_tank cost 960 -> 900 | -6.5 (rust 80.8 -> 73.4) | -4.2 (glass 29.6 -> 27.2) | | |
| e2 | exp_sparker max_health 220 -> 260 | +1.1 | -3.5 | | |
| e3 | exp_marine cost 150 -> 130 | -4.0 | -1.4 | | |
| e4 | exp_canister_gun damage 11 -> 13 | -5.6 | -6.2 | | |
| a1 | auth_rifle damage 8 -> 9 | -1.6 | | +3.2 | |
| a2 | auth_jeep max_health 150 -> 180 | +2.3 | | +2.4 | |
| d1 | dir_conscript cost 80 -> 90 | | | +3.8 (27 of 32 pieces) | -9.0 (18 of 32) |
| d2 | dir_delver cost 800 -> 880 | | | -18.1 | +17.1 |
d2 is an AI effect: a dearer Delver makes the Directorate much stronger (the AI buys other units instead). d1 rejected (Directorate v Left Behind).

## v2/v3: combinations, chunks 8-15 (seeds 65-128)
- c1 = e3 + a1 (Marine 130, Rifle 9), 24-30 of 32 pieces: Authority v Directorate +2.4, Authority v Expedition -3.4, Authority v Left Behind +3.6,
  Directorate v Expedition -0.8, Left Behind v Expedition -4.4. Right direction for both pairings, too small for either.
- c2 = Marine 130 + Rifle 10 (8-11 pieces, stopped): Authority v Directorate +19.6 but Authority v Left Behind +14.3: rifle damage is not targeted
  enough. v3 (c2, c3 = c2 + Glide 930) was deleted to free the cluster.

## v4 (running): chunks 16-23 (seeds 129-192)
- r1: Authority infantry (rifleman, rocketeer, marksman, engineer) `rad_resist_permille` 0 -> 400. Why: the Authority's weakest row against every
  faction is Ash Plains (v Directorate 16.5, v Left Behind 34.6, v Expedition 53.9) and its soldiers are the only ones with no ash resistance
  (Directorate 600, Left Behind 300).
- c4: r1 + c1 + Glide 900.
