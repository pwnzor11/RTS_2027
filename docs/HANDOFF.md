# Handoff: Session 8 — art push + reworks, owner LLM play-test, M4-16 diagnosis, M6 contracts
Last commit: 679c511 on main (PR #12) + this handoff commit (2026-11-04)
Where things stand: M5 is playable end to end: any AI slot can have an LLM commander (Anthropic or OpenAI-compatible) that sets
its strategy every 30 s through a recorded directive, talks in character in a chat panel, survives save/load and replays exactly
with no network. Only M5-10's owner play-test with a real key is open. M4-16 round 2 measured: tier ladder flat or inverted, target
not met (docs/balance/m4_16_round2.md). Session-5 art is still NOT on GitHub. Full suite 2 096 tests (AI 102, Game 773, Llm 107,
Scripting 43, Sim 1 071), green on Linux and Windows CI. Decisions D-108..D-112. Save format 9 (older saves no longer load).

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Art: once `integ-art` (or the bundle) is on GitHub, merge it into main through a PR (CI checks sprites), then ART-S6 reworks
   (Seismograph drum, Searchlight lens, Assembly Bore roof, Hatch cage, auth_gate open state) and the PAL_DIR radiation-green ramp.
   Pilot one rework as a contact sheet first.
2. Owner LLM play-test (USER_CHECKS session 7): confirm D-110 (chat not in replays), D-111 (Cmdr toggle, Enter, /to N), D-112
   (persona voices). Fix whatever the play-test finds (prompt wording, cadence feel, chat panel size).
3. M4-16 diagnosis: why higher Authority tiers lose (timeline traces of lost 3 v 2 seeds), then one change at a time: tech hold on,
   Q-43 tier costs, placement symmetry + seeded variation; re-run the 4-player matrix. Target >= 60 % per ladder cell.
4. M6 Left Behind contracts (D-102 answers in place; design docs/design/06_left_behind.md).

## Half-finished work
- M4-16: ladder not met; round-2 numbers in docs/balance/m4_16_round2.md; tech hold coded but off; placement symmetry patch parked
  (f796142, reverted in 55a3a27).
- M5: a loaded game's commanders restart their history and call count; the summary still cannot report enemy superweapon charge.
- Commander chat is game-layer only (no replay chat block) pending D-110.

## Waiting on the user
- Art push: `rts_art.bundle` + the session-5 `push_art.ps1` into the RTS_2027 folder, then run it (owner said: after this session).
- Play-test the LLM commander with a real key (Settings > "AI commander (LLM)...").
- Q-41 old lore DRAFT banners; Q-42 foam; Q-43 tier costs. D-105 balance numbers still "assumed — confirm".
- Delete old remote branches: `wp/m2-*`, `wp/m3-*`, `wp/m4-*`, `integ-m2*`, `integ-m3`, `integ-m4`, `integ-s6`, `wp/m3-21`,
  `wp/m4-16*`, `wp/m5-03`, `wp/m5-04`, `wp/m5-08`, `wp/neutral`, `wp/lore`, and this session's `wp/m5-02`, `wp/m5-05`, `wp/m5-06`,
  `wp/m5-07`, `wp/m5-09`, `integ-s7` (keep `wp/art-*`, `restore/*`).
- Scheduled tasks: several one-shot "placeholder"/"check"/"noop"/"unused" reminders from helpers in this and earlier sessions are
  listed (all fired and disabled); the owner can delete them in the scheduled-tasks list.

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
- Session 7 lessons:
  - Every helper test command gets a hang guard: `flock /home/claude/wt/dotnet.lock timeout 580 dotnet test ... --blame-hang-timeout 5m`.
    One deadlocked test (an awaited hand-made Task) held the shared lock and stalled the whole wave; kill only that testhost by pid.
  - Helpers that queue on the lock hand back early or reach for scheduling tools (three created "placeholder" reminders even
    though the brief forbade it). Brief them: run builds as FOREGROUND calls with a 600 000 ms tool timeout, rerun if still queued,
    never hand back while queued, no monitors, no scheduling, no mcp__remote-devices__* tools.
  - Haiku managed well-bounded packages (personas, chat panel) but gave up on the setup/save package while queued; Sonnet finished it.
  - After cloning, run `git lfs install` BEFORE `git lfs pull`; check `git lfs ls-files` shows `*` (not `-`) or the scene/menu proof
    tests fail with DivideByZero.
  - A worktree for measurements: `git worktree add --detach /home/claude/wt/measure <branch>`, Release build of src/Tools; each
    16-game `ai match` cell takes 1–2.5 min, the whole M4-16 set about 15 min with two lanes.
  - New sim state parts move the golden hashes, SkirmishDefaultHashTests and CombatTests' final hash; re-pin those only, and only
    after checking that the part sub-hashes other than the new one are unchanged.

## If time runs short
Do item 1 (art onto main) and item 2 (play-test fixes); M4-16 and M6 can each fill a later overnight session.
