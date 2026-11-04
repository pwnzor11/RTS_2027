# Handoff: Session 6 — owner review, art push, M4-16 balance, M3-21 follow-ups, then M5 / M6
Last commit: 7696ca2 on main (M4); PR #10 (M4-12 AI, integ-m4 5b26fb6) + this handoff commit — see below (2026-11-04)
Where things stand: M3 is complete and on main (PR #7, #8). M4-01..M4-15 are on main (PR #9): procedural maps (4 biomes ×
S/M/L, 2–4 players), garrisons, three-tier tech + 13 upgrades, +14 Authority / +7 Directorate types, spectator, setup v2,
map editor, theaters, neutral ruins. M4-12 (AI for 2–4 players, tier climbing, garrisons, stall fixes) is in PR #10 with
this handoff, landed if Windows CI was green. Session-5 art (14 Directorate structures, 4 Authority structures, agitprop
turret, Sunrise, rad mortar rev 2) is on local `integ-art` only; it reaches GitHub when the owner runs push_art.ps1.
Every guessed value is marked ASSUMED (D-089, D-095). Decisions D-091..D-098.

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Owner: run push_art.ps1 (USER_CHECKS session 5, item 1), then say "art pushed"; lead merges `integ-art` into main
   through a PR (LFS objects are then on GitHub, so CI can check out sprites).
2. Owner review: the art notes below, Q-35..Q-39, the USER_CHECKS session 5 list, the [DRAFT] lore.
3. M4-16 integration + balance (ROADMAP): start-0 advantage on 2-player generated maps (2–11×, check map/sim first),
   higher tiers losing to lower ones, rerun the 4-player matrix on the real generator, Directorate veteran mirror
   grinding, garrison bonus, foam vs tunnels, 8× spectator performance, neutral faction (Q-37).
4. M3-21 follow-ups: turret facing, dive flag and Sunrise blast radius in the view, AI save/restore, gate open state +
   open-gate art, Sunrise ready state.
5. Then M5 LLM commander contracts (key only in local settings, never logged) or M6 Left Behind contracts after Q-39.

## Half-finished work
- M4-12 ladder target (≥60 % higher tier wins) and 4-player fairness are not met; see docs/balance/m4_ai.md.
- M3-14 faction/tier targets not met (most M3 games drew); M4-12 improved decisiveness (Authority games 0–19 % draws).
- Effects art (explosions etc.) not redone → M10.

## Waiting on the user
- push_art.ps1 (three files sent in session 5: push_art.ps1, rts_art.bundle, art_objects.tar → `_art_push` folder).
- Art notes from the lead review: Seismograph drum reads like a bag; Searchlight lens is blown out; Assembly Bore roof is
  flat; Hatch cage reads as a red box; auth_gate only has a closed state; rad mortar lost its green (Q-35).
- Q-34..Q-39; lore approvals.
- A stray one-shot scheduled task named "check" was created by a helper; it fired once and does nothing more. Delete it
  from scheduled tasks if it is still listed.
- Leftover branches the owner can delete on GitHub: `wp/m3-*`, `wp/m4-*`, `integ-m3`, `integ-m4` (once PR #10 is merged),
  `restore/*` when no longer needed.

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


## If time runs short
Do item 2 (land M3 so far) and M3-18 wiring, so the owner can play everything built in session 4.
