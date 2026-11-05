# Handoff: Session 10 — supply on, M6-12 acceptance, VET-6, M7-02 naval contracts
Last commit: PR #19 (session 9 wave 3) + this handoff commit (2026-11-05); see `git log -1 origin/main`
Where things stand: the Left Behind is a playable third faction (M6-03..10: rigs, salvage, raiders, auras, the Second Drift storm,
data, game layer, AI). Generic veterancy (VET-1..5, save format 11) and the supply cap (SUP-1..3, SUP-5; held off by D-133) are in.
The M4-16 wave gate landed (D-131). M7 plan, supply and veterancy designs, M8 plan and Expedition lore/personas are written. The
Left Behind art (14 structures, packed rig, 16 units) is on local branch `art-s9`, staged in the owner's `_art_push` folder for him
to push (D-084). Full suite 2 331 tests (Sim 1 238, Game 824, AI 119, Llm 107, Scripting 43). Decisions D-119..D-133.

## Start here (shown to the user as the plan; nothing starts until they say go)
0. If the owner ran `_art_push\push_art_s9.ps1`: open a PR `art-s9` → main (merge main into it first if needed), Windows CI, merge.
   If not, remind him (it needs his PC; the cloud cannot upload LFS objects).
1. Supply on (D-129/D-133): with `[supply] enabled = true` three AI tests fail — M4AiTests.RegularFillsItsOwnGarrisonWithIdleInfantry
   (Authority: 2 of 3 occupants), VeteranClimbsToTierThreeAndBuildsTierTypes (Authority: "tier 3 within 15 minutes") and
   LeftBehindAiTests.ALeftBehindAiPlaysAFullGameWithoutAStuckRig (tier 5, seed 4). Find why (supply headroom 6 in
   BuildOrderManager.SupplyShort, ration hall needing auth_power, units refused while capped), fix the AI (not the tests), flip it on.
   Then SUP-4 (sidebar counter "used/cap", tooltip) and SUP-6 (before/after table).
2. M6-12 acceptance: soak every faction pair (3 factions), Left Behind golden replays, early-game income curves for the 3 factions
   (`Tools ai timeline`), and re-measure the Left Behind AI on integ with veterancy and the Second Drift fix (docs/balance/m6_10_ai.md
   was measured before both). Salvage rarely runs (Hauler gate 150 wreck value): tune.
3. VET-6 (AI uses veterancy: army value, retreat, focus fire) and the own-unit info panel (Q-46).
4. M7-02 naval contracts (docs/design/11_m7_plan.md), then M7 water locomotion and the Expedition skeleton.
5. M4-16 top rungs: still under 60 % above 2v1 (docs/balance/m4_16_round4.md); Directorate games draw 40-60 %.

## Half-finished work
- Supply: everything built, held off (D-133). SUP-4 has only the block text ("Supply cap reached: build a supply structure").
- M6-12: tables done (docs/balance/s9_tables.md); soak, golden replays and income curves not run.
- Left Behind AI: the Second Drift now can fire (entrance range 0) but the AI's use of it is untested in a match; Kites unused (Q-48).
- Art: turret manifests for lb_spike_nest and lb_harpoon_post got the [house] block by hand; a re-render through turret_build.py
  drops it unless the generator from main is used. Renders used 12 (vehicles) / 24 (infantry) samples; final quality wants 24+.
  The infantry render prints "touches the canvas edge" on every frame (unchecked; likely a false alarm).
- Expedition personas use the faction string "Expedition" (no enum value yet).

## Waiting on the user
- Run `powershell -ExecutionPolicy Bypass -File .\_art_push\push_art_s9.ps1` in the RTS_2027 folder, then tell Claude "art pushed".
- Q-43 (keep 2 500, recommended), Q-44 session-9 numbers, Q-45 Left Behind vehicle scale, Q-46..Q-50 (docs/OPEN_QUESTIONS.md).
- Confirm or reverse the "assumed — confirm" decisions D-123..D-133 (D-129 and D-131's gate are already the owner's).
- Play-test: USER_CHECKS "Session 9" (Left Behind, veterancy; supply by setting `enabled = true`). Still pending from session 7: the
  LLM commander with a real key (D-110..D-112).
- Delete old remote branches once happy: `land/s9-w2`, `land/s9-w3`, `wp/sup5`, `integ-m6`, `integ-s9` (after PR #19),
  `wp/m4-16r3-wip`, and the session-8 list. Keep `restore/pre-s9-wave1`, `restore/pre-s9-m6w2` until happy.
- Optional: clear old files in `_art_push` (the `*.old`, `*.done` and session-8 tars) after the session-9 push.

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

- Session 8 lessons:
  - `gh pr checks` uses GraphQL, which is blocked: poll `gh api repos/pwnzor11/RTS_2027/commits/<sha>/check-runs` instead (a watcher
    on `gh pr checks` silently waits forever).
  - GitHub's GH008 hook rejects a push that references any LFS object it lacks anywhere in history; `lfs.allowincompletepush` does not
    help. Rewrite the offending commits (filter-branch index-filter to existing blobs) and check objects with the LFS batch API
    ("download", real sizes, at most 100 objects per request).
  - Building sprite manifests need a [house] block (HouseRamp.Standard) and fx anchors inside the frame, or Game tests fail.
  - `ai match` cells need 64 games (32 seeds x 2 sides): a 16-game cell resolves only about +-12 points; a 32-seed cell takes 8-10 min.
  - Background runs must use run_in_background directly; a trailing `&` inside the command is killed.
  - `ai match --trace N --game G` prints per-side army, income, tier and wave lines for one game (M4-16 diagnosis).
  - Fetch refspec for integration branches: `+refs/heads/integ-*:refs/remotes/origin/integ-*`.
  - When checking on a helper, cd back to your own worktree afterwards: the stop hook checks the current folder and will flag the
    helper's uncommitted work.
  - CommanderReplayAndSaveTests can time out under heavy CPU load (passed 3/3 alone).

- Session 9 lessons:
  - Direct pushes to main are refused by the session safety check even as a fast-forward: land with
    `gh api -X PUT repos/pwnzor11/RTS_2027/pulls/<n>/merge -f merge_method=merge -f sha=<head>` after CI is green.
  - Pattern: integration branch `integ-s<N>`, snapshot it to `land/s<N>-w<k>` for each PR so later merges do not move the PR head.
  - Test summary: `--logger "console;verbosity=normal"` and grep `Total tests|Passed:|Failed:|^\s+Failed ` ("Passed!" never prints).
  - bpy through the proxy stalls: download the wheel with a resumable `curl -C - --max-time 120` loop, then pip install the file.
  - xUnit analyzer errors (xUnit2031 etc.) fail the whole test project build: build tests before handing a package back.
  - The Monitor tool expires after 5 minutes: poll CI with a run_in_background loop instead.
  - CommanderReplayAndSaveTests asserted one request; a slow Windows runner sends two. Now `Assert.NotEmpty` (fixed).
  - The shell's cwd drifts into worktrees after `cd`: put `cd <dir> &&` at the start of every command.
  - A/B a failing AI test with one data flag (supply on/off) before reading code: it splits "caused by the feature" from "already red".
  - Lead-only small packages (one function, <100 lines) are faster than briefing a helper and waiting on the build lock.

## If time runs short
Do items 0-2 (art PR, supply on, M6-12); M7-02 fills a second overnight session.
