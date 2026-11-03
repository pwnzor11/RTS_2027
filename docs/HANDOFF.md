# Handoff: Session 3 — M1 review, then M2 planning
Last commit: 8757d63 (2026-11-03) — M1.5 landed via PR #1 (Windows CI green); docs checkpoint sits on top
Where things stand: Session 3 confirmed D-043..D-047 (D-049), ran a five-lens adversarial review
(docs/reviews/2026-11-03_adversarial), and built M1.5 hardening in one wave of 7 helpers: relations/teams/neutral +
team vision + actor view, boundary guards + debug ops, replay/save v6 (streaming, embedded rules, setup header),
publish with data and notices + crash log + version stamp, test fixtures split + golden replay, AI host + fog-honest
PlayerView + difficulty.toml + `Tools ai match/matrix`, F1 debug console. 437 sim + 15 AI + 35 game tests. Windows and
Linux produce identical sandbox hashes. Decisions D-049..D-057 (D-057 "assumed — confirm"). M2 scope fixed in D-053.

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Owner: run USER_CHECKS "Session 3" (sandbox check from Session 2 is still open too), do the LFS step (item 7).
2. Confirm D-057 (one table, "take all" first); answer Q-33 (SPDX licence version) → add SPDX headers by script + a
   test that enforces them + COMMON.md rule.
3. Answer Q-27 (Authority M2 roster) and Q-28 (content budget); then plan M2 contract-first: economy loop, production
   with prerequisites/cost/power, SimHost fixed-step driver + screen stack (review C-2/C-3) before the sidebar, palette
   sprites (C-4), Recruit-tier AI on the new harness (tiers must beat lower tiers in `ai matrix`).
4. M2-01 corner advantage on generated maps (now measurable with `ai match` seed swaps).

## Half-finished work
- M1.5-08: SPDX headers (waits on Q-33); Git LFS conversion (owner, USER_CHECKS item 7).
- Leftover branches the owner can delete on GitHub: `integ`, `integ-m15` (merged), `restore/pre-m1`, `restore/pre-m1.5`.

## Waiting on the user
- Windows sandbox check (Session 2) and the Session 3 checks; LFS step; D-057 confirmation; Q-27, Q-28, Q-33.

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
- .NET SDK: `apt-get update` first, then `apt-get install -y dotnet-sdk-10.0` (the index is stale without it).
- Git LFS uploads are blocked by the workspace proxy (403 on GitHub's storage host); LFS work happens on the owner's PC.
- `gh pr create` fails (GraphQL blocked): open PRs with `gh api repos/pwnzor11/RTS_2027/pulls -f title=… -f head=… -f base=main`.
  A PR is the way to run Windows CI before landing; CI also runs `ai matrix` into the job summary.
- Helpers on this 2-CPU box: build with `-m:1`; 7 parallel helpers worked. Create worktrees under /home/claude/wt yourself.
- Setup calls (Spawn*/PlaceBuilding/AssignMission) throw after the first Step: tests use Debug/Place commands
  (tests/Sim.Tests/Boundary/LateSpawn.cs, tests/AI.Tests/TestWorld.cs). New sim fields must be hashed or allow-listed
  with a reason in HashCoverageTests. Golden: `RTS_UPDATE_GOLDEN=1 dotnet test --filter Golden` when a change is intended.
- Never copy EA code (D-050). AI never runs inside Step (D-051).
- Don't: use EA names/assets; put floats in `src/Sim`; iterate dictionaries in the sim; change contracts
  without a DECISIONS entry.

## If time runs short
Do items 1–2 (owner review) and plan M2; M2-01 can be the first item of the M2 build session.
