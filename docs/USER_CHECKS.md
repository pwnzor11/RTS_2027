# User checks

## Standing smoke test
1. `git -C <repo> log -2 --oneline` shows the `Last commit:` hash from docs/HANDOFF.md as the newest or second-newest commit (the handoff commit may sit on top).
2. (Once there is a build) build from clean clone, run, main menu appears.

## 2026-11-02 · Session 1
- Open `docs/research/README.md` on GitHub → 20 rows (00_SYNTHESIS plus 01–06, 10–17, 20–24) and
  every linked file exists and renders.
- Open `docs/DECISIONS.md` → entries run D-001 through D-034; D-033 says "assumed — confirm".
- Open `docs/design/` → three files: `01_architecture.md`, `02_game_design.md`,
  `03_setting_and_factions.md`.
- Open `docs/OPEN_QUESTIONS.md` → only Q-24 and Q-25 are open; rounds 1–3 are under Answered.
- Open `docs/LICENSING.md` → the EA additional terms are summarised (no trademarks, keep notice, mark
  modifications) and the derived-files register is empty.
- Search the repo for "Tiberium", "GDI", "Nod", "Westwood" → hits only inside `docs/research/`,
  `docs/LICENSING.md`, `docs/00_START_HERE.md` and this file, always as references to the EA games,
  never as names of our own things.
- Open `docs/DECISIONS.md` → runs D-001 through D-035 (supersedes the D-034 line above).

## 2026-11-02 · Session 2
- Clean clone on Windows, then `dotnet build RTS_2027.slnx` → `0 Warning(s)` and `0 Error(s)`.
- `dotnet test RTS_2027.slnx` → `Passed!  - Failed:     0, Passed:   192`.
- `dotnet run --project src/Tools -- validate` → `OK rules hash 0xfe839d106d2ec907`.
- `dotnet run --project src/Tools -- run --ticks 200 --seed 1` twice → both print
  `tick 200 hash 0xef7e7f90260e7514`; with `--seed 2` → a different hash.
- `dotnet run --project src/Game` → a 1280×720 window titled "RTS_2027 skeleton": dark grid, 6 squares
  (3 grey-green, 3 rust) circling the centre with facing lines; mouse wheel zooms 1–4×, WASD/arrows or
  middle-drag pans, F1 shows a small bar, Esc quits. (Demo data, not yet the sim.)
- GitHub → Actions → latest "CI" run on main is green with artifacts `test-results` and `RTS_2027-win-x64`.
- Open `art/pilot/contact_sheet.png` → rows for arms A, B, C, a house-colour remap row and an
  in-context ash patch; reply with your pick (Q-24).
