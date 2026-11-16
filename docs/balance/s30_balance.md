# Session 30 balance round (Expedition strength pass, Pi cluster)

Same cells as sessions 27-29: `ai match` tiered:3 v tiered:3, size 96, layout 1, 30 000 ticks, `--income 1`, seeds 1-32 x 2 start swaps = 64 games
per row (screen). Baseline = main (s27 VE cells, reproduced by main since s28). Landing rule D-257: the target row moves toward 50 % on 128 games
and no other cross-faction row moves more than 3 points away from 50 %. Variant branches `bal/s30-*` are measurement only. Cluster scripts: laptop
k3s-cluster folder `claude/rts/s30/` (build*.sh, screen1-3.sh from s29's mkseries.py, sum28.py).

## New keys (PR #97, all unset, so no game changed)
In `[faction.<id>]` of data/ai/difficulty.toml (documented in its comment block):
- `aa_cap_before_air` 0..100: until an enemy aircraft is seen, at most this many anti-air-only vehicles alive or queued (softer `aa_needs_air`).
- `retreat_floor_pct` 0..100: the faction's waves go home below this health percent; missing = built-in (40 for the Expedition, none for the others).
- `mopup_hunt` 0/1 (helper s30-mopup): the mop-up hunts a relocating base (last-seen cell of a packed Mother Rig, former structure blocks first,
  3000-tick revisit, 3-unit sweep parties; deployable vehicles leave the "beaten" enemy value). Saved behind AI layout flag 0x200 only once used.

## Screens (64 games per row; main in brackets)
Rows: Auth v Exp rust 41, Dir v Exp rust 73, LB v Exp rust 56, Exp v Auth glass 58, Exp v Dir glass 52, Exp v LB glass 60, Auth v LB 66, Dir v LB 49.

| Variant | Change | Result |
|---|---|---|
| s1 | Sparker cap 1 (first soft mode) | as the full `aa_needs_air`: Auth v Exp 24, Dir v Exp 35, LB v Exp 23, glass 66-77 |
| s2 / s3 | s1 or aa 1, + retreat floor 25 | Expedition stronger still (glass 69-83) |
| f1 | retreat floor 25 alone | Auth v Exp 53, Exp v LB 52, but Exp v Dir glass 65, Dir v Exp 78 |
| g1 / g2 / g3 | aa 1 + Glide Tank 1050 cr / 320 hp; cap 1 + 1000 cr | no real change from aa alone (Glide Tank is not the lever) |
| l3 | LB tier-3 army_cap 26 | Auth v LB 58, Dir v LB 38, Exp v LB 69 |
| l4 | LB tier-3 cap until 2700 | Auth v LB 60, Dir v LB 30, LB v Exp 64 |
| k3 / k5 / k8 | Sparker cap 3 / 5 / 8 | k5: Dir v Exp 50, Auth v Exp 28, Exp v Dir 63; k8: Dir v Exp 68, Auth v Exp 35, Exp v Dir 56 |
| m1 | mopup_hunt Exp + LB | neutral within noise: Auth v Exp 45, Dir v Exp 77, Exp v LB 60 (draws 19 -> 16) |
| n6 / n8 / n10 | cap 6 / 8 / 10 + mopup_hunt | Dir v Exp 65 / 69 / 72, Auth v Exp 30 / 31 / 38, Exp v Auth glass 62 in all three |

## Findings
- The Sparker waste is what holds the Expedition back: with none (or even one) air-only unit before enemy air it gains 17-38 points on the rust
  rows and 8-18 on the glass rows. The cap is a smooth dial (cap 8-10 is close to main, cap 3 close to none).
- Every Expedition-wide strengthening that helps Directorate v Expedition costs Authority v Expedition (and Exp v Auth / Exp v Dir glass) more than
  3 points. Directorate v Expedition rust is a matchup problem (the Expedition cannot break the Directorate base; ~16-18 draws of 64), not a
  faction-strength problem; the next lever should act on the Directorate side or on the Expedition's siege against Directorate defences.
- The Left Behind side cannot fix Authority v Left Behind: both levers pull Directorate v Left Behind down 11-19 points.
- Glass draws (helper s30-mopup traces): the Mother Rig relocation explains only some; the rest are an Expedition army/economy collapse late on and
  a rig that repairs faster than the leftover Expedition units hurt it.

## Decision
No balance change lands (D-263); no variant met D-257 on the screen, so nothing went to 128 games. The keys stay in the code, unset.
