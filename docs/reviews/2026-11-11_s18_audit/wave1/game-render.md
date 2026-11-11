# game-render: Render, Sprites, Audio, Host, top-level Game — findings
(Saved by the lead from the helper's hand-back; the helper could not write the file itself.)
Scope: code and data reading only, nothing built or run. Platform: MonoGame DesktopGL 3.8.6, WinExe, net10.0 (DX device-lost does not apply).
COUNTS: critical 0 / major 6 / minor 8 / idea 1

## Findings
- GAMEREND-01 major CODE: the shared SceneRenderer is never reset between games. RenderMemory.cs:43-47 keeps _started=true, so in the 2nd+ game of a session every starting building replays its 70-tick build animation. DeathWatcher and DamageTierTracker handle tick regress; RenderMemory does not (no Reset exists, RtsGame.cs:96-103). Fix: add Scene.Reset() called from the GameScreen ctor, or treat Tick<_tick as a restart. Size S.
- GAMEREND-02 major CODE: sprite loading is all-or-nothing. One bad manifest or PNG makes LoadAll throw (RtsGame.cs:91-104, SpriteLibrary.cs:93, SpriteSheet.cs:62-95). The whole library is then replaced by the legacy tank sheet plus placeholders, silently. Other exception types crash startup. Today all 155 manifests pass. Fix: load per sheet, skip and log the bad one, show a menu notice. Size S-M.
- GAMEREND-03 major CODE: the build is WinExe, but 16 Console.Error/Console.WriteLine diagnostics go nowhere on Windows ("audio disabled", "sprite library failed", "replay not recorded", palette warnings). The only log file is the crash report, written next to the exe (Program.cs:89), which fails under a read-only Program Files install. Fix: one small Log class writing to %APPDATA%/RTS_2027/logs, size-capped. Size S.
- GAMEREND-04 major CODE: audio cooldowns run on sim time (AudioDirector _nowMs = tick*50, updated only in Process). Notify (unit select, GameScreen.cs:547,642,661,1181,1195) uses the same frozen clock. While paused (P), only the first select click sounds; later clicks are dropped (cooldown 60 ms, elapsed 0). low_power/low_funds cooldowns also scale with game speed. Fix: wall clock for Notify events. Size S.
- GAMEREND-05 major DESIGN: nothing pauses the sim on focus loss or minimise. Only edge scroll reads IsActive (RtsGame.cs:112, GameScreen.cs:437,447). DesktopGL keeps calling Update, so a single-player game keeps running while alt-tabbed. No decision in the docs covers it.
- GAMEREND-06 major DATA-REF: 26 rules ids have no sprite and draw as placeholders (designed fallback, no crash):
  - Vehicles (10): auth_carrier, auth_seismic_mortar, auth_foam_truck, auth_interceptor, auth_bomber, auth_howitzer, auth_bulwark, dir_ash_sower, dir_delver, dir_great_dirigible.
  - Infantry (4): auth_engineer, auth_marksman, dir_assessor, dir_volunteer.
  - Buildings (12): auth_ration_hall, auth_signals_hq, auth_blockhouse, auth_flak, auth_airfield, auth_atomic_battery, dir_bunk_gallery, dir_redoubt, dir_committee_hall, neu_ruin_tenement, neu_ruin_depot, lb_still_rig.
  - Several are buildings or units the Authority and Directorate campaign needs. No test lists the gap. Add a test with an allow-list. Size S.
- GAMEREND-07 minor CODE: fullscreen uses IsFullScreen with the default HardwareModeSwitch (RtsGame.cs:55-61,131-137). On DesktopGL that is a real mode switch to the 1280x720 back buffer, not the "borderless" that Settings.cs:11 documents. UI scale is then computed for 720p. The four fixed window sizes (SettingsScreen.cs:24) are not clamped to the display, and a dragged window size is never saved.
- GAMEREND-08 minor CODE: replays use paths relative to the working directory (SimSnapshotSource.cs:32-54, ReplayCatalog.cs:29). Settings and saves use %APPDATA%, crash logs use the exe folder. A read-only working directory means "replay not recorded", silently. The replay stream flushes every 64 ticks, so a crash keeps nearly all of it (sound).
- GAMEREND-09 minor CODE (low confidence): MonoGameAudio.PlayVoice never disposes the SoundEffect it creates, and _active is pruned only inside PlaySfx. No voice cap. Possible InstanceLimitReachedException on a long session.
- GAMEREND-10 minor PERF: per-frame allocations and scans: UnitLayerPlan.For allocates a List per unit per frame; UnitInterpolator.Build allocates a Dictionary every frame; FxPlayer uses library.Ids.Contains (linear LINQ scan over ~155 ids) per active effect and per fx anchor per frame; MusicPlayer.Candidates does OrderBy/Where/ToList every frame even with no music files; SceneRenderer re-copies the player view and ranks every frame; HealthBars calls sprites.ForUnit twice per bar; units and buildings are not culled before sorting. (Terrain, decals, props are baked or culled once; remains/puffs/FX are capped.)
- GAMEREND-11 minor PERF: SimHost.StepCore takes a full Snapshot and a new projectile List after every tick, including each of the 8x speed ticks (SimHost.cs:228-250). Under load MonoGame's fixed-step catch-up can spiral toward its 500 ms cap in spectator 8x on heavy maps.
- GAMEREND-12 minor CODE: Settings: a newer-version or unparsable file loads as defaults and the next Save overwrites it; Save is a non-atomic WriteAllText; window_width/height up to 16384 are not checked against the display.
- GAMEREND-13 minor CODE: the VehicleAliases entry auth_tank->tank_v2 (SpriteResolver.cs:21,107) shadows data/sprites/vehicles/auth_tank.sprite.toml. PNGs are byte-identical today, so editing auth_tank.png would change nothing in game.
- GAMEREND-14 minor CODE (low confidence): wrecks and storms are planned from ViewFor(0) regardless of viewer (SceneRenderer.cs:119,133). Vehicle health bars are a fixed 40 px while ships at draw_scale 1.8-2.4x get much larger brackets.
- GAMEREND-15 idea PERF: Update and Draw are both locked to 60 Hz (fixed step plus vsync), so a 144 Hz monitor shows 60. Fog is one SpriteBatch quad per fogged cell per frame (~8k at zoom 0.5 on a 4K window); a per-tick fog texture, as the minimap already does, would be cheaper.

## Open questions
- Does SpriteSheet.Load premultiply twice if MonoGame 3.8.6 FromStream already premultiplies? Needs a one-pixel test.
- Does minimising on Windows (0x0 client size) crash anywhere?
- Is pause-on-focus-loss wanted?
- Is the synchronous terrain and decal bake on the largest maps (128x128 cells x 16 texels) a visible freeze?

## Things checked and found sound
All 155 manifests match their PNGs; sounds.toml references all resolve; fixed-step catch-up capped (250 ms SimHost, 500 ms MonoGame), pause/speed coherent, interpolation correct; one y-sort total order for buildings/units/props; textures baked once per map and disposed; missing audio device falls back to NullAudio; crash report fires once; no music files (known).

## Not covered
Veterancy/*, TerrainPalette/PropArt/PlaceholderArt/RemainsArt internals, TelegraphRenderer, CursorArt, WallTiling frame logic, VoiceDecoder, most of AudioEventDeriver, theater toml contents, fonts, MenuBackground/MenuSpriteCache. No runtime verification.
