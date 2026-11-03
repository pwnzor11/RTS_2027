# Handoff: Session 3 — M1 review, then M2 planning
Last commit: (set by the handoff commit; see `git log -1`)
Where things stand: Session 2 built the M0-11 skeleton, the M0-12 art pilot (owner picked arm B at
64 px, D-039), the v2 tank (M1-ART-01) and all of M1 overnight (M1-01..M1-11) under D-040: map model and
files, rules schema v1, mission state machine, path service, building placement with the one-cell gap
rule, fog and ghosts, path-following movement with blocking and crushing, combat with projectiles and
splash, desync tooling (sub-hashes, `Tools match`, `Tools replay verify`), and a battle sandbox in the
game window. 394 tests; Windows CI also runs the sandbox headless and verifies replays. Every overnight
choice is logged as "assumed — confirm" in D-043..D-047.

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Ask the owner to try the sandbox on Windows (USER_CHECKS, Session 2) and report anything that looks
   wrong: sprite facing, fog, overlays, controls (WASD was removed, D-047).
2. Walk the owner through the "assumed — confirm" decisions D-043..D-047 as one table (what was chosen
   | what changing it would mean | recommendation) with "take all" first; log confirmations/reversals.
3. M2-01: find and fix the corner advantage on generated maps (headless match favours the (4,4) start).
4. Plan M2 (playable skirmish slice: economy loop harvester → refinery → credits, production queues
   with prerequisites/cost/power, sidebar UI and minimap, one faction's first real units, one map, a
   simple AI) the same way M1 was planned: contracts first, waves, owner questions up front.

## Half-finished work
- None. Leftovers for the owner to delete on GitHub: branch `integ` (merged into main) and branch
  `restore/pre-m1` (restore point before M1; delete once M1 is accepted).

## Waiting on the user
- Windows check of the sandbox window (the cloud workspace has no display; only the headless path is
  verified).
- Confirmation of D-043..D-047.

## How to run this session
- Lead: Opus for build sessions; Sonnet for a pure review/question session. Helpers: Haiku default;
  Sonnet for sim core, determinism, serialisation, movement, combat.
- Repo clone: `/home/claude/rts_2027`; push to `origin main` directly (no branch protection). Tag pushes
  are blocked by the workspace git proxy: use a `restore/<name>` branch as a restore point.
- .NET 10 SDK is not preinstalled and dot.net is blocked: `apt-get install -y dotnet-sdk-10.0` works.
  Blender: `pip install bpy --break-system-packages` works headless (Blender 5.2).
- Shallow clone: other remote branches need `git config --add remote.origin.fetch '+refs/heads/<b>:refs/remotes/origin/<b>'`.
- Parallel helpers: split ownership by file. `Simulation` is a partial class (Simulation.cs core,
  .Movement.cs, .Combat.cs, .Hashing.cs); mission handlers live in MoveMission.cs / CombatMissions.cs.
  Lead writes contracts (types, stubs, hooks) on main before each wave; helpers never edit them.
- Every new sim array must live in an ISimStatePart; HashCoverageTests fails otherwise. State-part order:
  map, units, infantry, buildings, paths, vision, combat, projectiles (append only).
- Tests that pin exact lists collide between helpers: pin the full list in one place only.
- Windows CI catches what Linux doesn't (line endings bit us once; `.gitattributes` now pins TOML to LF).
- A helper once scheduled a stray message by mistake; scheduling tools stay forbidden in COMMON.md.
- Don't: use EA names/assets; put floats in `src/Sim`; iterate dictionaries in the sim; change contracts
  without a DECISIONS entry.

## If time runs short
Do items 1–2 (owner review) and plan M2; M2-01 can be the first item of the M2 build session.
