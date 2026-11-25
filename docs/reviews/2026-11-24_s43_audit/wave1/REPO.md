# REPO — tests, tools, CI, installer, licensing, repo hygiene
Scope read: tests/ (all 7 projects, grep/script over every [Fact]/[Theory]; read S13 balance, PathStallPerfProbe, NetCodecTests, AiStallAuditTests, AudioProvenanceTests, LicenceHeaderTests), src/Tools/Tools.csproj, .github/workflows/ci.yml + release.yml (+ git log of .github), installer/rts_2027.iss, docs/LICENSING.md, NOTICE.md, THIRD_PARTY_NOTICES.md, docs/CREDITS.md, data/ui/credits.toml, data/licences/, data/audio/PROVENANCE.toml, .gitattributes, .gitignore, Directory.Build.props, Game.csproj, tools/ (grep for paths/IPs). No GitHub API access was used (brief: no external services), so the "no Windows CI runs since Nov 12" claim could not be checked at the source.

## Findings

### REPO-01 · MED · bug
What: The installed game records replays into `replays/` relative to the working directory. The installer puts the game in `{autopf}` (Program Files) and every shortcut sets `WorkingDir={app}`, so a normal (non-admin) user runs with a CWD they cannot write to. Replay recording fails (console-only error, which is invisible with no console) and the Replays list (`ReplayCatalog.DefaultFolder` = `GetFullPath("replays")`) points at the same unwritable folder. Saves and settings use AppData and are fine; only replays are affected.
Where: src/Game/Menu/ReplayCatalog.cs:29; src/Game/Sandbox/SimSnapshotSource.cs:32-54 (`"replays/..._last.rpl"`); src/Game/Sandbox/ReplayRecording.cs:38-44 (catches UnauthorizedAccessException, writes to Console.Error only); installer/rts_2027.iss `[Setup] DefaultDirName={autopf}\...`, `[Icons] WorkingDir: "{app}"`, `PrivilegesRequired=admin`.
Evidence: read. CI's smoke test runs from a writable temp folder, so it cannot catch this.
Impact: Everyone who plays the installed build gets no replays and no "verify with" path; the post-game "Save replay" action reports failure. Zip users running from a user folder are fine.
Fix idea: Resolve the replay folder under %APPDATA%\RTS_2027 (like saves/logs) or add a writable-dir fallback.   Changes a match outcome / number / visible text?: no (new files location only)

### REPO-02 · MED · debt
What: Release version is not applied to the binaries. `release.yml` publishes with no `-p:Version=` and takes the tag only for the zip name and the installer `/DAppVersion`. The exe, replay headers, crash logs and LAN build checks use `Directory.Build.props` `<Version>0.1.5</Version>`. Tagging v0.2.0 gives an installer called 0.2.0 that contains a game reporting 0.1.5+sha. The workflow also has no check that the tag matches the props version and does not run the test suite before publishing (tag can sit on an unbuilt/red commit; CI's `nightly` job does at least `needs: build-test`).
Where: .github/workflows/release.yml (Publish and Build installer steps); Directory.Build.props:12.
Evidence: read.
Impact: Wrong version in bug reports/replays; replays from "0.2.0" are stamped 0.1.5.
Fix idea: Pass `-p:Version=${tag#v}` to publish, or fail the job if tag != props version; add a `dotnet test` or `needs` gate.   Changes a match outcome / number / visible text?: no (version string only)

### REPO-03 · MED · text
What: docs/LICENSING.md is stale and contradicts the repo. It says third-party assets are "recorded in docs/CREDITS.md once we have any" and the allowed-licence list omits CC BY-SA 3.0 and GPL-2+ sources that now ship; the EA-register is "(none)" (true, see Checked). Missing from every licence document: Warzone 2100 (GPL-2.0-or-later, tools/art/wz/README.md) is a read-only reference and its renders are not in CREDITS, which is fine only because the `*_wz` modules appear to be unshipped candidates. But 0 A.D. renders (CC BY-SA 3.0) ship in data/sprites/props/zeroad/ while D-033 (docs/DECISIONS.md:226-229) says "CC BY-SA 3.0 ... excluded", status "assumed — confirm". The exception is recorded only in a DECISIONS line ~1411 and CREDITS ("owner approved ... session 15"). There is no explicit supersession of D-033 and LICENSING.md does not mention it.
Where: docs/LICENSING.md:46 and the allow-list paragraph; docs/DECISIONS.md:226-229 vs :1411; docs/CREDITS.md line for "0 A.D. prop renders"; data/sprites/props/zeroad/LICENSE.txt.
Evidence: read (grep). CC BY-SA 3.0 is not GPL-3.0-compatible one-way (only BY-SA 4.0 is); CREDITS says "Kept as separate data files, never merged into GPL-only sheets", but they are shipped in the same installer and loaded by ZeroAdPropAtlas alongside GPL code.
Impact: A licence-compliance reader sees an allow-list that forbids what ships. No gameplay effect.
Fix idea: Add a D-0xx superseding D-033 for 0 A.D. (and say it is aggregated, not combined), update LICENSING.md.   Changes a match outcome / number / visible text?: no

### REPO-04 · MED · text
What: CREDITS.md header promises "Every third-party asset, library and AI-assisted artwork is listed here (D-032, D-033)", but its "AI-assisted artwork" table lists only the 12 act stills. Roughly 300 shipped Blender-rendered, Claude-scripted assets have no CREDITS row: data/sprites/vehicles (84 png), buildings (74), infantry (22), data/portraits (77), data/ui/skins (40), art/booklet (151) and art/pilot (89). Only props/blender, fx, stills, audio and fonts have rows.
Where: docs/CREDITS.md "AI-assisted artwork" (lines ~43-56).
Evidence: script — grep of every tracked png/ogg/wav/ttf under data/ and art/ against CREDITS.md + credits.toml + THIRD_PARTY_NOTICES.md + PROVENANCE.toml: groups with zero mentions = the dirs above. (Audio: PROVENANCE.toml covers all 306 audio files, 0 missing, 0 stale.) The generator scripts exist in tools/art, tools/booklet, tools/art/stills, so the info is recoverable, only the register is incomplete.
Impact: D-032 disclosure of AI-assisted art is incomplete in the in-game credits/About, which read CREDITS.md.
Fix idea: Add one table row per directory (tool, author-of-script, human work) rather than per file.   Changes a match outcome / number / visible text?: yes (credits text only)

### REPO-05 · LOW · debt
What: The one audio test that is `[Skip]`ped is stale: `UnknownSourcesAreReported` says "owner to confirm the sources of ... unknown", but PROVENANCE.toml has zero entries with source "unknown" (sources used: kokoro-82m, stable-audio-open-1.0, sfxr, ace-step-1.5). The test would pass today and would guard future regressions, but being skipped it guards nothing.
Where: tests/Game.Tests/Audio/AudioProvenanceTests.cs:120.
Evidence: script — regex over data/audio/PROVENANCE.toml: source set = {kokoro-82m, stable-audio-open-1.0, sfxr, ace-step-1.5}; 306 entries = 306 audio files.
Impact: A future "unknown" source would slip through CI.
Fix idea: Remove the Skip.   Changes a match outcome / number / visible text?: no

### REPO-06 · LOW · debt
What: Skip census and tests that cannot fail. Explicit `Skip =` attributes: 2 (above, plus AI.Tests AiStallAuditTests.cs:214 `RepairManager_KeepsRepairOn_WhileHealthKeepsFalling`, "S18 AI-09: awaiting owner decision", a still-open item from the last audit; its twin on :195 pins the opposite behaviour, 0/4/8 toggling). `[ProbeFact]` env-gated skips: 4 (SpectatorPerfBench, SpriteSheetTests:466, VisionPerformanceTests, CarrierBalanceProbe), with the attribute class duplicated in Game.Tests/S18 and Sim.Tests/S18. 3 `S13CampaignBalanceTests` facts (e.g. `ConscriptsVsRiflemen_NowAndCandidates`) only call PrintFight and contain no assertion, so they cannot fail. `NetCodecTests.cs:189` `Assert.True(decoded >= 0)` is a tautology (the real assertion is "no exception other than InvalidDataException", which is valid, only the extra line is dead). Early-`return` tests that silently pass when data is absent: FxLookTests:484, FxTests:165, CatalogAndTextTests:102, LobbyContentHashTests:73; SecretStoreTests:42/65/88 return on non-Windows (documented; CI is Windows).
Where: as listed.
Evidence: script — regex counting [Fact]/[Theory] methods with no assert-like call: 12 of 3335 methods (list above plus PathStallPerfProbe, MidMissionSaveLoadTests, CampaignMissionEndTests whose assertions are in helpers = false positives). grep for `catch {}` in tests: 0.
Impact: Small; 3 balance "tests" count as passing coverage but check nothing; the 4 early-return tests would hide a moved data folder.
Fix idea: Move print-only facts to ProbeFact; replace `return` with Assert.True(Directory.Exists(...)).   Changes a match outcome / number / visible text?: no

### REPO-07 · LOW · debt
What: CI gaps. (a) Triggers are `push: main` and `pull_request: main` only, so a pushed branch with no PR (HANDOFF line ~408 says "push a land/ branch early" for Windows CI) gets no run; and PRs/pushes created with the default GITHUB_TOKEN by automation do not trigger workflows. (b) No path filters or `if:` conditions skip the build-test job (verified: none), so the HANDOFF "no Windows CI runs since Nov 12" is not explained by the workflow file; the only workflow change in that window is e6f6657 (Nov 13, nightly release). Likely causes are outside the file (Actions disabled/quota, as S23 notes the storage quota, or PR bot token). (c) Python tooling tests (tools/audio/test_sfxr.py, tools/audio/sfx/test_make_sfx.py) and tools/audio/check_provenance.py are never run in CI (the C# AudioProvenanceTests partly covers it). (d) `choco install innosetup` is unpinned. (e) `Verify the sandbox replay` depends on replays/sandbox_last.rpl being written by an earlier step (replays/ is git-ignored), an ordering dependency, not a bug today.
Where: .github/workflows/ci.yml:3-8, steps list; HANDOFF.md:57.
Evidence: read; `git log --since=2026-11-12 -- .github` returns one commit.
Impact: Landing without Windows verification, as the HANDOFF already notes.
Fix idea: Add `workflow_dispatch` and a `push: branches: ['land/**']` trigger; confirm Actions status in repo settings.   Changes a match outcome / number / visible text?: no

### REPO-08 · LOW · debt
What: Large binaries not under LFS. `.gitattributes` LFS-tracks only data/sprites, art, data/audio wav, data/music ogg. Not tracked: docs/field_manual/RTS_2027_Field_Manual.pdf (38.6 MB, 3 commits in history), the pilot PDF (1.7 MB), data/images/stills (11.5 MB; README says deliberately not LFS), data/audio/voice ogg (6 MB), data/ui/skins, data/portraits, data/fonts png. Pack size is 115 MB.
Where: .gitattributes; docs/field_manual/.
Evidence: script — `git ls-files` sizes minus `git lfs ls-files`.
Impact: Every PDF regeneration adds ~38 MB permanently to clones.
Fix idea: `*.pdf` to LFS (needs history migration to help) or stop committing regenerated PDFs.   Changes a match outcome / number / visible text?: no

### REPO-09 · LOW · debt
What: Hard-coded personal/host paths in tooling: `/home/claude/wt/blender.lock`, `/home/claude/ref/{zeroad,wz2100,wz2100-texpages,kenney-particles}`, and D:\ACE-Step-1.5 in docs. All have env overrides in some scripts (`ZEROAD_ROOT`, `WZ_*`, `RTS_KENNEY_PARTICLES`) but not `build_infantry.py:37 LOCK`, `wz_tracks.py:19`, `auth_tank_wz.py:30`, `terrain_sheet.py:7`. No IPs other than doc examples (192.168.1.20 in LAN help text). No secrets found.
Where: tools/art/build_infantry.py:37, tools/art/wz/wz_tracks.py:19, tools/art/vehicles/auth_tank_wz.py:30, tools/art/zeroad/terrain_sheet.py:7.
Evidence: script — grep.
Impact: Contributors cannot run these; no runtime effect.
Fix idea: Read from env with a repo-relative default.   Changes a match outcome / number / visible text?: no

## Suspects (unproven)
- Windows CI actually not running since Nov 12: prove with `gh api repos/pwnzor11/RTS_2027/actions/runs` and Actions settings (workflow file shows no cause).
- tools/art/vehicles/auth_tank_wz.py and structures/auth_factory_wz.py (WZ-derived): confirm no `*_wz` sprite sheets are shipped; none are tracked under data/ (git ls-files grep), but data/sprites/vehicles auth_tank frames should be compared by hash with art/pilot work output.
- CREDITS says SFX sfxr output is "CC0 (ASSUMED, M3-INT: owner to confirm)" and music "CC0 / no rights claimed"; the legal status of AI outputs is an owner call, not code.
- THIRD_PARTY_NOTICES mentions Noto only in CREDITS/credits.toml; OFL.txt is in data/fonts (verified present).

## Checked and fine
- EA/Westwood markers: only AboutContent.cs:15 disclaimer and a Python test string; LICENSING register "(none)" is accurate. Every `.cs` file under src/tests has SPDX header test.
- All 7 test projects and 8 src projects are in RTS_2027.slnx, so `dotnet test` covers every suite; no [Ignore]/[Explicit]; 3335 test methods.
- PROVENANCE.toml: 306 entries = 306 audio files (script), licences match the allow-list test.
- Data licences present for Skorpio, LPC, Kenney, 0 A.D., CC-BY(-SA) 3.0; OpenAL LGPL text and replaceability note shipped; LICENSE/NOTICE/THIRD_PARTY files copied into publish via Game.csproj.
- Installer firewall ports (47270-47271) match NetProtocol.DefaultGamePort/DefaultDiscoveryPort; uninstall removes the rule; ci.yml `publish-game` smoke test runs from an empty folder.
- No path filters or conditions skip CI jobs; nightly release replacement logic is coherent.
