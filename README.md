# RTS_2027

RTS_2027 is an open-source 2D real-time strategy game for Windows, in the style of the classic Command & Conquer
games. It is a hobby project: a single-player campaign, skirmish against AI, and later LAN play.

RTS_2027 is not affiliated with or endorsed by Electronic Arts; no EA code, art or audio is included.

## Build and run (Windows)

One-time setup: install the .NET SDK named in `global.json`, then install Git LFS and turn it on for your user:

```
git lfs install
```

Clone the repository (with LFS files), then from the repository root:

```
dotnet build RTS_2027.slnx
dotnet run --project src/Game
dotnet test RTS_2027.slnx
```

Useful options for `src/Game`: `--empty` starts with the legacy 3-tank setup, `--demo` draws the fabricated demo
source, and `--headless-check 200` runs a scripted battle with no window and exits (0 on success). If the game
crashes it writes `logs/crash-<time>.txt` next to the exe and shows where it is.

The tools in `src/Tools` (`validate`, `match`, `replay verify`, `--version`) run with
`dotnet run --project src/Tools -- <command>`.

## Publish a Windows build

```
dotnet publish src/Game -c Release -r win-x64 --self-contained -o publish/Game
```

The publish folder holds the game, its `data/` folder, `LICENSE`, `NOTICE.md` and `THIRD_PARTY_NOTICES.md`.

## Documentation

- [docs/00_START_HERE.md](docs/00_START_HERE.md): project rules, the session protocol and where things live.
- [docs/DECISIONS.md](docs/DECISIONS.md): design decisions.
- [docs/CREDITS.md](docs/CREDITS.md): third-party libraries and asset provenance.

## Licence

RTS_2027 is free software under the GNU General Public License version 3. See [LICENSE](LICENSE) (including the
additional permission for MonoGame), [NOTICE.md](NOTICE.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md),
and [docs/LICENSING.md](docs/LICENSING.md) for the rules that apply to code and assets.
