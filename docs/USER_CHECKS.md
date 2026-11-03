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
- GitHub → Actions → latest "CI" run on main is green with artifacts `test-results` and `RTS_2027-win-x64`.
- Open `art/pilot/contact_sheet.png` (v1 pilot) and `art/pilot/contact_sheet_v2.png` (v2 tank, D-043).
- `dotnet run --project src/Game -- --demo` → the old orbit demo (squares circling the centre).
(Test counts, hashes and the default window changed during M1; current values are in the next block.)

### 2026-11-03 · Session 2, M1 (overnight build)
Counts below supersede the M0-11 lines above (the suite grew to 394 tests).
- `dotnet test RTS_2027.slnx` → `Passed!  - Failed:     0, Passed:   394`.
- `dotnet run --project src/Tools -- validate` → `OK rules hash 0x65d5f64698dac4db`, then
  `vehicles 4 · infantry 2 · buildings 4 · weapons 4 · warheads 3 · effects 2`.
- `dotnet run --project src/Tools -- run --ticks 200 --seed 1` twice → both print
  `tick 200 hash 0x56c9dec634c0bdeb`; with `--seed 2` → `0x428e7a1c5ee5812f`.
- `dotnet run --project src/Tools -- map info data/maps/test_64.map.toml` → name test_64, 64x64, starts
  (4,4) and (59,59), map hash `0x38861f8b09f33819`.
- `dotnet run --project src/Tools -- match --ticks 1500 --record match.rpl` then
  `dotnet run --project src/Tools -- replay verify match.rpl` → `OK 1500 ticks, final hash 0x…`.
- `dotnet run --project src/Game` → window titled "RTS_2027 battle sandbox" on test_32: per side 3 tanks, 2 jeeps, 1 artillery
  truck, 5 riflemen (squares) and a turret; player 1 attacks after about 2 seconds. Keys: arrows /
  middle-drag pan, wheel zoom, left-drag select, right-click move (right-click on an enemy: attack), A then
  right-click on ground: attack-move, S stop, G guard, X scatter, F2 cycles fog viewer (you → enemy → all), F3 occupancy,
  F4 paths, F5 weapon ranges, Space pause, `.` single step, Esc quit.
- After Esc the console prints the replay path and the verify command; running it → `OK … ticks`.
- Headless (also run by CI): `dotnet run --project src/Game -- --headless-check 1200` → `sandbox ok tick 1200 p0 4 p1 0 hash 0x0f28036c22a6c8c9`
  on Windows and Linux alike (cross-platform determinism).
- GitHub → Actions → latest CI on main green, including the steps "Headless battle sandbox (end to
  end)", "Verify the sandbox replay" and "Headless match and replay verify".

## Session 3 · M1.5 hardening (2026-11-03)
Pull main first. Requires the .NET 10 SDK.
1. **AI tier matrix:** `dotnet run --project src/Tools -c Release -- ai matrix --ai skirmisher --tiers 1-5 --seeds 2 --ticks 3000`
   prints a 5×5 "Win %" table and "simulated 178500 ticks in …". Placeholder tiers: higher tiers do NOT win yet (M2 tuning).
2. **AI vs idle:** `dotnet run --project src/Tools -- ai match --a skirmisher:3 --b idle --seeds 2 --ticks 3000` → A wins 4 of 4.
3. **Debug console:** `dotnet run --project src/Game`, press F1, type `help`; with the mouse over open ground type
   `spawn vehicle test_tank 3` + Enter → three tanks appear (stacked on one cell). Esc closes the console without quitting.
   `reveal` toggles the full-map view; `speed 4` runs four times faster.
4. **Published zip:** download the CI artifact `RTS_2027-win-x64-<sha>` from the latest main run, unzip anywhere, run `Game.exe`
   → the sandbox opens. The folder contains `data\`, `LICENSE`, `NOTICE.md`, `THIRD_PARTY_NOTICES.md`.
5. **Crash dialog:** in that unzipped folder rename `data` to `data_x` and run `Game.exe` → a message box says it crashed and
   `logs\crash-*.txt` exists. Rename it back.
6. **Replay survives:** run the sandbox twice → `replays\sandbox_last.rpl` and `replays\sandbox_prev.rpl` both exist;
   `dotnet run --project src/Tools -- replay info replays/sandbox_last.rpl` shows the build and embedded rules.
7. **Git LFS (one time, D-056):** in the repo on Windows: `git lfs install`, then
   `git lfs track "data/sprites/**/*.png" "art/**/*.png"`, `git add .gitattributes`, `git add --renormalize data/sprites art`,
   `git commit -m "chore(repo): track sprite sheets with Git LFS (D-056)"`, `git push`.
