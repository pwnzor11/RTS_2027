# Handoff: Session 7 — art push + reworks, M5 sim wiring (M5-01/02/05), AI re-measure
Last commit: 55a3a27 on main (PR #11) + this handoff commit (2026-11-04)
Where things stand: M3 complete; M4-01..M4-15 done; M4-16 round 1 landed (start-bias sim fixes, AI round 1, rules garrison
counters, Neutral faction, 8× benchmark) but the AI tier ladder is not re-measured on the fixed starts yet. M3-21 done. M5 plan
approved (D-103) and M5-00/03/04/08 are on main: src/Llm (contracts, providers, reply parser, fog-honest summary), the LLM
settings page and DPAPI key store; nothing is wired into a game yet. Session-5 art is still NOT on GitHub (see Waiting on the user).
Full suite 1 997 tests, green on Linux and Windows CI. Decisions D-099..D-107.

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Art: once the owner has put `rts_art.bundle` + the session-5 `push_art.ps1` in the RTS_2027 folder and run it, check that
   `integ-art` is on GitHub, merge it into main through a PR (CI checks sprites), then ART-S6: reworks (Seismograph drum,
   Searchlight lens, Assembly Bore roof, Hatch cage, auth_gate open state) and the PAL_DIR radiation-green ramp for the rad mortar.
   Pilot one rework as a contact sheet before the rest.
2. M5-01 sim contracts (Opus): CommandKind.Directive = 23, directive state part + hash, PlayerView.Directive, pack/unpack of
   DirectiveDraft (src/Llm/Contracts.cs). Then in one wave: M5-02 DirectiveBias in the tiered AI, M5-05 commander loop (30 s,
   apply-at-tick, stale drop, fallback; wire LlmHooks.TestConnection), M5-06 personas from leaders.md, M5-07 chat panel,
   M5-09 setup/spectator. M5-10 replay/save tests last. Plan: docs/design/09_m5_plan.md.
3. M4-16 round 2 (overnight-friendly, long matches): re-measure the tier ladder, cross-faction, Directorate mirror and 4-player
   matrix with the new per-game seeds; then tune toward ≥ 60 % per tier; try mirror-symmetric placement + seeded placement
   variation together; measure the Q-43 tier-cost proposal.
4. Then M6 Left Behind contracts (D-102 answers in place).

## Half-finished work
- M4-16: tier ladder target not met; results so far in docs/balance/m4_16_ai.md; tech hold coded but off.
- Placement symmetry patch (reverted 55a3a27, kept in history as f796142) — needs seeded variation first.
- Summary can't report enemy superweapon charge (not in PlayerView); small view addition if wanted.

## Waiting on the user
- Art push: `rts_art.bundle` and the session-5 `push_art.ps1` (from the session-5 chat downloads); `art_objects.tar` is already in
  `RTS_2027\art\`. The `_art_push` folder holds the session-4 files — do not re-run it.
- Q-40 model names; Q-41 old lore DRAFT banners; Q-42 foam; Q-43 tier costs (OPEN_QUESTIONS).
- D-105 balance numbers: confirm after a play-test.
- Delete old remote branches on GitHub: `wp/m2-*`, `wp/m3-*`, `wp/m4-*`, `integ-m2*`, `integ-m3`, `integ-m4` (keep `wp/art-*`,
  `restore/*`). New session-6 branches can go too once main is confirmed: `wp/m3-21`, `wp/m4-16*`, `wp/m5-*`, `wp/neutral`,
  `wp/lore`, `integ-s6`.
- The stray "check" scheduled task from session 5, if still listed.

## How to run this session
- Lead: Opus for build sessions (the owner wants long unattended overnight builds); Sonnet for a pure review/question session. Helpers: Haiku default; Sonnet for sim core,
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
  `flock -o /home/claude/wt/blender.lock …` (scripts must not self-lock). Art helpers run out of turns while queued: brief
  them to commit after each asset, and be ready to finish contact sheets yourself. Owner approves a pilot before any
  fan-out; send contact sheets with SendUserFile (tall sheets get scaled — split them to review).
- Windows CRLF: raw string literals in tests keep CRLF on a Windows checkout — normalise before searching for "\n".
  To check, convert all tests/*.cs to CRLF locally and run the suite.
- Sim rules: every new field hashed or allow-listed with a reason in HashCoverageTests; state parts are append-only
  (… players, economy, production, then extension parts such as "mission"); setup calls throw after the first Step (tests
  use Debug/Place commands); golden: `RTS_UPDATE_GOLDEN=1 dotnet test tests/Sim.Tests --filter Golden`.
- Never copy EA code (D-050). AI never runs inside Step (D-051). Lua only via ISimExtension (D-071). No floats or
  dictionary iteration in src/Sim. Scheduling tools stay forbidden for helpers (one created a stray task this session).
- Session 5 lessons:
  - Always `flock -o` (D-096) and install the dotnet wrapper first: /usr/local/bin/dotnet sets DOTNET_CLI_USE_MSBUILD_SERVER=0,
    MSBUILDDISABLENODEREUSE=1, UseSharedCompilation=false, then execs /usr/bin/dotnet. Without it VBCSCompiler holds the lock.
  - After cloning or creating worktrees run `git lfs pull && git lfs checkout` (pointer PNGs break Game tests).
  - Check what the remote integration branches already hold before launching a package (M3-16 was built twice).
  - Base every wave-2 worktree on the integration branch AFTER wave 1 merged (M4-12 missed the generator).
  - `git diff origin/main..` not `main..` (the local main ref goes stale after fast-forward landings).
- Session 4 lessons:
  - .NET builds also share ONE lock: wrap every dotnet build/test in `flock /home/claude/wt/dotnet.lock …` (D-090). Full
    suite ≈ 8 min; with 8 helpers queued, expect 30–60 min waits. Merge in batches and run one full suite per batch.
  - Helpers hand back while still queued, or re-send the same report many times: verify with git log, then TaskStop them.
  - Never `pkill -f` with a pattern that also matches your own shell command (killed the lead twice).
  - Shared enums are a collision point (two helpers both took FxKind = 3). Pre-assign enum values in briefs.
  - Art branches cannot be pushed from the cloud (LFS, D-084): keep them as `wp/art-*`, and at wrap-up rebuild
    `_art_push` in the owner's folder (LFS objects + bundle + push_art.ps1) through the device bridge.
  - Address SendMessage notes to the right helper id (a sprite note went to the audio helper).
  - The owner likes to see art: send every finished contact sheet for approval with a 1-line read per asset.
- Session 6 lessons:
  - Helpers get cut off mid-task while queued on the dotnet lock and hand back uncommitted work. Brief them to commit after
    each step, and when one reports "not finished", either resume it once with numbered steps or finish it yourself.
  - A helper that touches AI managers must also add any new long-lived field to that manager's Snapshot/Restore (M3-21 save
    format); the AI save→load determinism test catches it only for the paths it plays.
  - Mirror-symmetric AI placement stalemates identical AIs (D-104). Any symmetry fix in AI code needs the Directorate mirror
    test (ADirectorateMirrorAtVeteranDoesNotFreeze) and the pinned default-skirmish hash run before merging.
  - `ai match` now seeds each game of a pair separately; numbers from before session 6 are not comparable.
  - Remote branch deletion and folder requests outside RTS_2027 are blocked by the session safety checks: ask the owner.
  - The owner's RTS_2027 folder can be linked through the desktop app (device bridge); read-only checks of it are fine.


## If time runs short
Do item 1 (art onto main) and M5-01, so the next session can fan out the rest of M5.
