> **Lead correction (session 3):** E-1 says MonoGame can be used "under its MIT option". It cannot: MonoGame's LICENSE.txt is Ms-PL, with MIT only for "Portions Copyright © The Mono.Xna Team". The FSF-derived license lists class Ms-PL as GPL-incompatible. See README.md, finding 1.

# Lens E: Licensing, legal and distribution risk, plus repo/process hygiene

Not legal advice; verify every legal point with a lawyer before relying on it.

## Findings (ranked, most dangerous first)

### E-1 · Binaries ship with no licence, notices or source offer; MonoGame licence choice undocumented · severity: WILL-BREAK
- Evidence: `.github/workflows/ci.yml` publish job runs `dotnet publish src/Game -r win-x64 --self-contained -o publish/Game` and uploads only `publish/Game`. `grep -rn "LICENSE\|ThirdParty\|CREDITS" .github src/Game/*.csproj` returns nothing. MonoGame is dual-licensed Ms-PL and MIT (community.monogame.net). Ms-PL is, as I recall, on the FSF list as GPL-incompatible (not fetched; verify).
- Failure: Any copy handed to family or friends for LAN play is a conveyance under GPL v3 §6, which requires the licence text and Corresponding Source (or a written offer). The binary also carries the .NET runtime (MIT) and MonoGame, which need their own notices. If the Ms-PL option were the one relied on, combining it with GPL code is a compatibility question. This bites at the first LAN test with a friend (M5 at the latest, but in practice earlier).
- Fix: Add a publish step that copies `LICENSE`, `NOTICE` and `ThirdPartyNotices.txt` into `publish/Game`, plus a source-offer line (repo URL and tag). Write down in `docs/CREDITS.md` that MonoGame is used under its MIT option. NOW; S.

### E-2 · EA's additional terms have no carrier file yet; they must travel with any EA-derived code · severity: WILL-BREAK (latent, triggers at first port)
- Evidence: EA's `LICENSE.md` (fetched 2026-11-03) says "Any propagation or conveyance of this program must include this copyright notice and these terms", and "You may not distribute any modification of this program using any Electronic Arts trademark". Our root `LICENSE` is plain GPL v3 text. `docs/LICENSING.md` register is "(none yet)". No file contains the EA notice.
- Failure: The first ported file (planned in M1/M2 reuse) ships under a LICENSE that omits EA's notice and terms. That is the conveyance violation the notice rule describes, and it stays invisible until someone checks.
- Fix: Create `NOTICE` (or `EA_NOTICE.txt`) with the EA copyright and additional terms copied verbatim from the EA repo; reference it from `LICENSING.md`; add a pre-port checklist (keep header, mark file, register, notice present in publish). Also add a README line: "Not affiliated with or endorsed by Electronic Arts." NOW; S.

### E-3 · Per-file copyright headers absent, so the "keep EA header / mark modified" rules have nothing to attach to · severity: COSTLY-LATER
- Evidence: 0 of 149 `.cs` files under `src/` and `tests/` contain "Copyright" or a licence line (`grep -rl -i copyright src tests` returns 0). `README.md` is 10 bytes (`# RTS_2027`).
- Failure: At M2 and later, reviewers and AI helpers add files without headers. Retrofitting headers across the repo later means touching every file and mixing EA and project headers by hand.
- Fix: Add an SPDX/GPL header template now (`// SPDX-License-Identifier: GPL-3.0-only`, copyright owner, project URL), add it to `COMMON.md` style rules, and fill README with build, licence and credits pointers. NOW; S.

### E-4 · .gitignore misses publish output and other artefacts; a local publish can be committed permanently · severity: CHEAP-WIN
- Evidence: `.gitignore` has `bin/ obj/ *.user .vs/ TestResults/ /scratch/ /art/pilot/work/ /.claude/ replays/`. `git check-ignore publish/Game/x.dll` returns not ignored. `saves/x.sav` and `.env` are also not ignored.
- Failure: The first local `dotnet publish -o publish/Game` followed by `git add -A` puts 40 to 100 MB of DLLs into history, which is hard to remove later without rewriting it.
- Fix: Add `publish/`, `saves/`, `*.sav`, `*.blend`, `*.blend1`, `*.log`, `.env`, `.DS_Store`, `Thumbs.db`. NOW; S.

### E-5 · Binary art is committed to git without LFS or a retention rule · severity: COSTLY-LATER
- Evidence: `git ls-files art data` totals 2.3 MB. The largest blobs are `art/pilot/contact_sheet.png` (1.16 MB) and `contact_sheet_v2.png` (0.52 MB), both in history. `art/pilot/README.md` describes repeated re-renders.
- Failure: M2 to M4 (dozens of vehicle, building and infantry sheets at 2 to 4x cell size, 16 facings) with every re-render committed will push history into hundreds of MB. Rewriting history later breaks every clone and CI cache.
- Fix: Decide before M2: track Blender sources and generator scripts in git, keep rendered sheets in Git LFS (add `*.png` LFS rule only for `data/sprites/**` and `art/**`), and keep pilot contact sheets out of the repo or in LFS. S now, M later.

### E-6 · CC BY-SA and CC BY attribution cannot be met by CREDITS.md alone in a shipped binary · severity: COSTLY-LATER
- Evidence: D-033 allows CC BY-SA 4.0 (owner confirmed in D-036). `docs/CREDITS.md` is a repo file with empty tables. CC BY-SA 4.0 is one-way compatible with GPLv3 (Creative Commons announcement, 2015), so the licence choice is sound.
- Failure: CC BY requires attribution "reasonable to the medium", which for a game usually means an in-game credits screen. A `publish/` binary with no credits breaks the condition. Mixed per-asset licences in one GPL repo also become hard to audit once there are hundreds of imports.
- Fix: Add a per-asset manifest (`data/assets.toml`: file, licence, author, source URL, attribution string, modified yes/no) and generate `CREDITS` from it, including a shipped credits file. Make the import rule "no import without a manifest row". Before first import (M2). S now, M later.

### E-7 · Runtime dependencies are unlisted; LGPL obligations for native libraries are unaddressed · severity: COSTLY-LATER
- Evidence: `src/Game/Game.csproj` uses MonoGame.Framework.DesktopGL 3.8.6 (NuGet version confirmed). `src/Data/Data.csproj` uses Tomlyn 2.10.1 (BSD-2-Clause, from memory). `src/Sim/Sim.csproj` uses System.IO.Hashing 10.0.14 (MIT, from memory). `docs/CREDITS.md` "Libraries" table is "(none yet)". MonoGame DesktopGL on Windows is expected to bundle SDL2 (zlib) and OpenAL Soft (LGPL-2.x, through OpenTK); not verified in the package.
- Failure: An LGPL DLL in a self-contained publish needs its licence text, a source offer and the ability to replace it, which is not provided. MoonSharp (planned, M4) is BSD-3-Clause from memory and also needs a credits row.
- Fix: Run `dotnet publish` once, list the native DLLs in `publish/Game`, and fill the CREDITS library table with each name, version, licence and use. Add one line per planned library (MoonSharp). NOW; S.

### E-8 · Faction, unit and setting names not yet cleared for trademark · severity: COSTLY-LATER
- Evidence: `docs/design/03_setting_and_factions.md` uses "Continental Reconstruction Authority", "Directorate of the Interior", "Ashfall", "Pitchblende", "Broadcast", "The Sunrise Device". My grep found no EA, Westwood or C&C names in our content (only in docs and the LICENSING text, as intended). I did not run a trademark search, so collisions with other games and brands are unknown.
- Failure: A name clash found after a public store page or trailer costs a rename across art, store and data. Names live in data (D-029), so the rename is cheap now and expensive later.
- Fix: One name-clearance search (USPTO, EUIPO, Steam, itch.io) for the faction names, Ashfall and the two superweapons before M3 public build. Keep `RTS_2027` as a working title only. NOW; S.

### E-9 · AI-generated art has no provenance or prompt rules · severity: COSTLY-LATER
- Evidence: D-032 permits AI generation "for all art", disclosed in CREDITS. The `AI-assisted artwork` table has columns for tool and human work but no rows. D-032 notes copyright is unsettled.
- Failure: Outputs that imitate C&C or Westwood art (through prompts that name the franchise or its style) bring EA trademark and copyright exposure into a project whose licence file promises none. Generator terms of use can restrict use that the GPL assumes. Pure AI output may not be copyrightable in the US (US Copyright Office guidance, not checked), so it cannot be licensed under GPL and sits outside the licence altogether.
- Fix: Record for every AI asset: tool, version, terms accepted, prompt, and the human edits. Ban franchise names and "in the style of" references in prompts. NOW for the rule, before M2 for the first asset; S.

### E-10 · Art pipeline is not reproducible on Windows or in CI · severity: CHEAP-WIN
- Evidence: `art/pilot/README.md` uses `python3 tools/art/...` and needs Blender 5.2's `bpy` module plus Pillow and numpy, with no pinned requirements file. The README says "No downloads, no external assets or fonts", which I could confirm from `tools/art/build_tank.py`. There is no README under `tools/art/` (the brief's reference). CI does not run the art scripts.
- Failure: On a Windows box, `python3` may not exist and the `bpy` wheel needs a matching Python. A later session cannot regenerate sprites from a fresh clone, and the `house_cal.txt` cache trap in the README becomes a silent mismatch.
- Fix: Add `tools/art/README.md` (Windows commands, Python version) and `tools/art/requirements.txt` with pinned versions. Keep art tools out of CI. S, before M2.

## Things you checked that are fine
- Root `LICENSE` is the full GPL v3 text (first 20 lines and header checked). EA's additional terms, as summarised in `docs/LICENSING.md`, match the EA `LICENSE.md` I fetched (trademark, affiliation, notice, modification marking, indemnity).
- Test-only packages (xUnit Apache-2.0, coverlet MIT, Microsoft.CodeAnalysis) are not referenced by shipped projects.
- The art pilot is procedural Blender output with no downloaded assets; PIL and numpy are dev-only.
- Git history has no secrets: the only matches for password, token or key are in prose docs and in `docs/research` text. 43 commits, 230 tracked files.
- `.gitattributes` (`text=auto`, LF for TOML and Lua, binary PNG) and `global.json` (SDK pinned, rollForward latestFeature) make a fresh Windows clone build. CI runs on `windows-latest` from `global.json`.

## Unknowns (what I could not verify)
- Whether MonoGame 3.8.6's DesktopGL package bundles SDL2 and OpenAL Soft exactly as I expect, and their exact versions. I did not inspect the NuGet package or the publish output. Verify with `dotnet publish` and a DLL listing (E-7).
- The FSF's GPL-incompatibility status for Ms-PL: from memory, not checked on gnu.org (E-1).
- Whether the EA repo contains any non-code files. I could only read the summarising fetch of `LICENSE.md`, not the full text. Check the tree before porting any non-code file (E-2).
- The trademark status of our names (E-8) and the copyright status of AI output (E-9): not searched beyond general knowledge.
- Tomlyn (BSD-2-Clause), System.IO.Hashing (MIT) and MoonSharp (BSD-3-Clause) licences are from memory; confirm on NuGet before writing them into CREDITS.
