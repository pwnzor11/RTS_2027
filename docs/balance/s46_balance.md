# Session 46 balance: Q-93 unit-number screens and the D-305 gate

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

## v4: chunks 16-23 (seeds 129-192)
- r1: Authority infantry (rifleman, rocketeer, marksman, engineer) `rad_resist_permille` 0 -> 400. Why: the Authority's weakest row against every
  faction is Ash Plains (v Directorate 16.5, v Left Behind 34.6, v Expedition 53.9) and its soldiers are the only ones with no ash resistance
  (Directorate 600, Left Behind 300).
- c4: r1 + c1 + Glide 900.
- r1 (32 of 32): Authority v Directorate +1.3, Authority v Expedition +1.5, Authority v Left Behind -0.4: small alone.
- c4 (v4 chunks 16-23 plus the 512-game decide jobs k4a/k4b, 43-61 of 128 pieces when paused): Authority v Directorate +12.3, Authority v
  Expedition -4.9, Authority v Left Behind +5.4, Left Behind v Expedition -1.9, but **Directorate v Expedition -8.9** (glass -7.1): the cheaper
  Expedition units also beat the Directorate, so c4 alone would push Directorate v Expedition below 40 and Glass Coast's row below 25.

## v5: Directorate warheads v Heavy (Authority tanks), chunks 24-31 (paused at ~50 of 192)
- h1 dir_rocket v Heavy 100 -> 85: Authority v Directorate +4.6, Directorate v Left Behind -3.0 (25 / 16 pieces). h2 dir_slug v Heavy 85: noisy.

## v6/v7: c4 plus a Directorate buff v Light (Expedition and Left Behind armies are mostly Light), chunks 0-7
- c5 = c4 + dir_rocket v Light 75 -> 85 (stopped at 46 pieces): Directorate v Expedition +24, Directorate v Left Behind +19, Authority v Directorate
  -2: a cliff, far too strong. v7: c7 = c4 + dir_rocket v Light 78 (32 pieces): Authority v Directorate +8.6, Authority v Expedition -7.5, Directorate v Expedition
  +3.8, Directorate v Left Behind +2.0. c8 (80) stopped unread. c7 went to the 512-game decide.

## Decide: c7 on 512 games per row (D-305 gate, D-330)
c7 = Authority infantry ash resist 400, Rifle 9, Marine 130, Glide Tank 900, Directorate rockets v Light 78. All 24 rows, seeds 1-256 (chunks 0-31;
jobs v7, k7a, k7b, k7c), every chunk paired with r45 (build 8d4b009 + the data patch; the landed commit has the same rules data).
| row | r45 % | c7 W-L-D | c7 % | diff |
|---|---|---|---|---|
| ash plains authority v directorate | 16.5 | 104-306-102 | 25.4 | +8.9 |
| ash plains authority v expedition | 53.9 | 230-264-18 | 46.6 | -7.3 |
| ash plains authority v left behind | 34.6 | 178-255-79 | 41.1 | +6.5 |
| ash plains directorate v expedition | 55.9 | 212-152-148 | 58.2 | +2.3 |
| ash plains directorate v left behind | 52.7 | 245-182-85 | 57.4 | +4.7 |
| ash plains left behind v expedition | 43.6 | 216-286-10 | 43.0 | -0.6 |
| glass coast authority v directorate | 50.0 | 178-140-194 | 56.0 | +6.0 |
| glass coast authority v expedition | 60.9 | 270-222-20 | 54.9 | -6.0 |
| glass coast authority v left behind | 51.8 | 278-181-53 | 60.6 | +8.8 |
| glass coast directorate v expedition | 26.1 | 134-312-66 | 30.0 | +3.9 |
| glass coast directorate v left behind | 47.7 | 223-203-86 | 52.3 | +4.6 |
| glass coast left behind v expedition | 60.3 | 251-227-34 | 52.5 | -7.8 |
| mountains authority v directorate | 29.2 | 128-236-148 | 35.2 | +6.0 |
| mountains authority v expedition | 63.6 | 270-208-34 | 56.5 | -7.1 |
| mountains authority v left behind | 46.0 | 213-202-97 | 51.3 | +5.3 |
| mountains directorate v expedition | 46.4 | 170-191-151 | 47.1 | +0.7 |
| mountains directorate v left behind | 53.5 | 227-179-106 | 55.9 | +2.4 |
| mountains left behind v expedition | 59.5 | 268-213-31 | 55.7 | -3.8 |
| rust belt authority v directorate | 44.4 | 221-198-93 | 52.7 | +8.3 |
| rust belt authority v expedition | 80.0 | 334-158-20 | 67.9 | -12.1 |
| rust belt authority v left behind | 66.6 | 335-137-40 | 71.0 | +4.4 |
| rust belt directorate v expedition | 69.0 | 283-98-131 | 74.3 | +5.3 |
| rust belt directorate v left behind | 27.8 | 136-314-62 | 30.2 | +2.4 |
| rust belt left behind v expedition | 64.7 | 291-201-20 | 59.1 | -5.6 |

Pooled: authority v directorate 33.6 -> 41.8, authority v expedition 64.6 -> 56.4, authority v left behind 50.2 -> 56.4, directorate v expedition 48.5 -> 51.5, directorate v left behind 44.8 -> 48.6, left behind v expedition 56.9 -> 52.5.
**Gate D-305: met.** All six pairings inside 40-60 and all 24 rows inside 25-75 (edges: ash Authority v Directorate 25.4, rust Directorate v Expedition 74.3).

## Campaign sweep (D-222; 45 cases x 12 runs)
sw46b on 07e0299 (the c7 numbers): 44 of 45 meet their targets; dir_02 tier 5 fell to 2/12 (its tier-5 reserves are all Riflemen). Retune: tier 5
reserve 10 -> 8 and second reserve 5 -> 4 riflemen (9 alone gave 1/12): 6/12 on seeds 1-12 (13/24 on seeds 1-24), swd3 on b81fb96. With it all 45
meet the targets. Changed v sw45: auth_01 t5 9 -> 11, auth_02 t5 7 -> 6, auth_04 t5 4 -> 5, dir_01 t5 9 -> 6, dir_03 t5 9 -> 6, dir_04 t5 5 -> 6,
exp_03 t5 10 -> 12, exp_04 t3 10 -> 12, exp_04 t5 6 -> 12, lb_01 t5 6 -> 4 (dir_02 t5 6 -> 6 after its retune).

## Notes
- The cluster node huntsman went NotReady mid-run; five stuck pods were force-deleted and their pieces re-ran elsewhere.
- Tests moved by the numbers: three ashfall tests now use the Marine as the soldier with no ash resistance; WaveCommitTests' keys-off game re-pinned
  (3804 -> 3608 ticks); exp_03's tier-5 fixed-seed walk dropped under D-221 (sweep 12/12).
- The Field Manual PDF builds its stat tables from data: it still shows the old Marine and Glide Tank costs until the next booklet build.
