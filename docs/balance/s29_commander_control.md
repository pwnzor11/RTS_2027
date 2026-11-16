# S29 · M5-13 random-directive control (no model, no key)

Run on the Pi k3s cluster, 2026-11-16, at main 0f467f2c. Setup as in s12 Test 1 (docs/balance/s12_commander.md): tier 3 v tier 3
(`tiered:veteran`), default factions, map 64, 30 000-tick limit. Seeds 1-64, both starts (128 games per arm).

- **Control:** `ai commander --random-directives --a tiered:3 --b tiered:3 --seed S --swap W --size 64 --ticks 30000` (side A gets seeded random
  directives and orders on the commander's call schedule; side B is the plain AI).
- **Baseline:** `ai match --a tiered:3 --b tiered:3 --seeds 8 --first 1..57 --size 64 --ticks 30000` (8 chunks, same seeds, starts swapped).
- Cluster scripts: k3s-cluster folder `claude/rts/s29/cmd.sh`, `cmdbase.sh`; raw results in the owner's `scratch/s29/` (git-ignored).

| Arm | Side A W-L-D | A share of decisive games | By start |
|---|---|---|---|
| Baseline (plain v plain) | 58-51-19 | 53 % | start 0 won 64 of 109 decisive games |
| Random directives (A) v plain (B) | 25-94-9 | 21 % | swap 0: 13-45-6; swap 1: 12-49-3 |

**Reading:** random directives and orders cost side A about 32 points against the plain AI. The levers are strong (they can lose a game on their
own), so a model that only matches the plain AI is already far better than random: the s12 Test 1 model run (gpt-5.4-mini, old M5-11 levers,
32 games) was 16-14-2 (53 %). M5-13 still needs the model run with the stronger M5-12 levers (D-241) on the same 128-game setup, which needs the
owner's key on the laptop (never in chat). A model result is then read against both rows: above 53 % = the model adds strength; between 21 % and
53 % = it uses the levers better than chance but worse than leaving them alone.
