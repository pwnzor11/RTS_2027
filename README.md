# RTS_2027

RTS_2027 is an open-source 2D real-time strategy game for Windows, in the style of the classic Command & Conquer
games: build a base, harvest a resource, raise an army and break the other side's. It is a hobby project with three
goals, in order of importance: a single-player campaign, skirmish against AI, and later LAN play with family and friends.

RTS_2027 is not affiliated with or endorsed by Electronic Arts; no EA code, art or audio is included.

## The world

*No spoilers here: everything below is what any player learns in the first minutes of the game.*

In October 1962 the crisis that everyone expected to end at the brink did not. For nine days missiles, then bombers,
fell on the continent's cities, rail hubs and reactors. The war never ended with a surrender. It simply ran out of things
to throw, and left behind a sky the colour of a bruise.

What came next is called the First Winter. Ash blotted out the sun for most of a year, and the people who kept anyone
alive were not generals but lineworkers, water engineers and railway dispatchers with hand-copied lists of who still
had a boiler, a truck or a doctor. And the ash kept falling, for decades. It poisoned the fields, but refined in a
centrifuge it also became **ashfall**: reactor fuel, and the only power anyone could still build machines around. The
land under the richest drifts became the land worth fighting for.

By 2001 four powers want that land.

- **The Continental Reconstruction Authority.** Born on a frozen lakeshore in 1963, when forty-one surviving counties
  signed a one-page Charter: restore power, restore roads, count the living, and dissolve when the work is done. It is
  a government of clerks and engineers who would rather fix a culvert than fight. Its citizen army is slow, expensive
  and very hard to move. *Count the living, mend the line.*
- **The Directorate of the Interior.** When the bombs fell, a continuity-of-government complex under a mountain range
  sealed its doors from the inside, and for forty years nobody opened them. Two generations have grown up by red lamp
  light, under the warm, unhurried Voice that reads the Director's orders. Now its hatches are opening in the ash, and
  its loudspeakers announce that the surface was only ever held in trust.
- **The Left Behind.** In the winter of 1990 an evacuation convoy left the Glass Coast, and four thousand people did
  not make it onto the last train. They stripped the stranded trains and built crawler-rigs, and became caravans that
  never stop moving and never leave a wreck without looking inside it. They remember exactly who left them, and they
  are owed.
- **The Overseas Expedition.** Across the ocean another continent burned a little less. In 2000 its Tidewater Concordat
  sent the Second Relief Fleet: forty-one ships, a hundred aircraft and eleven thousand volunteers. The official reason
  is rescue. Every officer knows the other one: the richest ash fields on earth lie on this side of the water.
  *Return with the tide.*

The look is atompunk: valve radios, teletype strips, stencilled serial numbers, sealed reactor-driven vehicles and
decon showers, in a world that never got past 1962.

## Where the project stands (November 2026)

| Milestone | Status |
|---|---|
| M1 / M1.5 · Deterministic simulation core (20 Hz, integer maths, seeded RNG, replays) | Done |
| M2 / M3 · Playable skirmish: Authority and Directorate, full tech trees, tunnels and burrowing, superweapon, audio, main menu, Archive | Done |
| M4 · Skirmish depth: procedural maps (4 biomes, S/M/L, 2–4 players), garrisons, three-tier tech, upgrades, spectator mode, map editor | Done except the AI difficulty ladder balance |
| M5 · LLM commander: an optional language model sets strategy every 30 s and talks in character as a faction leader (your own API key) | Done; owner play-test with a real key pending |
| M6 · The Left Behind: mobile rig bases, wrecks and salvage, raids, the Second Drift storm, AI, art | Done |
| M7 · The Overseas Expedition: ships, submarines, landings, carriers, home shipments, beachheads, floating docks, AI, art | Playable in skirmish; final acceptance and owner play-test open |
| M8 · Campaign for all four factions | In progress: mission scripting, objectives, triggers, dialogue, reinforcements, AI script hooks, campaign screens, briefings and ending sequence are built; a tutorial and the first Authority mission run on it; the other pilot missions come next |
| M9 · LAN multiplayer | Not started |
| M10 · Polish (art pass, music, balance, installer) | Not started |

What you can play today:

- **Skirmish** with all four factions on generated or hand-made maps, 2–4 players, against AI at five difficulty tiers,
  with an optional ocean edge for naval play. Supply caps, three-rank veterancy and superweapons are in.
- **Spectate** AI against AI (or LLM commander against LLM commander).
- **Campaign → Tutorial**, plus the first Authority mission as a pilot.
- **Archive**: an in-game encyclopaedia of the world's history, factions, units and leaders that unlocks as you play.
- **Map editor**, save/load, and replays.

Known gaps: the AI difficulty tiers are not yet evenly spaced, most of the campaign is still to be written, and the art is
a first pass that will be revisited once the game is complete.

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
- [docs/ROADMAP.md](docs/ROADMAP.md): milestones and what is done.
- [docs/DECISIONS.md](docs/DECISIONS.md): design decisions.
- [docs/design/](docs/design/): design documents for each milestone.
- [docs/lore/](docs/lore/): the setting in full (contains campaign spoilers).
- [docs/CREDITS.md](docs/CREDITS.md): third-party libraries and asset provenance.

## Licence

RTS_2027 is free software under the GNU General Public License version 3. See [LICENSE](LICENSE) (including the
additional permission for MonoGame), [NOTICE.md](NOTICE.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md),
and [docs/LICENSING.md](docs/LICENSING.md) for the rules that apply to code and assets.
