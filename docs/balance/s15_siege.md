# S15 siege behaviour (Q-64, D-174)

Method: `Tools ai match --a tiered:A --b tiered:B --faction-a F --faction-b F --seeds 16 --size 96 --ticks 30000` (32 games per cell), Release build of the worktree,
tool copies per variant in a scratch dir. Traces with `ai timeline ... --trace N --dump 1`.

## Diagnosis (before any code)

Traced Directorate 5 v 1 (seeds 1 and 4, both drawn at 30 000 ticks).

1. **Seed 1 (the tier 1 side owns the field).** The tier 5 side loses its whole army by t12000 (army 1, then 0) and sits on shaft + vent + one bunk gallery with no cash.
   The tier 1 side has 90 units at home at t30000 and does not finish it. Cause: its single wave (14 roster members) is still in `Assault` since t9000. Five hurt
   conscripts (hp 10-20) stand at 88,90 attack-moving at the shaft (hp 927, repaired/barely scratched), the others are `Move`ing around home. A tier 1 profile has
   `respond_to_attacks = 0`, so none of the end-of-wave rules runs (`Stalled`, `Regroup`, `Unstick`); the roster never drops below a quarter of the launch size, so the
   wave never ends, and the 70 idle units wait at the rally cell for a "next wave" that cannot start. This is a wedge, not a siege problem.
2. **Seed 4 (tier 5 is ahead and slow).** Tier 5 reaches 30 units only at t19500 (income is thin: cash 0 for most of the game, one scraper). Before that it sends
   waves of 8-17 units against a base with pillbox, rad mortar (range 9, splash), a redoubt and 5 bunk galleries, and loses them (army 17 -> 7 at t12000-t18000).
   Waves 6-10 (t21000-t30000) are 20-35 units and do grind the base down (4 buildings left at t30000, no enemy army) but the game is over about 3 000 ticks too late.
   The loser keeps stray hatches (87 hp) that count as bases.
3. Common to both: waves are launched on size/time only, with no idea of how strong the defence is; targets are "nearest building", so the army walks past nothing
   and attacks whatever is closest (often a gallery inside mortar cover) instead of clearing outside-in; every unit attack-moves to the same cell, so long-range
   units (rocket truck range 8, howitzer 10, mortar 9) fight inside the range of the same defences instead of outside it (pillbox 3, turret 5); production structures are
   only preferred while `Ahead`.
