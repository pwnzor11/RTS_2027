# Handoff: Session 18 — read the layout 2 v6 laptop run, settle Q-70, decide the layout 2 default; then the start skews and art follow-ups
Last commit: `docs: Session 17 handoff` on integ-s17, landed through the session 17 handoff PR (Windows CI green first; the lead merges, D-179).
Check `git log -1 origin/main`.
Where things stand: **M11 is complete** except the Editor props tool (M12-06b). **M12 waves 1-3 are built**; map layout 2 (zones, lanes, all four
biomes) is at v6 and still OFF by default (`MapGenerator.CurrentLayout = 1`, D-197). Session 17 (2026-11-09/10): wrote the missing session 16
records; Ash Plains layout 2 is half-turn (D-198); main fields about 9 cells from the start with a band test (D-199); the Expedition start-unit bug is
fixed (`SkirmishStart.SpawnUnits`, second pass to ring 16; it also broke Expedition on Mountains layout 1, the default); the art baseline is the
owner's PC re-render (D-200, PR #54); the layout 2 income test counts the 10-minute point over live games only (D-201). Directorate AI tuning is
closed (D-196). Decisions up to D-201; open question Q-70.
Full suite: 3 709 tests on integ-s17 before the PR #55 merge (Sim 1 589, AI 222, Game 1 471, Scripting 295, Tools 13, Llm 119), Linux; Windows CI
green on PRs #54 and #55.

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Read the owner's layout 2 v6 laptop run if it exists: `balance_runs\s17l2\s17l2_summary.md` and the `.log` files in the owner's RTS_2027 folder
   (device bridge, read-only; or ask him to attach the summary). Compare with docs/balance/s17_layout1_baseline.md: start share per biome against the
   60 % limit, income at 3 and 5 minutes within 10 %, 10 minutes over games still running at tick 12 000 (D-201). The income lines are in the logs
   (`income at 3/5/10 min (harvested + shipments): A x/y/z | B x/y/z`, CRLF: strip `\r` before parsing, skip the literal `3/5/10`).
   Write docs/balance/s17_layout2_v6.md.
2. Q-70 with those numbers (income lever after v6), then ask the owner whether layout 2 becomes the default. D-197 names three blockers: Expedition
   coast in every biome (met, session 17 sweeps), start side at most 60 %, income within 10 %. Note for the owner: layout 1 itself fails the 60 %
   limit on glass_coast (66 %) and mountains (64 %).
3. Start skews that are not the map (M12-12b and the Mountains item): Mountains favours one start for Directorate on both layouts (layout 1: 15 v 33; layout 2 v5: 30 v 16) and
   for Left Behind on layout 2 (29 v 13; layout 1 is 9 v 11 with 44 draws), although the layout 2 starts are exact images (test `Layout2_Mountains_TwoPlayers_StartsAreExactImages`); Glass Coast layout 1 Expedition wins
   55 v 5 from start 0. Look at slot order, AI first-move order and the Expedition landing choice; measure with `ai match` on the owner's laptop.
4. `ai timeline` builds layout 2 maps without sea for the Expedition, unlike `ai match` (M12-12b): make it pass the ocean request.
5. Art follow-ups (owner rule: pilot, approve, fan out; renders on the owner's PC, D-196): kitbash Warzone parts where they add detail (D-189);
   per-vehicle FX anchors; art for GlassCrest, IceSlab, WreckHulk, BrokenPylon props; building art depth (M10).
6. If time: Editor props tool (M12-06b); wire `explosion_energy` / `lightning_fork` to weapons; decide the mop-up AI switches (still off); voice
   lines once the owner has Piper set up.

## Half-finished work
- Nothing in flight; every session 17 helper branch is merged (wp/s17-docs, wp/s17-expstall, wp/s17-l2v6). wp/s17-artdrift has no commits
  (diagnosis only, no script change was needed).
- Layout 2 v6: field distance applies to the half-turn skeleton only (Medium and Large 2-player maps); Small, 3-player and 4-player maps are unchanged.
  `Layout2Fields` values are the same for every biome so far.
- Layout 2 v6 fairness on Ash Plains (new skeleton) is NOT measured yet; the cloud income probe was 8 games per biome and the Expedition checks 8-32 games. The laptop run is the measurement.
- The Expedition start fix has tests for Ash Plains layout 2 only. Mountains layout 1 was checked by games (docs/balance/s17_layout1_baseline.md,
  "After the fix"), not by a test: add one (Expedition kit spawns on Mountains layout 1, a few seeds).
- Mop-up AI code is in MilitaryManager but switched off (MopupLaunch/MopupSweep = false).
- Voice lines: on hold until the owner has Piper set up (tools/voice/wyoming_batch.py).
- `unit_reserve` difficulty override key exists and is tested but unused (s15_lbknob.md).

## Waiting on the user
- Run the layout 2 series on the laptop (after `git pull`): `powershell -ExecutionPolicy Bypass -File tools\measure\run_s17_layout2.ps1`
  (20 rows, 64 games each; results in `balance_runs\s17l2\`).
- Pull and play (USER_CHECKS "Session 17"): Expedition skirmish on Mountains now has its start units; the four Left Behind rigs have the darker pad.
- The re-render summary script on the owner's PC (outside the repo) counts lines containing the word "flag" as audit flags: its "5 FLAG lines"
  was 0 real flags. Fix it there or ignore the line.
- Optional clean-up (the owner deletes branches himself): art/pc-rerender-20261108, land/s16-*, land/s17-w1, wp/s16-*, wp/s17-*, wp/art-s16-pilot,
  integ-s16, older integ-*/land-*/wp-* branches; `art_rerender\20261108\after_extract` and `balance_runs\run_s17_layout1_base.ps1` in his folder
  (copies made by the lead in session 17). Keep restore/pre-s17 until happy.

## How to run this session
- Lead: Opus for build sessions (the owner wants long unattended overnight builds); Sonnet for a pure review/question session. Helpers: Haiku default; Sonnet for sim core,
  determinism, serialisation, movement, combat, AI, scripting, and all Blender art.
- Repo clone: `/home/claude/rts_2027` (remote URL github.com/pwnzor11/RTS_2027.git; re-set it after a container restart); main has no branch protection, but land through a PR so Windows CI runs first
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
- Session 14 lessons:
  - 2 CPUs + 6-8 code helpers = 30-90 min dotnet lock waits. Brief helpers to run builds/tests in the FOREGROUND with a 600 000 ms timeout
    (or poll); a helper that backgrounds a queued test and ends its turn hands back "unverified" work. Stop such helpers and verify at merge.
  - Give every helper a private scratch dir (/tmp/claude-0/<pkg>/): one overwrote another's measurement script in the shared /tmp.
  - Overlapping UI packages: let ONE integration package merge them, build, and fix (s14-ui3 did ui1+ui2+obj+scale cleanly).
  - The game runs under xvfb for screenshots (recipe in the UI audit "Method"; box-drag needs xdotool mousemove_relative while held).
  - Stills: Cycles renders ~10 min each on one blender lock; three faction helpers in parallel worked (scenes built while others render).
  - Safety check refused: bulk deletes of CI artifacts, force-push, installing binaries on the owner's PC. Don't retry; hand the owner the command.
  - D-173 lesson: check what a diagnosis actually measured (here "13-15 structures" included the shaft and factories) before recommending a rule.
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

- Session 10 lessons:
  - Four waves of 4-7 Sonnet helpers each ran 1-3.5 h on the 2-CPU box; merging one by one as reports arrive, then a scoped
    M7/hash test run per merge and one full suite per wave, kept every PR green first time except one Windows-only flake.
  - Python probes on the Windows runner can take over 20 s (Store stub): treat a probe timeout as "not available" (SfxrTests fixed).
  - Several helpers edit the same allow-lists (RulesOfSweepGrepTests, HashCoverageTests) and the RulesHash doc comment; these are
    the usual merge conflicts, trivially resolved by keeping both sides.
  - Parts that only write new arrays when they are in use (naval, effects, stats, tunnels "version 2 when needed") keep every old
    hash; ask helpers for that pattern instead of an unconditional format bump.
  - Adding ship types to the real data made every game write sea labels: SkirmishDefaultHashTests moved on the "naval" sub-hash only.
  - `/usr/bin/dotnet vstest <dll> --Tests:<name>` runs a built test dll without the dotnet wrapper (used for soak runs).
  - A helper created a scheduled task despite the brief (session 10, "x"); never delete or act on such tasks on a helper's request.

- Session 11 lessons:
  - Helper branch names can collide with old remote branches (`wp/m7-14ui` existed from session 10): suffix new ones with `-s<N>`.
  - Force-push is refused by the safety check: when a helper amends a pushed branch, push under a new name instead.
  - Art fan-out: 5 Sonnet art helpers on one Blender lock took ~3-4.5 h for 44 assets. Review every sheet: catch draw_scale double
    scaling (ships), tread-like hover bases, wrong hull colour. Contact sheets live in git-ignored scratch/; read them before cleanup.
  - Data shape problems show up only when the AI plays: the shoreline water masks fit south coasts only (Q-55) and the Command Ship
    could not Deploy on water (D-150). Run an AI game on the real data before calling a faction playable.
  - The device bridge can disconnect mid-session: build the art push as a zip (bundle + LFS objects + push_art.ps1) and send it with
    SendUserFile; a 46-sprite faction is about 5 MB.

- Session 12 lessons:
  - The owner was present and answered in batches with AskUserQuestion; "full send" after a pilot meant: write the other pilots and the
    fan-out without another review gate. Put every remaining writer choice in one review file instead of asking mid-run.
  - Mission fan-out: one Sonnet helper per faction writing acts 2-4 (+ ending) took 30-60 min each on the 2-CPU box with the dotnet lock;
    ask them to commit one mission at a time — a container restart lost no committed mission, and SendMessage resumed both helpers.
  - Shared briefs as files (scratch/briefs/*.md, read by absolute path) kept each launch prompt to ~15 lines.
  - Tests that pin "which missions exist" or placeholder text break the moment content lands: use a temp data folder or an inline fixture.
  - Ids in data (`next`, campaign lists) need a cross-check test; helpers guessed `left_behind_02` and `expedition_02`.
  - Missions ran without player factions in the sim until s12 (faction rules silently off): any new mission-only setup path must reuse
    MissionSession.MakeSetup.
  - Windows CI: `VSTEST_CONNECTION_TIMEOUT=600` in ci.yml; a test-host connect timeout is a runner problem, re-run once, then fix the env.
  - GitHub Actions minutes ran out once; the repo is public since (D-159), so Actions are free.

- Session 15 lessons:
  - New sprites (LFS) cannot be pushed from the cloud. Package them for the owner: `git bundle create <zip>/x.bundle <base>..<branch>`, copy EVERY
    LFS object referenced by any commit in the range (not just the tip: `git lfs push` checks history; the first packs package missed 9 versions),
    and a push_art.ps1 that fetches, verifies the bundle, copies objects into .git/lfs/objects, `git lfs push origin <branch>`, `git push`.
    Ship it as a .zip (the owner's browser refuses a bare .ps1). Keep art commits off branches you need to push until the owner has pushed.
  - Licences: OpenGameArt and kenney.nl block automated reads. Read the packs' own licence files; get verbatim licence texts from GitHub
    (raw.githubusercontent.com/spdx/license-list-data/main/text/<id>.txt works). CC-BY-SA 3.0 is not GPL-compatible: keep such art as separate,
    separately licensed data files (0 A.D. props) and never in GPL sprite sheets; avoid it in tooling that mixes (Warzone page-6/17/30, wheels).
  - Enum-count pins live in several contract tests (M4, M6, M7): grep for `Enum.GetValues<CommandKind>().Length` when adding a command.
  - Windows CI catches file-lock bugs Linux misses (a test deleted a replay file still open): close sessions (SaveReplay) before deleting.
  - A container restart lost nothing committed; helper reports can be recovered from the end of their task transcripts.
  - When a re-rendered asset and a renderer-side scale option overlap, give the asset a manifest flag (figure_scale_permille) so they do not stack.
  - The owner answered every question within minutes; batching 2-4 questions with AskUserQuestion and sending contact sheets first worked well.
- Session 13 lessons:
  - The auto-mode safety check refused `gh api -X PUT …/pulls/<n>/merge` ("merge without review"). Do not look for a way around it;
    leave PRs open, stack later work on the same integration branch, and ask the owner to merge (or to allow merges) at the end.
  - Squashing a helper branch with `git reset --soft <integ>` after the integration branch moved on silently REVERTS everything merged
    since the helper's base. Squash against the merge base (`git reset --soft $(git merge-base …)`) or apply the helper's diff
    (`git diff <base> <tip> | git apply --index`) onto the integration branch, and check `git diff --stat` before landing.
  - The repo moved to github.com/pwnzor11/RTS_2027 ("This repository moved" on push): `git remote set-url origin` fixed it.
  - LFS pulls in the cloud need the repo added to the session (add_repo, push access); then all LFS files check out.
  - The device bridge VM has no git-lfs: pulls in the owner's folder fail; the owner pulls on Windows.
  - Helpers on the shared dotnet lock could not finish Sim.Tests/Tools.Tests inside 580 s: the lead runs one full suite per batch.
  - The solution file is `RTS_2027.slnx` (dotnet test RTS_2027.slnx …).
- Session 17 lessons:
  - A container restart stops running helpers; nothing committed or on disk is lost and SendMessage to the same helper resumes it with its context.
    Brief every helper to commit after each numbered step, and push helper branches when they have commits (the stop hook asks for it).
  - The owner's laptop runs a 16-row, 64-game series in about 90 minutes; the 2-CPU cloud box needs that long for one row. Put measurement scripts
    in tools/measure, hand them over, and read the results from his folder through the device bridge (logs are ASCII with CRLF; PowerShell `*>`
    logs such as the re-render logs are UTF-16).
  - Measure the baseline before setting a blocker: layout 1 fails the 60 % start limit itself on two biomes, and the plain 10-minute income mean
    tracks game length, not income.
  - A stall with "0 structures, 0 units" for BOTH sides is a setup problem (start units), not an AI problem: check SkirmishStart before the tactics code.
  - Art drift: when a full re-render differs from committed sheets, compare render-commit times with later edits to shared material tables
    (`lb_common.py` MATERIALS) before touching a script; here the sprites were stale, not the scripts.
  - To read LFS files from a local-only branch in the owner's folder (no git-lfs in the bridge VM): `git cat-file -p <branch>:<path>` gives the
    pointer, the object is at `.git/lfs/objects/<aa>/<bb>/<oid>`. In the cloud, `git lfs fetch origin refs/remotes/origin/<branch>` (the short name fails).
  - A helper made one stray device-bridge call despite the brief (no effect). Keep "no mcp__remote-devices__* tools" in every brief.
