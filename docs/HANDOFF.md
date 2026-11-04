# Handoff: Session 5 — long overnight build: finish M3, start M4 skirmish depth and the new factions
Last commit: d0eed7e (+ this handoff commit) (2026-11-04) on integ-m3 — see "Where things stand" for what is on main vs integ-m3
Where things stand: M3 waves 0–2 are merged on `integ-m3` (contracts, tunnels, burrowing, hardening + gates, radiation /
Blackout / stealth, upgrades / auras / transports / engineers / air, Directorate data, faction UI, audio, Directorate
AI, Sunrise Device, Archive with owner-approved lore, integration fixes, session 3 follow-ups). PR #7 (wave 1) is open
for Windows CI; integ-m3 has moved past it, so update or replace the PR before landing. Art lives on local `wp/art-*`
branches and reaches GitHub only when the owner runs `_art_push/push_art.ps1` (D-084). The owner approved: the
Directorate palette, conscript, Smelter Vent, 4 infantry, both vehicle batches, the lore. Morning review pending: the
turret rework sheets (crawler, rocket truck, decon truck) and the dir_shaft pilot rev 2. Decisions D-082..D-090; the
expansion plan is D-087 (4 factions, skirmish depth, LLM commander) and the new milestone order is in ROADMAP.

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Owner: run `_art_push/push_art.ps1` (USER_CHECKS Session 4, item 1); say how the turret rework and the Command
   Shaft look (sheets in art_out were sent in session 4; resend if needed).
2. Land M3 so far: bring the art branches into an `integ-art` merge
   (resolve tools/art/build_vehicle.py between art-veh1 and art-veh2: keep both feature sets, D-083), update PR #7 or open
   a new PR integ-m3 → main, wait for Windows CI, land.
3. Finish M3: M3-18 game wiring (turret/shadow/dive layers, Sunrise button + targeting + flash, Archive tracker, Hatch →
   Bore Head button),
   M3-11 AI tiers 2–5 (also fix the skipped InfantryLeavesIrradiatedCells test), M3-19 mission replays, M3-ART (14
   Directorate structures in the dir_shaft style using tools/art/structures/dir_struct_common.py, 4 Authority structures,
   agitprop turret via S.mount), M3-14 integration + balance (tunnel time saved 30–50 %, win rates near 50 %), Q-34.
4. M4 skirmish depth, all equal priority (D-087): procedural maps (4 types × S/M/L), AI-vs-AI spectator mode,
   garrisoning, three-tier tech trees and upgrades for both factions, the map editor. Contract-first, like M3.
5. Design wave for M6/M7 (overnight-friendly, D-088): new-faction design docs (06_left_behind.md, 07_expedition.md)
   with roster, mechanics and asymmetry; lore drafts in docs/lore/ in the approved style (owner approves); palette and
   one pilot unit + building per faction, lead-reviewed, then fan out. Naval systems for the Expedition need their own
   contract design. M5 LLM commander can start once M4 contracts are in (Anthropic + OpenAI-compatible, key only in local
   settings, never logged).

## Half-finished work
- M3-16 main menu lobby: merged at wrap-up (42c2482); only headless renders exist (art_out/menu_lobby*.png, menu_scene_1..4.png,
  sent to the owner) — the first live look is the owner's. The menu's Archive button opens the real ArchiveScreen (M3-17 kept on merge).
- M3-ART structures: only dir_shaft (rev 2, on wp/art-str1) is done; wp/art-str2 was created but never used.
- Merge of art branches into code: not done (art is pushed only by the owner script).
- Leftover branches on GitHub the owner can delete after landing: `wp/m3-*`, `wp/art-*` once merged, older `integ-*`.

## Waiting on the user
- push_art.ps1; turret rework + Command Shaft review; Q-34 (assumed values, can wait for the balance pass);
  merge PR(s) after Windows CI if they want to watch it.

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
