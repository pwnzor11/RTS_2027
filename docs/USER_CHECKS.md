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

### 2026-11-03 · Session 2, M1 (overnight build)
Counts below supersede the M0-11 lines above (the suite grew to 394 tests).
- `dotnet test RTS_2027.slnx` → `Passed!  - Failed:     0, Passed:   394`.
- `dotnet run --project src/Tools -- validate` → an `OK rules hash 0x…` line, then
  `vehicles 4 · infantry 2 · buildings 4 · weapons 4 · warheads 3 · effects 2`.
- `dotnet run --project src/Tools -- map info data/maps/test_64.map.toml` → name test_64, 64x64, starts
  (4,4) and (59,59), map hash `0x38861f8b09f33819`.
- `dotnet run --project src/Tools -- match --ticks 1500 --record match.rpl` then
  `dotnet run --project src/Tools -- replay verify match.rpl` → `OK 1500 ticks, final hash 0x…`.
- `dotnet run --project src/Game` → battle sandbox on test_32: per side 3 tanks, 2 jeeps, 1 artillery
  truck, 5 riflemen (squares) and a turret; player 1 attacks after about 2 seconds. Keys: arrows /
  middle-drag pan, wheel zoom, left-drag select, right-click move (on an enemy: attack), A + right-click
  attack-move, S stop, G guard, X scatter, F2 cycles fog viewer (you → enemy → all), F3 occupancy,
  F4 paths, F5 weapon ranges, Space pause, `.` single step, Esc quit.
- After Esc the console prints the replay path and the verify command; running it → `OK … ticks`.
- GitHub → Actions → latest CI on main green, including the steps "Headless battle sandbox (end to
  end)", "Verify the sandbox replay" and "Headless match and replay verify".
