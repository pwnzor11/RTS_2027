# Design 21 · M10 polish plan
Design round held with the owner on 2026-11-12, in a session running in parallel with session 19 (no game code changed). Every question below was put
to the owner with options and a lead recommendation, and **all of them are answered**. DECISIONS entries get their numbers at the next wrap-up, after
session 19's, so the two sessions cannot pick the same id. Builds on D-080 (audio sourcing), D-184 (art direction), D-189 (Warzone kitbash),
D-192 (team-colour pass), D-196 (renders on the owner's PC), design 18 (art guidance) and design 16 (maps overhaul, prop art in wave 4 or M10).

## 1. Build order (owner answer)
- **Asset work runs now, in parallel sessions**, as art/data PRs separate from the integration branch: the 26 missing models (M14-06), the building
  depth pipeline, Warzone tracks, prop art, music prompts and SFX. None of it touches `src/`.
- **Code features wait until after M9 LAN** (D-171 order unchanged): weather, water, battle juice, death animations, UI skins, portraits, graphs,
  medals, map previews, loading/credits screens, installer, releases.
- Inside the code work: **look first** (depth re-render, tracks, weather, water, juice, deaths), then UI skin, portraits, menus and meta,
  **shipping last** (installer, releases, update notice).

## 2. Art
| Item | Owner answer | Notes |
|---|---|---|
| 26 ids with no art (design 19 §5, M14-06) | **Build in today's style now**; upgrade later through the shared pipeline | Lead writes Blender scripts in the cloud, pilot first (**Bomber + Airfield**), owner approves the contact sheet, owner renders sprites and Field Manual heroes on his laptop (D-196). Then the other 24, in 3-4 faction batches. Each lands with a `.sprite.toml` and the "PLACEHOLDER ART (M4-ART)" rules comment removed. |
| M10-xx buildings: less flat, more depth | **Shared pipeline upgrade** in `tools/art/build_structure.py` | Bevels, ambient occlusion, edge highlight, roof/wall detail, stronger height cues; respects design 18 §7 (upper-left sun ~40°, hard alpha, 1 px outline, dithered 1-bit shadow). Pilot on 2 buildings (one per base faction) side by side with today's, then a full re-render on the laptop (~60 buildings plus the 10 new ones). |
| M10-06 Warzone kitbash (D-189) | **WZ tracks on every tracked vehicle** (not the lead's "only where thin") | Only GPL-2.0-or-later WZ geometry through `tools/art/wz`; never the CC-BY-SA 3.0 files. Pilot on 2 tracked vehicles (one Authority, one Directorate), then all tracked vehicles; house_audit must still pass. Per-vehicle FX anchors in the manifests while the scripts are open. |
| M10-05 team-colour fixes | **Closed** (D-192 left 0 of 138 flagged) | The 26 new ids and the re-rendered tracked vehicles are audited as they land. |
| M10-07 procedural props (GlassCrest, IceSlab, WreckHulk, BrokenPylon) | Covered by design 16 wave 4 item 3: our own Blender props, about 12 per biome with 2-3 variants | Asset work, can run now; contact sheet per biome. |
| Unit portraits | **Yes** | Close-up renders made alongside the hero renders on the laptop; shown in the selection card. Code part after M9. |
| Death animations | **Yes** | Infantry: fall, flung by blast, burn, radiation; vehicles: wreck flip variants. Frames are asset work (can start now, after the 26 ids); hooking them up is code. |

## 3. Audio
| Item | Owner answer | Notes |
|---|---|---|
| Soundtrack size | **About 20 tracks** (not the lead's 10) | List: menu, credits, victory, defeat; per faction two battle tracks, a tension (calm) track and a briefing theme (16). The LAN lobby uses the menu track. |
| Style | **Mix by faction** (not the lead's single style) | Authority 1960s orchestral (brass, strings, snare); Directorate industrial (machines, low synths, choir hits); Left Behind raw folk and percussion; Expedition naval brass over electronics. Menu, lobby and credits blend all four. |
| Tool | ACE-Step on the owner's PC (D-080); **not set up yet** | Setup guide: docs/audio/ace_step_setup.md (ACE-Step 1.5, now MIT). Prompt pack for all 20 tracks: docs/audio/music_brief.md. A batch script follows once the REST API is checked on the owner's machine. Tracks ship CC0, tool credited in CREDITS. The music player and `data/music` folder already exist. |
| Unit responses | **Piper voice barks later**, together with M8-18 | Short spoken acknowledgements per unit class once Piper runs on the NAS; the current clicks stay until then. |
| SFX | **Per weapon class and faction**, about 60-80 sounds | Every weapon class, per-faction building/death/production sounds, naval/air/tunnel/rig events, ambient biome loops. CC0 packs and our own sfxr output, every file in CREDITS. Wired through the existing `data/audio/sounds.toml` events. |

## 4. Code features after M9 (all picked by the owner)
In-game feel: **weather per biome** (view-only particles, can be turned off), **animated water and shores** (wave cycles, foam, wakes),
**battle juice** (light screen shake on big blasts, hit flash, muzzle flash and smoke), **death animations** (from §2).
Menus and meta: **faction UI skin** (sidebar, buttons, fonts), **unit portraits** in the selection card, **end-of-match graphs** (army,
economy, kills, tech over time from the stats the sim already keeps), **map preview thumbnails** (skirmish setup, LAN lobby, editor),
**medals and records** (local only: campaign par time and no-loss medals, skirmish records, a profile page), **loading screens with art
and tips, end credits roll**. Plus a **final balance pass** (laptop series across factions and tiers) before release.
All of it stays out of the sim: no hash, save or replay change, except that graphs read existing sim stats.

## 5. Shipping
- **Zip and an Inno Setup installer**, both built by CI: Start menu entry, uninstaller, and **a checkbox that adds a private-network firewall
  rule for LAN**. This amends M9 assumption 3 (design 20 §8): zip users still get the Windows prompt and the help page.
- **GitHub Releases with notes, and an update notice**: the game checks once at start (can be turned off in settings) and shows
  "new version available". Matters for LAN, where every player needs the same build. No auto-updater.

## 6. Proposed ROADMAP items (go into ROADMAP when the plan lands)
Assets now: M14-06 (26 ids; pilot running), M10-08 building depth pipeline, M10-06 WZ tracks on all tracked vehicles, M10-07 prop art per
biome, M10-09 music (guide, prompt pack, 20 tracks), M10-10 SFX pass, M10-11 portraits and death frames.
After M9: M10-12 weather, M10-13 water and shores, M10-14 battle juice and death animations, M10-15 faction UI skin, M10-16 portraits in the
card, M10-17 end-of-match graphs, M10-18 map previews, M10-19 medals and records, M10-20 loading and credits screens, M10-21 final balance
pass, M10-22 installer, M10-23 releases and update notice. M10-05 is ticked as closed.

## 7. Owner answers in brief (for the DECISIONS entry)
26 ids current style now; pilot Bomber + Airfield; building depth = shared pipeline; WZ tracks on every tracked vehicle; M10-05 closed;
about 20 music tracks, style mixed by faction, ACE-Step guide needed; Piper barks later with M8-18; SFX per weapon class and faction;
zip plus Inno Setup with an optional firewall rule; GitHub Releases plus update notice; all eleven extras; assets in parallel now, code after
M9, look first. The owner also reported the session 18 build plays well (USER_CHECKS "Session 18").
