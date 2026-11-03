# Technology Options for a Windows 2D RTS in 2026

## Summary

- Every serious option can host a deterministic lockstep sim. The real differences are who owns the determinism problem, how much working RTS infrastructure you inherit, and how much you learn.
- OpenRA (C#, GPL v3, SDL + OpenGL) is the only option that already ships lockstep, replays, a map editor, Lua missions, a YAML rules/mod system and C&C-style gameplay. Its latest stable release is `release-20250330`; a playtest `playtest-20261026` exists. The cost is that you live inside its assumptions (traits, 2.5D cell grid, its release cadence) and learn its architecture rather than building your own.
- The EA code is C++ and Win32 (the Remastered DLL architecture, MFC, Windows 8.1 SDK, VS2017 recommended). Porting the sim into a modern C++ host is possible but is a large archaeology and rewrite job, because the sim is tangled with the 1990s UI/render layer. EA's code is mostly useful as a reference for rules and algorithms, not as a drop-in module.
- C++ with SDL3 is mature (3.4.x with a patch release roughly every two months through 2026) and gives total control of determinism. C# with MonoGame/FNA gives similar control with faster iteration and works well with Claude. Rust/Bevy is exciting but ships a new breaking release every ~4 months (0.20 in Oct 2026), a poor fit for a multi-year hobby project.
- Godot is excellent for tools/UI but its own docs-level guidance is that it is not deterministic; you would use it as a renderer and run the sim in your own integer code, at which point the engine is mostly overhead for an RTS.
- Unity is closed source and the editor/runtime terms are not GPL-friendly; Unreal is overkill. Both are out for an open-source-forever goal.
- **Recommendation:** a C# custom engine on .NET with an integer/fixed-point sim, built on FNA or MonoGame (shortlist leader), with OpenRA as the explicit runner-up and the best "fast to playable" path. Details below.

## 1. C++ stack

**SDL3.** Stable and actively released. The releases page shows 3.4.0 (1 Jan), 3.4.2 (Feb), 3.4.4 (Apr), 3.4.6, 3.4.8, 3.4.10 (May), 3.4.12 (Jul), 3.4.14 (Aug), 3.4.16 (Sep), 3.4.18 (2 Oct). Theme of 3.4: better interop between the SDL_GPU API and the 2D renderer, native PNG load/save, pixel-art scaling mode. The `SDL_Renderer` always batches draws (call `SDL_FlushRenderer()` before using a raw graphics API), defaults to linear filtering (set nearest-neighbor explicitly for pixel art), and audio is built on `SDL_AudioStream`. License is zlib (permissive, GPL v3 compatible). SDL2 is in maintenance mode; start on SDL3. SDL gives window, input, audio device, timers, gamepads; you add sprite batching (SDL_Renderer is enough for a 2D RTS at C&C scale), mixing (SDL_mixer or miniaudio) and everything else.

**raylib** (v6.2 shown on the releases page, Oct): simple C API, zlib license, great for learning, but it is a game-framework with immediate-mode drawing; fine for a prototype, less structured for a large codebase. **SFML 3** (3.1.0 latest; 2.6.x final 2.x): C++17 API, text rendering rewritten in 3.1, networking with TLS/HTTP/IPv6; fine and friendlier C++ than SDL, smaller community for strategy games. **bgfx**: low-level cross-API renderer (BSD); heavy for 2D sprites, not recommended. Gamepad/audio/windowing would still be needed.

**Build and tools.** CMake + vcpkg (manifest mode) + Visual Studio 2022/2026 or clang-cl is the standard Windows path; vcpkg has SDL3, imgui, miniaudio, Lua, sol2. Dear ImGui is the obvious choice for in-game debug/dev tools (state inspectors, desync diff viewers, AI debuggers) and is MIT.

**Porting the EA sim with a new renderer.** What it takes, from the repo README and structure: the repo is `TIBERIANDAWN/`, `REDALERT/`, `CnCTDRAMapEditor/` and `SCRIPTS/`; it is archived read-only (20 Jan 2025) and builds only as Win32 via `CnCRemastered.sln` with Windows 8.1 SDK + MFC, VS2017 recommended (later versions hit a struct-packing mismatch). The license is GPL v3 *with additional terms* (check `LICENSE.md`; the brief already forbids EA names/assets). Practical work: (1) separate sim from the 1990s graphics/UI code that is interleaved in the same classes (object classes call draw/sound); (2) replace the Win32/DirectDraw/MFC and DOS-era file I/O; (3) handle 16-bit/segmented idioms, packed structs, global state and implementation-defined behaviour; (4) retain bug-for-bug determinism of its integer math and its RNG while changing the compiler. Gain: authentic C&C behaviour (pathfinding quirks, AI, economy numbers) and a ready-made rules model. Cost: a large legacy codebase to understand before you can add anything, and you inherit 1995 architecture you want to *learn from*, not copy. Most helpers in this research set are studying the code for exactly that reason; the likely best use is "port algorithms, not files."

## 2. C# stack

**MonoGame** (v3.8.5 released 15 Jul, 3.8.5.1 on 14 Aug): added DirectX 12 and a cross-platform Vulkan native backend, a new content pipeline project system, OpenAL and SDL 2.32 updates. Licensed Microsoft Public License / MIT-style (open). Large community; XNA-style `SpriteBatch` is ideal for 2D sprites. WindowsDX targets use Windows Forms and are incompatible with AOT; DesktopGL (SDL) is the AOT-friendly target.

**FNA** (XNA4 reimplementation, Microsoft Public License; actively maintained, ~3.1k stars): accuracy-focused, SDL-based, with an official appendix for NativeAOT on PC (needs `rd.xml` for reflection-only types, a filtered SDL API list and native-lib copy steps). Many shipped commercial games. Slightly more manual setup than MonoGame (no content pipeline GUI), better for programmers who like owning the stack.

**Raylib-cs**: C# bindings to raylib; fine for prototypes, thinner ecosystem. **Stride**: MIT, active (6,800+ commits, .NET 10 to build), 3D-first with 2D templates; heavy editor, small RTS community. Both are less suitable than MonoGame/FNA.

**Godot 4 + C#**: see section 4.

**.NET publish story.** MonoGame recommends self-contained `win-x64` publish (zipped folder), explicitly warns against single-file ("a hidden self-extracting zip") for large games, and disables ReadyToRun/tiered compilation in templates to avoid stutter. Native AOT removes JIT and runtime reflection but needs trimming-safe code. Installing a self-contained build means users need no runtime. .NET 8 is LTS; later LTS (.NET 10) is what current tooling (Stride, VS 2026) targets, so choose the newest LTS and move on. Player-facing downside: bundled runtime makes the download tens of MB, irrelevant for a hobby game.

**Determinism gotchas in .NET.**
- Floating point: since RyuJIT on x64 uses SSE2 there is no x87 excess-precision problem, and basic IEEE ops (+, -, *, /, sqrt) are deterministic on the same architecture. Risks: JIT may use FMA contraction on some platforms/versions, `Math.Sin/Cos/Atan2/Pow` call the platform C runtime and can differ across OS/CPU/runtime versions, and ARM vs x64 may diverge. Gaffer on Games' summary: arbitrary cross-compiler/cross-architecture float determinism is unlikely, and transcendentals can differ between AMD and Intel. For a Windows-only LAN game with one shipped binary this is survivable, but fixed-point is safer and also makes saves/replays portable across versions.
- Collections: `Dictionary<K,V>` and `HashSet<T>` enumeration order is not guaranteed by contract (insertion order in practice until removals, then reuse of free slots). Use `List`, sorted keys, or iterate IDs in ascending order. `string.GetHashCode()` is randomized per process; never use it in sim logic or ordering. `System.Random` should not be used; ship your own PRNG (the EA code's RNG is a model).
- Other: multithreading in the sim, `float` parsing/formatting culture, LINQ unstable sorts (`List.Sort` is not stable), uninitialized-state (less of an issue in C# than C++, which Forrest Smith's Supreme Commander desync post lists as a top cause).

**OpenRA as a base.**
What it is: GPL v3, C#, SDL + OpenGL (Windows also DX11), modular assemblies (`OpenRA.Game`, `OpenRA.Platforms.Default`, `OpenRA.Server`, `OpenRA.Mods.Common`, `OpenRA.Mods.Cnc`, `OpenRA.Mods.D2k`, utility, tests). Ships Red Alert, Tiberian Dawn and Dune 2000 mods. World coordinates are integers: 1024 units per cell, `WPos/WVec/WRot`, `WAngle` with 1024 steps, `CPos/CVec` for cells: that is a fixed-point sim by design, which is exactly the determinism strategy we want.

Mod SDK (`OpenRA/OpenRAModSDK`): a bare environment for "a new mod/game". `make.cmd` fetches the engine, `launch-game.cmd` runs it, an `example` mod and an `OpenRA.Mods.Example` C# project are provided, `mod.config` pins `ENGINE_VERSION`, and scripts package Windows installers, macOS `.app` and Linux AppImages. SDK/engine upgrades go through a wiki guide. Legal fit: engine and SDK scripts GPL v3; custom DLLs/Lua you add must be GPL-compatible; data (art, audio, YAML) can carry other terms. That matches the owner's "GPL v3 forever" goal.

Standalone games on OpenRA: [Combined Arms](https://www.moddb.com/mods/cc-all-mod/articles) (huge C&C/RA/Scrin overhaul: ranks, mutually exclusive upgrade paths, new units; standalone download); [Romanov's Vengeance](https://github.com/MustaphaTR/Romanovs-Vengeance) (Red Alert 2-style, GPL-3.0, 345 stars, last push July 2025 per a mirror site, i.e. slowing); [OpenHV](https://github.com/OpenHV/OpenHV) (original open-content RTS based on Hard Vacuum: GPL v3 code, CC art, ~3,200 commits, LAN and internet play, bots, map editor; proof that a fully original, non-C&C game is feasible). I could not verify a current "Krush Kill 'n' Destroy" or "Red Alert 2 mod" status beyond Romanov's Vengeance.

Gained: lockstep netcode with sync hashes, replays, lobby, server, map editor, Lua mission scripting, YAML rules, sprite/palette pipeline, AI bots (incl. basic skirmish AI), minimap, fog, build queues, Windows installer, 15 years of RTS bug fixes. Time to playable slice is days to weeks.

Constrained: (1) the engine's idioms: actors composed of traits in YAML, orders, activities, 2.5D cell grid with optional isometric support, infantry sub-cells, 8 or 32-facing sprites. Rule changes inside that vocabulary are cheap; changes outside (e.g. different movement model, different map model) mean forking the engine; (2) asset expectations: sprite formats (SHP-like, PNG sheets via sequences), palettes, `.oramap` maps; (3) release cadence: stable releases are infrequent (20230225, 20231010, 20250330), with playtests between; mods pinned to an engine version must be forward-ported by hand; (4) you are a downstream of a volunteer project's priorities; (5) learning value is mostly "learn OpenRA", not "learn how to write an RTS", though the owner could read and extend the engine's C#. A hybrid is possible: fork the engine (GPL allows it) rather than just mod it.

## 3. Rust stack

**Bevy.** Release history from bevy.org/news: 0.15 (Nov 2024), 0.16 (Apr 2025), 0.17 (Sep 2025), 0.18 (13 Jan 2026), 0.19 (19 Jun 2026), 0.20 (8 Oct 2026). Each release carries hundreds of PRs and, historically, breaking API changes; budget a migration every 4-5 months or pin a version forever. ECS is a good fit for RTS data layout, 2D sprite rendering is built in (`bevy_sprite`, `bevy_sprite_render`), licensed MIT/Apache-2.0 (GPL-compatible). Networking: `bevy_ggrs` (GGRS rollback; its repo has docs "pitfalls" and "debugging desyncs"; sim logic "must be deterministic"; a compatibility table maps crate versions to Bevy versions, with the fetched page mixing 0.18 on main and 0.19 as latest, so check live); `lightyear` is client-server. Rollback is aimed at fighting games, not 500-unit RTS lockstep, so for an RTS you would write your own lockstep. Bevy's scheduler can run systems in parallel, so you must force deterministic ordering and iteration (entities are not guaranteed sorted). Compile times are a real tax on iteration (mitigation: `bevy_dylib` dynamic linking, Cranelift backend; I could not verify current numbers). Fixed-point crates exist (`fixed`, fixed32-style crates; I could not verify a leading deterministic-sim crate).

**macroquad / ggez / comfy**: simple 2D frameworks, smaller communities, less maintained. Fine for prototypes, not a long-run basis.

**Rust verdict.** Great for correctness (no uninitialized memory, a top desync cause per the Supreme Commander postmortem), and `no_std`-style integer sim is natural. Worst case for AI-assisted iteration: borrow-checker friction in large ECS refactors and breaking engine upgrades. "Shipping game in 2026" is feasible but the RTS-specific ecosystem is thin.

## 4. Engines

- **Godot 4.x**: latest stable 4.7.2 (18 Aug 2026), 4.8 in beta (29 Oct), 4.6.3 (May 2026). MIT licensed, excellent 2D, strong editor, Windows export easy, GDScript or C# (.NET). Determinism: GDQuest's glossary states plainly that "Godot, like most game engines, is not deterministic" and recommends plugins (SG Physics 2D, Jolt for 3D) or fixed-point on `Vector2i`. The Rapier plugin claims cross-platform determinism on IEEE 754-2008 platforms (self-reported). For an RTS you would write the sim yourself in C# integers and use Godot only for rendering/UI/tools: workable but you carry the engine's scene tree and update loop for little gain.
- **Unity**: Runtime Fee cancelled (for games) in Sep 2024; Personal stays free, ceiling raised to $200k revenue. Still closed-source, proprietary editor terms, which conflicts with a GPL-forever goal (you can ship GPL game code, but not the engine, and the owner is rusty on it anyway). Not recommended.
- **Unreal**: overkill for 2D sprites; large, C++ heavy; custom license (source-available, royalties). Not recommended.
- **Defold** (Lua, source-available license) and **LÖVE/Love2D** (Lua, zlib): easy 2D, but Lua doubles as sim language, and Lua numbers are doubles by default (determinism care), performance limited at RTS scale (LuaJIT helps). Not a learning match for C++/C#/Rust skills.
- **Heaps (Haxe)**: used by Shiro Games for Northgard and Dune: Spice Wars; compiles through HashLink VM or to C; DirectX 11 and OpenGL/SDL2 backends; open source. Strong evidence of shipping RTS-adjacent games, but Haxe is a niche language and AI/community support is thin.

## 5. Tools side

- **Map editor**: Tiled (latest 1.12.2, 27 May 2026; licensed GPL for the editor, BSD-ish libs; unverified license text) with custom properties and a JSON export, or an in-game editor. OpenRA's editor is included if you fork it; EA's `CnCTDRAMapEditor` is C# WinForms and could seed an own editor. In-game editor using ImGui (C++) or custom UI is the better long-term choice because it validates the same loader as the game.
- **Sprites**: Aseprite source is under its EULA (source-available, not open source; some modules MIT) so it is a paid tool for the author, not a dependency; the output (PNG/JSON sheets) is free of restrictions. LibreSprite is a GPL fork of the last GPL Aseprite; I did not verify its current status. Blender can render isometric/top-down pre-rendered unit sheets at 8 or 16 facings via script, which is the classic C&C-like pipeline. Because the owner cannot use EA assets, a pre-rendered pipeline from original 3D models is realistic.
- **Audio**: miniaudio is public domain/MIT-0, single header, dependency-free, wav/flac/mp3 decoding; SoLoud is zlib; OpenAL Soft is LGPL (dynamic linking OK). FMOD is proprietary (free indie tier with revenue limits; I could not fetch current terms) and is not source-available, so it clashes with an open-source-forever goal. Prefer miniaudio (C++), OpenAL via MonoGame/FNA (C#).
- **Lua embedding**: C++: sol2 (MIT) over Lua 5.4/LuaJIT; C#: NLua/KeraLua (native Lua, fast) or MoonSharp (pure C#, targets Lua 5.2, last stable 2.0.0.0 in 2016, a new maintainer announced July 2026 per its site, 3.0 beta), the latter easier for determinism and sandboxing; Rust: mlua. OpenRA already embeds Lua for missions. Alternative: skip Lua and script missions in the host language/YAML triggers; lower risk for determinism.
- **Hot reload**: .NET Hot Reload (`dotnet watch`) works for code/data; data-driven YAML/JSON reload on file change is a bigger win in every stack. C++ hot reload is possible (Live++ is commercial; VS Edit-and-Continue is limited).
- **Debugging determinism** (applies to all): hash the full sim state every N ticks per player and compare over the network (Supreme Commander hashes once per second; mismatch ends the match); keep per-system sub-hashes to bisect; record inputs for replay and re-run offline; run a second process locally in lockstep; log RNG call counts per tick; assert FPU mode if using floats. Forrest Smith's post notes uninitialized variables and freed-memory reuse as common causes, and that bisecting printed hashes found a Demigod bug after almost a week, so build the tooling on day one.

## 6. Comparison (1 = poor, 5 = excellent)

| Criterion | C++/SDL custom | C++ port EA sim + new renderer | C#/MonoGame (or FNA) custom | OpenRA fork/mod | Rust/Bevy | Godot (render only) |
|---|---|---|---|---|---|---|
| Learning value (architecture, AI) | 5 | 4 (reading 1995 code is educational but slow) | 5 | 3 (learn OpenRA, not RTS from scratch) | 5 | 3 |
| Time to playable slice | 2 | 1 | 3 | 5 | 2 | 3 |
| Control over determinism | 5 | 5 (inherits EA integer sim) | 5 (with fixed-point) | 4 (engine does it for you) | 4 | 3 (must bypass engine) |
| Tooling/debugging on Windows | 5 (VS debugger, ImGui) | 4 (VS2017-era project) | 5 (VS, Rider, hot reload) | 5 | 3 (compile times) | 4 |
| EA code reuse | 3 (algorithms by hand) | 5 | 3 (port algorithms) | 2 (OpenRA already reimplements) | 2 | 2 |
| Community/ecosystem 2026 | 5 | 2 | 4 | 3 (small, steady) | 4 but fast moving | 5 |
| Long-term maintainability | 4 | 2 | 5 | 3 (engine upgrades by hand) | 2 (breaking releases) | 4 |
| Fit with Claude-assisted development | 4 | 3 (large legacy context) | 5 | 4 (large codebase, strong C# training data) | 3 | 4 (C#) / 3 (GDScript) |

## Shortlist

1. **C# custom on FNA/MonoGame (leader).** Gain: full ownership of the sim (integer/fixed-point in your own `Sim` library with no graphics reference), fastest iteration, best fit with AI tooling and the owner's languages, easy unit tests of determinism (run the sim twice, compare hashes), VS/Rider debugging, permissive libs. Give up: months of infrastructure before it feels like C&C (pathfinding, fog, lockstep, editor, replays); no inherited gameplay features.
2. **OpenRA fork/mod (fastest, runner-up).** Gain: playable C&C-like game within weeks, GPL aligned, proven netcode and editor. Give up: architecture learning, freedom over core rules (you either fit the trait model or fork the engine), engine upgrade chores, tied to maintainers' pace.
3. **C++/SDL3 custom, porting EA algorithms.** Gain: closest to the source material, maximum control and performance, SDL3 is healthy and zlib-licensed. Give up: slower iteration, more manual memory/desync hazards (uninitialized state is the classic desync source), more build-system work, weaker AI-assisted loops than C#.

Not shortlisted: Rust/Bevy (release churn), Godot (determinism fights the engine), Unity/Unreal (license), Defold/Love/Heaps (niche).

## Implications for RTS_2027

- **Recommendation:** Build the sim as a pure, graphics-free C# library using integer/fixed-point math (OpenRA-style 1024 units per cell and 1024-step angles are a proven model), and put rendering in a thin FNA or MonoGame front end. This keeps the engine decision reversible: the sim could later move to C++ or be reused by a headless AI/test runner.
- **Recommendation:** Do a 2-3 week spike on OpenRA before committing: build the SDK `example` mod, add one new unit, one Lua mission, and one new trait. If rule changes feel natural, OpenRA becomes the pragmatic path; if you keep fighting its model, you will have learned what to copy into your own sim.
- Do not use floating point in sim logic; if you must, restrict to one binary, one architecture, no `Math.Sin/Cos`, and re-check .NET/JIT changes on upgrade. Replace `Dictionary` iteration with ordered structures and ban `string.GetHashCode()`, `System.Random`, wall-clock time and unordered parallelism from the sim.
- Port from EA the *algorithms and rule numbers* (economy, damage tables, AI team logic, pathfinding), not the files; the 1995 sim is coupled to UI/render and Win32 and the repo is archived without support.
- Choose MonoGame if you want the largest community, DX12/Vulkan backends and a content pipeline; choose FNA if you value accuracy, a simpler stack and NativeAOT support. Both use XNA-style `SpriteBatch`; switching later is cheap if sim and rendering are separated.
- Publish with self-contained `win-x64`, ReadyToRun/tiered compilation off; skip single-file. Keep AOT as an optional later experiment (DesktopGL/FNA only).
- Use miniaudio (C++) or OpenAL (C#) for audio, not FMOD; keep original sprite/art tools out of the build (Aseprite is EULA-licensed; use Blender and PNG sheets so any contributor can build).
- Build desync tooling in week one: per-tick state hash, replay recorder, two-process local lockstep test, per-system sub-hashes.
- Rust/Bevy is the highest-risk choice for a multi-year hobby build because of API churn; revisit only if the owner's priority is learning Rust.
- Unity and Unreal are out: not compatible with an open-source-forever promise.

## Open questions for the owner

1. **Primary goal: learning or shipping?** Options: (a) learn by building the engine and AI from scratch (C# or C++ custom); (b) get a playable C&C-like game fast and spend effort on campaign/AI (OpenRA fork). Both can coexist if the spike in the first recommendation goes well.
2. **Language preference for the long haul:** C# (faster loop, easiest with Claude, OpenRA-compatible), C++ (closest to EA source, more control, more desync risk), or Rust (safest memory, slowest churn tolerance).
3. **How much EA code to port (Q-02):** (a) none, reference only; (b) selected algorithms (pathfinding, AI, economy); (c) the whole sim ported to a new host. Option (c) is the most expensive; (b) is my suggestion.
4. **Is an in-house map/campaign editor required, or is Tiled plus a validator enough?** Affects whether ImGui/WinForms tool work is scheduled early.
5. **Do you want to be bound by a third-party engine's release cadence?** Relevant only to the OpenRA path (pin an engine version and fork vs track upstream).

## Sources

- [OpenRA download page](https://www.openra.net/download/) (stable release-20250330; playtest-20261026; Windows requirements)
- [OpenRA/OpenRA repository](https://github.com/OpenRA/OpenRA) (C#, SDL + OpenGL, GPL-3.0, project layout)
- [OpenRA Mod SDK releases](https://www.github.com/OpenRA/OpenRAModSDK/releases) (SDK contents, release history)
- [OpenRA Mod SDK README](https://github.com/OpenRA/OpenRAModSDK) (build, packaging, licensing of mods)
- [OpenRA World Coordinate System](https://github.com/OpenRA/OpenRA/wiki/World-Coordinate-System) (1024 units/cell, WPos/WAngle)
- [OpenHV](https://github.com/OpenHV/OpenHV) (standalone game on OpenRA)
- [Romanov's Vengeance (mirror page)](https://gittrend.io/repo/MustaphaTR/Romanovs-Vengeance)
- [GamingOnLinux: Combined Arms overhaul](https://www.gamingonlinux.com/2024/08/free-classic-rts-openra-mod-command-conquer-combined-arms-has-a-huge-overhaul/page=1/)
- [EA CnC_Remastered_Collection](https://github.com/electronicarts/CnC_Remastered_Collection) (build requirements, archive status, license)
- [SDL releases](https://github.com/libsdl-org/SDL/releases) and [SDL2 to SDL3 migration notes](https://github.com/libsdl-org/SDL/blob/main/docs/README-migration.md)
- [raylib releases](https://github.com/raysan5/raylib/releases); [SFML releases](https://github.com/SFML/SFML/releases)
- [MonoGame releases](https://github.com/MonoGame/MonoGame/releases); [MonoGame packaging docs](https://docs.monogame.net/articles/getting_started/packaging_games.html)
- [FNA repository](https://github.com/FNA-XNA/FNA); [FNA NativeAOT appendix](https://fna-xna.github.io/docs/appendix/Appendix-E%3A-NativeAOT-on-PC/)
- [Stride repository](https://github.com/stride3d/stride)
- [Bevy news/releases](https://bevy.org/news/); [bevy_ggrs](https://github.com/gschup/bevy_ggrs)
- [Godot download archive](https://godotengine.org/download/archive/); [GDQuest: deterministic simulation](https://www.gdquest.com/library/glossary/deterministic_simulation); [Godot Rapier physics](https://godot.rapier.rs/)
- [Unity: canceling the Runtime Fee](https://unity.com/en/blog/unity-is-canceling-the-runtime-fee)
- [Shiro Games technology stack (Haxe blog)](https://haxe.org/blog/shirogames-stack/)
- [Gaffer On Games: Floating point determinism](https://gafferongames.com/post/floating_point_determinism/)
- [Forrest Smith: Synchronous RTS engines and a tale of desyncs](https://www.forrestthewoods.com/blog/synchronous_rts_engines_and_a_tale_of_desyncs/)
- [miniaudio](https://github.com/mackron/miniaudio); [MoonSharp](https://www.moonsharp.org/); [Aseprite repository](https://github.com/aseprite/aseprite); [Tiled releases](https://github.com/mapeditor/tiled/releases)

Not verified: FMOD current license terms (page did not load), Bevy compile-time numbers, Defold/LÖVE/LibreSprite current status, Krush Kill 'n' Destroy and a Red Alert 2 mod beyond Romanov's Vengeance, NLua/mlua/sol2 versions, Tiled license text, exact .NET Math.* cross-machine behaviour in current runtimes.
