# Session 37 results: campaign sweep, par times, LAN soak, determinism, balance baseline

All runs on the home cluster (laptop k3s-cluster folder `claude/rts/s37/`: build.sh, base.sh, sweep.sh, soak.sh, det.sh, mir.sh, var.sh,
mkvar.py, sum.py). Build: main `d3dd95d` (session 36 landed) unless a section says otherwise.

## Campaign sweep (job sw37)
45 mission-tiers x 12 seeds (seed = 1000 + k x 7919, k = 1..12), bot walk-throughs. **All 45 meet D-222** (tier 1 >= 10/12, tier 3 >= 8/12,
tier 5 >= 3/12). Nothing since session 21 has broken a mission.

## Suggested par times (Q-73, for the owner; nothing lands)
The bot plays a scripted, near-perfect walk, so its times are far below a person's (the owner's auth_01 best is 8:35 against the bot's 3.2 min).
The suggestion is the bot's tier-3 median win time x 2.5, rounded up to a whole minute. "Timed" means all 12 tier-3 wins came on the same tick, so
the mission most likely ends on a timer; a par time makes no sense there (medal = Completed / No losses only).

| Mission | t1 | t3 | t5 | Bot tier-3 median win | Timed? | Suggested par |
|---|---|---|---|---|---|---|
| auth_01 | 12/12 | 12/12 | 6/12 | 3.2 min | no | 8 min |
| auth_02 | 12/12 | 12/12 | 9/12 | 4.0 min | yes | (4:00 timer) |
| auth_03 | 12/12 | 12/12 | 12/12 | 3.3 min | no | 9 min |
| auth_04 | - | 12/12 | 6/12 | 3.6 min | no | 9 min |
| dir_01 | - | 12/12 | 6/12 | 1.9 min | no | 5 min |
| dir_02 | 12/12 | 12/12 | 7/12 | 6.6 min | no | 17 min |
| dir_03 | 12/12 | 12/12 | 9/12 | 7.7 min | yes | (7:42 timer) |
| dir_04 | 12/12 | 12/12 | 5/12 | 2.7 min | yes | (2:40 timer) |
| exp_01 | - | 12/12 | 12/12 | 4.3 min | no | 11 min |
| exp_02 | 12/12 | 12/12 | 12/12 | 6.4 min | no | 17 min |
| exp_03 | 12/12 | 12/12 | 9/12 | 5.9 min | no | 15 min |
| exp_04 | 12/12 | 12/12 | 11/12 | 10.8 min | no | 28 min |
| lb_01 | 12/12 | 12/12 | 6/12 | 2.5 min | no | 7 min |
| lb_02 | 12/12 | 12/12 | 7/12 | 5.7 min | no | 15 min |
| lb_03 | 12/12 | 12/12 | 12/12 | 4.1 min | yes | (4:03 timer) |
| lb_04 | 12/12 | 12/12 | 12/12 | 8.3 min | no | 21 min |

## LAN lockstep soak (job soak37)
`Tools lan soak`, 54 configurations x 2 seeds = 108 runs of 12 000 ticks: 2/3/4 players, AI tier 1/3/5, packet loss 0/5/15 % with jitter
40/120/250 ms on 30 ms latency, input delay 2 and 3, real UDP sockets on loopback for the loss-free second seed. **108 of 108 in sync**
(all peers agree, bundle replay matches), on both Pi and x86 nodes.

## Cross-architecture determinism (jobs det37a on the Pis, det37x on x86)
All 10 faction pairs (4 mirrors + 6 cross) x 4 biomes, layout 1, seeds 1-2 x 2 swaps, with `--record-dir`: 160 games, run once on arm64 and
once on amd64. **All 160 replay files and all 40 result tables are byte-identical** across the two architectures (sha256 compared).
