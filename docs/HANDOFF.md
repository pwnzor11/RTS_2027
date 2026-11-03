# Handoff: Session 2 (in progress) — checkpoint after wave 1
Last commit: b597d70 (2026-11-03) — checkpoint after M1 wave B
Where things stand: overnight M1 run (D-040) in progress. Waves A–C (M1-01..M1-10) are on main, CI
green; 394 tests. Wave D (M1-11 battle sandbox and overlays) is running. Restore point: branch
`restore/pre-m1` (3279113).

## Start here (shown to the user as the plan; nothing starts until they say go)
1. Ask the user: has he read `docs/design/03_setting_and_factions.md`, and does he want to change any
   faction mechanic before code assumes it (tunnel network, radiation denial, conscript swarm)? —
   options: **go with the draft, refine during the roster pass** / change X now.
2. Ask the user: confirm D-033 (asset licence allow-list incl. CC BY-SA 4.0) — **yes** / strict only.
3. M0-11 Repo skeleton (Opus lead, helpers Sonnet for Sim core, Haiku for tooling/CI):
   .NET solution per `design/01_architecture.md` §2; `Sim` library with `EntityId`, slot arrays,
   `SimRandom`, `Fixed32`, lepton/cell/facing math, `Command`, `Sim.Step`, state hash; determinism
   analyzer test (no float/double/Random/DateTime/Dictionary-iteration in `src/Sim`); TOML loader
   with rules hash; headless `Tools` CLI; xUnit tests incl. replay determinism and save round-trip;
   GitHub Actions on windows-latest; MonoGame `Game` project that opens a window and renders a grid
   from a `SimSnapshot`. User can try: clone, `dotnet build`, `dotnet test`, run and see a window.
4. M0-12 Art pilot (lead-owned, pilot before fan-out per owner preference): one Authority vehicle
   three ways (Blender headless render, AI-assisted frames, hand pixel) at two candidate cell sizes
   (48 px and 64 px), 16 facings, house-colour remap test; deliver a contact sheet. User picks.
5. Checkpoint after each wave; then M1 planning.

## Half-finished work
- None. `docs/design/03_setting_and_factions.md` below the "Decided" line is a proposal awaiting the
  owner's reaction.

## Waiting on the user
- Q-25 confirm D-033; reaction to the setting doc.

## How to run this session
- Lead: Opus (build session). Helpers: Haiku default; Sonnet for `src/Sim` core, the analyzer and
  anything touching determinism or serialisation.
- Repo clone: `/home/claude/rts_2027` (push to `origin main` directly; no branch protection as of
  session 1). Remote may report "moved to RTS_2027.git"; harmless.
- Reference clones (read-only; re-clone if the workspace was reclaimed): see SESSION_LOG session 1.
- Commit everything before launching helpers (worktrees can't see uncommitted files). Write
  `COMMON.md` for code helpers (layout, `dotnet test` commands, forbidden list) before the first wave.
- Blender for the art pilot: `pip install bpy --break-system-packages` works headless in the cloud
  workspace (unverified in session 1; try first, fall back to apt/snap or a Blender tarball).
- Lessons from session 1: Haiku research helpers hit robots.txt walls on Spring/0 A.D./OpenRA wikis;
  a helper brief that proposes executing third-party repo code gets blocked by the auto-mode
  classifier, read-only briefs pass; helpers run 5–15% over word targets when tables are involved.
- Don't: use EA names/assets; put floats in `src/Sim`; start M1 systems before the skeleton's
  determinism tests are green.

## If time runs short
Land the `Sim` library with its determinism test and CI first; the MonoGame window and the art pilot
can slip a session.
