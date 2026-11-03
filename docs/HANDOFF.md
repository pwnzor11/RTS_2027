# Handoff: Session 4 — owner play-test, fixes, then M3 planning
Last commit: 3944725 (2026-11-03) — wrap-up content commit; only the handoff commits (docs) sit on top of it
Where things stand: Session 3 finished M1.5 (hardening from the adversarial review) and all of M2 (M2-01..M2-20). The game
has a main menu, a playable skirmish vs the Recruit AI on the Rust Belt map (economy, production, sidebar, minimap,
victory/defeat), Campaign Mission 1 "Survey" (MoonSharp Lua host), real Blender art for all 13 Authority types plus
pixel-art effects, and simultaneous-damage combat. 551 sim + 253 game + 28 AI + 35 scripting tests; Windows CI runs the
sandbox, skirmish and mission headless and verifies replays. Decisions D-049..D-072; only D-063 (M2-01 findings) is still
"assumed — confirm". Nothing has been seen on a real screen yet.

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Owner: report the Windows play-test (USER_CHECKS "Session 3 · M2 playable skirmish slice") and do the Git LFS step
   (USER_CHECKS Session 3 · M1.5, item 7) — art volume makes LFS urgent. Fix what the play-test finds first.
2. Confirm D-063 (corner-advantage causes; slot-order bias later fixed by D-065).
3. M3 planning, contract-first like M2. Owner questions up front: Q-29 Directorate tunnel spec (blocks the faction),
   Q-31 audio sourcing (D-059 wants voice + music), the Directorate M3 roster and harvesting (tunnel scrapers, D-053),
   whether M3 also adds tiers 2–5 of the AI (harness ready: `Tools ai matrix`).
4. Known follow-ups worth folding into M3 wave 1: remove Recruit's now-redundant scraper probes; explosion size by
   footprint; hitscan weapons raise no effect events; mission games record no replay (script spawns aren't in the setup
   log); MoonSharp has no instruction limit; wall art detail at 1×; Rocketeer east-facing flash cropped by its 32 px frame.

## Half-finished work
- None in code. Leftover branches on GitHub the owner can delete: all `integ-*` and `wp/*` branches (all merged).

## Waiting on the user
- Windows play-test; Git LFS conversion; D-063; Q-29, Q-31.

## How to run this session
- Lead: Opus for build sessions; Sonnet for a pure review/question session. Helpers: Haiku default; Sonnet for sim core,
  determinism, serialisation, movement, combat, AI, scripting, and all Blender art.
- Repo clone: `/home/claude/rts_2027`; main has no branch protection, but land through a PR so Windows CI runs first
  (`gh pr create` fails — use `gh api repos/pwnzor11/RTS_2027/pulls -f title=… -f head=… -f base=main`); GitHub marks the
  PR merged when main fast-forwards to its head. Tag pushes are blocked: use `restore/<name>` branches.
- Setup: `apt-get update && apt-get install -y dotnet-sdk-10.0`; Blender `pip install bpy --break-system-packages`
  (got 5.2.3 on Python 3.13). Fonts come from apt (fonts-noto-core, OFL).
- Shallow clone: add fetch refspecs for other branches, e.g.
  `git config --add remote.origin.fetch '+refs/heads/wp/*:refs/remotes/origin/wp/*'` (and `integ-*`), so pushed
  branches get upstream tracking (the stop hook checks it).
- Helpers: worktrees under /home/claude/wt (create them yourself, from the integration branch that holds the contracts);
  wave rules in docs/process/HELPER_WAVE_RULES.md (copy docs/process/HELPER_WAVE_RULES.md to /home/claude/wt/WAVE1.md at the start of a session); build with
  `-m:1` (2 CPUs). Lead writes contracts first; helpers own files by function; expect merge-time fixes (late spawns in
  new tests, pinned hashes, golden regeneration, part-order tests).
- Art: renders take 5–20 min and share ONE lock — wrap each render in exactly one
  `flock /home/claude/wt/blender.lock …` (scripts must not self-lock). Art helpers run out of turns while queued: brief
  them to commit after each asset, and be ready to finish contact sheets yourself. Owner approves a pilot before any
  fan-out; send contact sheets with SendUserFile (tall sheets get scaled — split them to review).
- Windows CRLF: raw string literals in tests keep CRLF on a Windows checkout — normalise before searching for "\n".
  To check, convert all tests/*.cs to CRLF locally and run the suite.
- Sim rules: every new field hashed or allow-listed with a reason in HashCoverageTests; state parts are append-only
  (… players, economy, production, then extension parts such as "mission"); setup calls throw after the first Step (tests
  use Debug/Place commands); golden: `RTS_UPDATE_GOLDEN=1 dotnet test tests/Sim.Tests --filter Golden`.
- Never copy EA code (D-050). AI never runs inside Step (D-051). Lua only via ISimExtension (D-071). No floats or
  dictionary iteration in src/Sim. Scheduling tools stay forbidden for helpers (one created a stray task this session).

## If time runs short
Do item 1 (play-test fixes) and item 3's owner questions; M3 building can start next session.
