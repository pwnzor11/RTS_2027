// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Game.Veterancy;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Render;

/// <summary>
/// Draws one frame in order: terrain, occupancy (F3), M3 ground layers and telegraphs, then buildings and units in one y-sorted pass
/// (<see cref="DrawOrder"/>), projectiles, fog, path (F4) and range (F10) overlays, health bars above the sprites, then
/// the drag box.
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    private static readonly Color DragColour = Color.White;

    /// <summary>F4: draw health bars on everything (the "Health bars: Always" setting); the game screen sets it each frame.</summary>
    public bool HealthBarsAlways { get; set; }

    /// <summary>D-184 pilot: draw infantry 1.25 times larger (the "Larger infantry" setting); the game screen sets it each frame.</summary>
    public bool LargerInfantry
    {
        get => UnitRenderer.InfantryExtraScale > 1f;
        set => UnitRenderer.InfantryExtraScale = value ? UnitRenderer.LargerInfantryScale : 1f;
    }

    /// <summary>M10-12: draw weather particles for the map's biome (the "Weather" setting); the game screen sets it each frame.</summary>
    public bool WeatherEnabled { get; set; } = true;

    /// <summary>M10-14: shake the world view on big blasts (the "Screen shake" setting); the game screen sets it each frame.</summary>
    public bool ShakeEnabled { get; set; } = true;

    /// <summary>QOL-20 (D-313): the "Reduce flashes" setting: no white hit flash on units, no promotion flash, no muzzle sparks. View only; separate from <see cref="ShakeEnabled"/>.</summary>
    public bool ReduceFlashes { get; set; }

    /// <summary>S45 V-04: tracked and wheeled vehicles leave fading track marks on soft ground (the "Tread marks" setting); the game screen sets it each frame.</summary>
    public bool TreadMarksEnabled { get; set; } = true;

    /// <summary>S45 V-04: vehicle and building deaths throw small debris chunks (the "Debris chunks" setting); the game screen sets it each frame.</summary>
    public bool DebrisEnabled { get; set; } = true;

    /// <summary>S45 V-04: one budget for explosions, debris and smoke; when full, the least important (far off screen, small) start last or not at all (the "Effect budget" setting).</summary>
    public bool FxBudgetEnabled { get; set; } = true;

    private readonly Stopwatch _clock = Stopwatch.StartNew();   // presentation time only: weather drift and shake decay, never the sim
    private double _lastSeconds;
    private readonly JuiceShake _shake = new();
    private readonly JuiceHitFlash _hits = new();
    private int _frame;
    private int _juiceTick = int.MinValue;
    private int _prevProjectiles;
    private WeatherKind _weather;
    private Vector2 _homeCamera;   // the camera position before this frame's shake; weather follows this, not the shaken view
    private readonly SpriteResolver _sprites;
    private readonly SpriteLibrary? _library;
    private readonly FxPlayer _fx = new();
    private readonly TreadMarkPool _treads = new();   // S45 V-04
    private readonly TreadMarker _treadMarker = new();
    private readonly DebrisField _debris = new();   // S45 V-04
    private readonly DeathWatcher _debrisDeaths = new();
    private readonly List<DeathRecord> _deathScratch = new();
    private readonly List<DebrisChunk> _chunkScratch = new();
    private readonly FxLook _look = new();   // D-184: damage tiers, remains, dust, mood tint
    private MapData? _moodMap;
    private MoodGrade _mood = MoodGrade.None;
    private int _fxTick = -1;
    private readonly SpritePainter _painter = new();
    private readonly RenderMemory _memory = new();
    private readonly WallTiler _walls = new();
    private readonly List<DrawItem> _items = new();
    private readonly ThemedTerrainLook _terrain = new();
    private readonly WaterOverlay _water = new();   // M10-13: animated water, shore foam, ship wakes
    private readonly DecorLayer _decor = new();   // M12: decals and land-linked sprites on the terrain
    private readonly List<WreckView> _wreckViews = new();      // M6-09: wreck and storm layers
    private readonly List<WreckDraw> _wrecks = new();
    private readonly List<StormView> _stormViews = new();
    private readonly List<StormDraw> _storms = new();
    private Viewport _viewport;   // M12-04: remembered from Draw for the prop culling
    private bool _disposed;
    private readonly HashSet<EntityId> _submerged = new();                    // M7-14: dived ships the viewer can see
    private readonly HashSet<EntityId> _ownOrAllied = new();                  // M7-17: dived ships the viewer owns or is allied with
    private readonly List<ViewActor> _subScratch = new();
    private readonly List<ViewActor> _rankScratch = new();                 // VET-5: veterancy ranks and promotion flashes
    private readonly List<(RankKey Key, int Rank)> _ranks = new();
    private readonly Dictionary<RankKey, int> _rankMap = new();
    private readonly List<RankKey> _promoted = new();
    private readonly PromotionTracker _promotions = new();
    private readonly SceneSpriteExtents _extents;                              // A43 FOLLOWUP-12: view culling reach of each sprite
    private SimRules? _wallRules;                                               // A43 FOLLOWUP-11: read by the cached wall predicate
    private readonly Func<int, bool> _isWallType;
    private Simulation? _cacheSim;                                              // per-tick caches (A43 FOLLOWUP-12): the sim data below only changes with a sim tick
    private int _cacheSimTick = int.MinValue;
    private int _cacheSnapTick = int.MinValue;
    private int _cacheViewer = int.MinValue;
    private bool _wrecksDirty = true, _stormsDirty = true, _submergedDirty = true, _ranksDirty = true;

    /// <summary>
    /// Creates a renderer that draws every vehicle with one sheet (the debug view's tank) and placeholders for the rest.
    /// </summary>
    /// <param name="sheet">Sheet every vehicle is drawn with.</param>
    public SceneRenderer(SpriteSheet sheet)
        : this(new SpriteResolver(null, sheet))
    {
    }

    /// <summary>Creates a renderer that draws library sprites by rules id, with placeholders where a file is missing.</summary>
    /// <param name="library">Scanned sprite library. Its sheets must be loaded before the first draw.</param>
    public SceneRenderer(SpriteLibrary library)
        : this(new SpriteResolver(library))
    {
        _library = library;
    }

    private SceneRenderer(SpriteResolver resolver)
    {
        _sprites = resolver;
        _extents = new SceneSpriteExtents(resolver);
        _isWallType = IsWallType;
    }

    private bool IsWallType(int t) => _wallRules is { } r && (uint)t < (uint)r.Buildings.Count && r.Buildings[t].Wall;

    // True when the sim data cached below is still the data of this snapshot: same sim, same sim tick, same snapshot tick.
    // Once per frame, first thing: marks the caches below stale when the sim, its tick, the snapshot or the viewer changed.
    private void RefreshCaches(SceneFrame frame)
    {
        int simTick = frame.Sim?.Tick ?? int.MinValue;
        if (frame.Sim is not null && ReferenceEquals(_cacheSim, frame.Sim) && _cacheSimTick == simTick
            && _cacheSnapTick == frame.Snapshot.Tick && _cacheViewer == frame.Snapshot.Viewer) return;
        _cacheSim = frame.Sim;
        _cacheSimTick = simTick;
        _cacheSnapTick = frame.Snapshot.Tick;
        _cacheViewer = frame.Snapshot.Viewer;
        _wrecksDirty = _stormsDirty = _submergedDirty = _ranksDirty = true;
    }

    /// <summary>Draws the frame. <paramref name="pixel"/> is a 1x1 white texture; the batch is already begun.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera, Viewport viewport)
    {
        // M10-14: the shake moves only the world drawing (the camera is put back exactly afterwards, so input and the HUD never see it).
        _homeCamera = camera.Position;
        _frame++;
        double now = _clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Clamp(now - _lastSeconds, 0.0, 0.1);
        _lastSeconds = now;
        ObserveJuice(frame, camera, viewport, dt);
        Vector2 shake = ShakeEnabled ? _shake.Offset : Vector2.Zero;
        if (shake != Vector2.Zero) camera.MoveTo(_homeCamera + shake / camera.Zoom);
        try
        {
            DrawWorld(batch, pixel, frame, camera, viewport, now);
        }
        finally
        {
            camera.MoveTo(_homeCamera);
        }
    }

    private void DrawWorld(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera, Viewport viewport, double seconds)
    {
        SimSnapshot snapshot = frame.Snapshot;
        MapData? map = frame.Sim?.Setup.Map;
        _viewport = viewport;
        RefreshCaches(frame);
        if (map is not null)
        {
            _terrain.Draw(batch, pixel, map, camera, frame.Sim?.Map, snapshot.Tick);
            _decor.Draw(batch, pixel, map, camera, viewport);
        }
        else
        {
            TerrainRenderer.Draw(batch, pixel, snapshot, camera, viewport);
        }

        if (frame.Overlays.Occupancy && frame.Sim is not null)
        {
            OccupancyOverlay.Draw(batch, pixel, frame.Sim, snapshot, camera, viewport);
        }

        // M3: ground layers and surfacing telegraphs sit on the terrain, under buildings and units.
        GroundOverlay.DrawBelowUnits(batch, pixel, snapshot, camera, viewport);
        TelegraphRenderer.Draw(batch, pixel, snapshot.Telegraphs, snapshot.Tick, camera, viewport);
        if (frame.Sim is not null) FoamOverlay.Draw(batch, frame.Sim, snapshot.Viewer, camera);
        if (map is not null) _water.Draw(batch, pixel, map, frame, camera, viewport);   // M10-13

        if (!ReferenceEquals(map, _moodMap))
        {
            _moodMap = map;
            _mood = map is null ? MoodGrade.None : TerrainPalette.ForMap(map).Mood;
            _weather = map is null ? WeatherKind.None : WeatherModel.KindFor(TerrainPalette.TheaterIdFor(map, null));   // M10-12
        }

        _look.Observe(snapshot, frame.Rules, _fx, _mood, _sprites);   // D-184
        if (frame.Sim is not null && frame.Rules is not null)   // M6-09: wrecks lie on the ground, under buildings and units
        {
            if (_wrecksDirty)
            {
                WreckStormOverlay.PlanWrecks(frame.Sim.ViewFor(0), frame.Rules.Economy.WreckTicks, _wreckViews, _wrecks);
                _wrecksDirty = false;
            }
            WreckStormOverlay.DrawWrecks(batch, pixel, _wrecks, camera);
        }

        if (TreadMarksEnabled)   // S45 V-04: track marks on the ground, under remains and units
        {
            _treadMarker.Observe(snapshot, frame.Rules, _treads);
            TreadMarker.Draw(batch, pixel, _treads, camera, viewport, snapshot.Tick);
        }

        _look.DrawGround(batch, pixel, camera, viewport, snapshot.Tick, _wrecks);   // D-184: remains and dust, on the ground
        _look.DrawDying(batch, _sprites, _painter, frame.Rules, camera, viewport, snapshot.Tick);   // M10-14b: soldiers playing their death state
        DrawSprites(batch, pixel, frame, camera);
        _look.DrawOverSprites(batch, _library, _sprites, frame, _memory, camera, viewport);   // D-184: damage smoke and fire
        GarrisonOverlay.Draw(batch, snapshot, camera);
        DrawEffects(batch, pixel, frame, camera);

        ProjectileRenderer.Draw(batch, pixel, frame.Projectiles, camera);
        GroundOverlay.DrawBlackout(batch, pixel, snapshot, camera, viewport);
        if (frame.Sim is not null)   // M6-09: ash storms over the sprites, under the fog
        {
            if (_stormsDirty)
            {
                WreckStormOverlay.PlanStorms(frame.Sim.ViewFor(0), snapshot.Tick, snapshot.MapWidth, snapshot.MapHeight, _stormViews, _storms);
                _stormsDirty = false;
            }
            WreckStormOverlay.DrawStorms(batch, pixel, _storms, camera);
        }

        FogRenderer.Draw(batch, pixel, snapshot, camera, viewport);
        FxLook.DrawMood(batch, pixel, _mood, viewport.Bounds);   // D-184: world colour grade (the HUD draws after the scene)
        if (WeatherEnabled && _weather != WeatherKind.None)   // M10-12: above the units, under the overlays and the HUD
            WeatherLayer.Draw(batch, pixel, _weather, viewport.Bounds, seconds, _homeCamera.X * camera.Zoom, _homeCamera.Y * camera.Zoom);
        if (frame.Overlays.Paths) PathOverlay.Draw(batch, pixel, frame, camera);
        if (frame.Overlays.Ranges) RangeOverlay.Draw(batch, pixel, frame, camera);
        SurveyOverlay.Draw(batch, pixel, frame, camera);   // S44 M-14: field richness while a Survey Hover is selected
        // VET-5: ranks come from the viewer's view only (fog-honest); the flash comes from the client-side tracker.
        if (_ranksDirty || frame.Sim is null)   // A43 FOLLOWUP-12: the ranks only change with a sim tick, so they are read once per tick
        {
            if (frame.Sim is not null) RankLookup.Fill(frame.Sim.ViewFor((byte)Math.Max(0, snapshot.Viewer)), _rankScratch, _ranks);
            else _ranks.Clear();
            _rankMap.Clear();
            foreach ((RankKey key, int rank) in _ranks) _rankMap[key] = rank;
            _promotions.Update(snapshot.Tick, _ranks, _promoted);
            _ranksDirty = false;
        }
        if (!ReduceFlashes) PromotionFlash.Draw(batch, pixel, frame.Units, camera, _promotions, snapshot.Tick);   // QOL-20
        HealthBars.Draw(batch, pixel, frame, camera, _sprites, _rankMap, HealthBarsAlways);
        NoPowerOverlay.Draw(batch, pixel, frame, camera);   // D-170: blinking marker over a building stopped for lack of power
        if (frame.DragRect is Rectangle box) Shapes.Outline(batch, pixel, box, DragColour);
    }

    // M10-14: once per sim tick, shake on big blasts, a muzzle flash and smoke puff for each new shell, and the units that were just hit.
    private void ObserveJuice(SceneFrame frame, Camera camera, Viewport viewport, float dt)
    {
        SimSnapshot snapshot = frame.Snapshot;
        _shake.Update(dt);
        _hits.Observe(snapshot.Tick, snapshot.Units, _frame);
        if (snapshot.Tick == _juiceTick) return;
        bool forward = snapshot.Tick > _juiceTick && _juiceTick != int.MinValue;
        _juiceTick = snapshot.Tick;
        int landed = 0;
        if (snapshot.FxEvents is { Count: > 0 } events)
        {
            foreach (FxEvent e in events)
            {
                if (e.Kind == FxKind.Detonation) landed++;
                float amp = JuiceBlast.Amplitude(e);
                if (amp > 0f && ShakeEnabled && forward && InView(Camera.WorldFromLepton(e.Pos), camera, viewport)) _shake.Kick(amp);
            }
        }

        int count = frame.Projectiles.Count;
        int shots = forward ? JuiceMuzzle.NewShots(_prevProjectiles, count, landed) : 0;
        _prevProjectiles = count;
        for (int i = 0; i < shots; i++)
        {
            Vector2 at = Camera.WorldFromLepton(frame.Projectiles[count - 1 - i]);
            if (!InView(at, camera, viewport)) continue;
            if (!ReduceFlashes) _fx.Spawn("sparks", at, snapshot.Tick, 1.1f);   // QOL-20: no muzzle sparks
            _look.Puffs.Add(new Puff(at.X, at.Y - 6, snapshot.Tick, 14, 0x8F8B86, 130, 0.4f, 9, Smoke: true));
        }
    }

    // A world point within one half-view of the screen, so off-screen blasts neither shake the view nor spawn flashes.
    private static bool InView(Vector2 world, Camera camera, Viewport viewport)
    {
        Vector2 s = camera.ScreenFromWorld(world);
        return s.X >= -viewport.Width / 2f && s.X <= viewport.Width * 1.5f && s.Y >= -viewport.Height / 2f && s.Y <= viewport.Height * 1.5f;
    }

    // Explosions (M2-19): the sim's per-tick events of a snapshot are turned into one-shot effects exactly once,
    // however many frames show that snapshot; the player then animates them until they finish.
    private void DrawEffects(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera)
    {
        SimSnapshot snapshot = frame.Snapshot;
        if (snapshot.Tick != _fxTick)
        {
            _fxTick = snapshot.Tick;
            StartEffects(frame, camera);
        }

        if (_library is not null) _fx.Draw(batch, _library, camera, snapshot.Tick);
        if (DebrisEnabled && _debris.Count > 0) DebrisPainter.Draw(batch, pixel, _debris, camera, _viewport, snapshot.Tick);
    }

    /// <summary>One effect waiting for the budget (S45 V-04): an explosion sprite or a death's debris chunks.</summary>
    private readonly record struct PendingFx(FxCandidate Candidate, string? Kind, Vector2 At, float Scale, DebrisChunk[]? Chunks);

    private readonly List<PendingFx> _pending = new();
    private readonly List<FxCandidate> _candidates = new();
    private readonly List<LeptonCoord> _selectedPrev = new();   // where the selected units stood a tick ago, so their deaths are never dropped

    // Once per sim tick: the events and deaths of the snapshot become explosions and debris, exactly once however many frames show it. With the
    // budget on, FxBudget decides which of them start (S45 V-04); with it off, all of them do, as before.
    private void StartEffects(SceneFrame frame, Camera camera)
    {
        SimSnapshot snapshot = frame.Snapshot;
        _pending.Clear();
        if (snapshot.FxEvents is { Count: > 0 } events)
        {
            Rectangle view = _viewport.Bounds;
            float cellPx = Camera.CellPx * camera.Zoom;
            foreach (FxEvent e in events)
            {
                bool guarded = e.Kind == FxKind.Superweapon   // the event does not name its owner, so every superweapon blast is kept
                    || (e.Kind == FxKind.UnitDestroyed && FxBudget.NearAny(e.Pos, _selectedPrev, DeathWatcher.UnitMatchLeptons));
                Vector2 centre = Camera.WorldFromLepton(e.Pos);
                int off = FxBudget.OffScreenCells(view, camera.ScreenFromWorld(centre), cellPx);
                foreach (ExplosionSpot s in ExplosionPlan.For(e))
                {
                    var cand = new FxCandidate(e.Kind == FxKind.Superweapon ? FxClass.Superweapon : FxClass.Explosion, FxBudget.SizeClassOfScale(s.Scale), off, guarded);
                    _pending.Add(new PendingFx(cand, s.Kind, centre + new Vector2(s.DxCells, s.DyCells) * Camera.CellPx, s.Scale, null));
                }
            }
        }

        CollectDebris(frame, camera);   // S45 V-04
        if (_pending.Count > 0)
        {
            bool[] keep;
            if (FxBudgetEnabled)
            {
                _candidates.Clear();
                foreach (PendingFx p in _pending) _candidates.Add(p.Candidate);
                keep = FxBudget.Admit(_candidates, _fx.ActiveCount + _debris.Count + _look.Puffs.Items.Count);
            }
            else
            {
                keep = new bool[_pending.Count];
                Array.Fill(keep, true);
            }

            for (int i = 0; i < _pending.Count; i++)
            {
                if (!keep[i]) continue;
                PendingFx p = _pending[i];
                if (p.Kind is not null) { if (_library is not null) _fx.Spawn(p.Kind, p.At, snapshot.Tick, p.Scale); }
                else foreach (DebrisChunk c in p.Chunks!) _debris.Add(c);
            }
        }

        _selectedPrev.Clear();
        foreach (RenderUnit u in frame.Units)
            if (frame.Selection.Contains(UnitKey.Of(u.View))) _selectedPrev.Add(u.View.Position);
    }

    // S45 V-04: a few chunks for every vehicle or building that just died (walls and soldiers throw none), queued behind the explosions.
    private void CollectDebris(SceneFrame frame, Camera camera)
    {
        SimSnapshot snapshot = frame.Snapshot;
        _debris.Prune(snapshot.Tick);
        if (!DebrisEnabled || frame.Rules is not { } rules) return;
        _debrisDeaths.Observe(snapshot, _deathScratch, rules);
        Rectangle view = _viewport.Bounds;
        foreach (DeathRecord d in _deathScratch)
        {
            int faction;
            bool building = d.Kind == RemainKind.BuildingRubble;
            if (building)
            {
                if ((uint)d.TypeIndex >= (uint)rules.Buildings.Count || rules.Buildings[d.TypeIndex].Wall) continue;
                faction = (int)rules.Buildings[d.TypeIndex].Faction;
            }
            else if (d.Kind == RemainKind.VehicleWreck)
            {
                if ((uint)d.TypeIndex >= (uint)rules.Vehicles.Count) continue;
                faction = (int)rules.Vehicles[d.TypeIndex].Faction;
            }
            else continue;

            Vector2 at = Camera.WorldFromLepton(d.Pos);
            DebrisPlan.For(at.X, at.Y, Math.Max(d.Width, d.Height), building, faction, snapshot.Tick, _chunkScratch);
            int off = FxBudget.OffScreenCells(view, camera.ScreenFromWorld(at), Camera.CellPx * camera.Zoom);
            _pending.Add(new PendingFx(new FxCandidate(FxClass.Debris, building ? 1 : 0, off), null, at, 1f, _chunkScratch.ToArray()));
        }
    }

    /// <summary>
    /// M11-10f: draws the sprite of building type <paramref name="typeIndex"/> at half transparency with its footprint's top-left on
    /// <paramref name="topLeft"/> (the placement ghost). Uses the same sprite lookup and painter as real buildings.
    /// </summary>
    public void DrawBuildingGhost(SpriteBatch batch, Texture2D pixel, SimRules rules, int typeIndex, CellCoord topLeft, Camera camera, int tick, byte player)
    {
        if ((uint)typeIndex >= (uint)rules.Buildings.Count) return;
        BuildingType bt = rules.Buildings[typeIndex];
        var view = new BuildingView(default, player, typeIndex, topLeft, bt.FootprintW, bt.FootprintH, bt.MaxHealth, false);
        ResolvedSprite sprite = _sprites.ForBuilding(rules, view);
        int frame = SpriteStates.BuildingFrame(sprite.Manifest, int.MaxValue / 2, bt.MaxHealth, bt.MaxHealth, false, tick, false);
        _painter.Draw(batch, sprite, frame, SpriteLayout.BuildingOrigin(camera, topLeft), PlayerColours.Base(player), camera.Zoom, 0.5f);
    }

    /// <summary>S18 GAMEREND-01: forgets what the renderer remembered of the previous game (building first-seen ticks, movers). Called when a game screen starts.</summary>
    public void Reset()
    {
        _memory.Reset();
        _treads.Clear();
        _debris.Clear();
        _selectedPrev.Clear();
        _treadMarker.Reset();
        _water.Reset();
        _hits.Reset();
        _shake.Clear();
        _juiceTick = int.MinValue;
        _prevProjectiles = 0;
        _cacheSim = null;
        _wrecksDirty = _stormsDirty = _submergedDirty = _ranksDirty = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _painter.Dispose();
        _look.Dispose();
    }

    // M12-04: blocking props in view join the y-sorted pass, so a unit south of a tree is drawn over it and one north of it under it.
    private void AddBlockingProps(SceneFrame frame, Camera camera)
    {
        MapData? map = frame.Sim?.Setup.Map;
        IReadOnlyList<PropInstance> props = _decor.Blocking;
        if (map is null || props.Count == 0 || _viewport.Width <= 0) return;
        if (!camera.VisibleCells(map.Width, map.Height, _viewport, out int x0, out int y0, out int x1, out int y1)) return;
        for (int i = PropPlanner.FirstAtRow(props, y0 - 1); i < props.Count; i++)
        {
            PropInstance p = props[i];
            if (p.CellY > y1 + 1) break;
            if (p.CellX < x0 - 1 || p.CellX > x1 + 1) continue;
            if (frame.Sim is { } sim && !PropPlanner.StillStands(p, sim.Map)) continue;   // M12-16: destroyed or crushed props vanish at once
            _items.Add(new DrawItem(DrawKind.Prop, i, (p.CellY + 1) * Camera.CellPx, p.CellX * Camera.CellPx));
        }
    }

    private void DrawSprites(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera)
    {
        SimSnapshot snapshot = frame.Snapshot;
        _memory.Observe(snapshot);
        _wallRules = frame.Rules;
        _walls.Observe(snapshot.Tick, snapshot.Buildings, _isWallType);   // cached delegate: no closure per frame
        _extents.Rules = frame.Rules;

        if (_submergedDirty && frame.Sim is not null)   // A43 FOLLOWUP-12: the dived-ship sets only change with a sim tick
        {
            _submerged.Clear();
            _ownOrAllied.Clear();
            _submergedDirty = false;
            PlayerView sees = frame.Sim.ViewFor((byte)Math.Max(0, snapshot.Viewer));
            sees.CopyOwn(_subScratch, includeBuildings: false);
            foreach (ViewActor a in _subScratch) if (a.Submerged) { _submerged.Add(a.Id); _ownOrAllied.Add(a.Id); }
            sees.CopyVisible(_subScratch);   // allies, and enemies found by sonar
            foreach (ViewActor a in _subScratch) if (a.Submerged && a.Kind == ActorKind.Vehicle)
            {
                _submerged.Add(a.Id);
                if (a.Relation == Relation.Ally) _ownOrAllied.Add(a.Id);   // M7-17: only own and allied dived ships are faded
            }
        }

        else if (frame.Sim is null)
        {
            _submerged.Clear();
            _ownOrAllied.Clear();
        }

        _items.Clear();
        SpriteCull.Collect(_items, snapshot.Buildings, frame.Units, _extents, camera, _viewport);   // A43 FOLLOWUP-12: only what can touch the screen
        AddBlockingProps(frame, camera);
        DrawOrder.Sort(_items);

        foreach (DrawItem item in _items)
        {
            if (item.Kind == DrawKind.Prop)
            {
                _decor.DrawBlocking(batch, camera, item.Index);
            }
            else if (item.Kind == DrawKind.Building)
            {
                BuildingRenderer.Draw(batch, pixel, _sprites, _painter, _memory, snapshot.Tick, frame.Rules,
                    snapshot.Buildings[item.Index], camera, _walls);
            }
            else
            {
                RenderUnit unit = frame.Units[item.Index];
                bool selected = frame.Selection.Contains(UnitKey.Of(unit.View));
                UnitRenderer.Draw(batch, pixel, _sprites, _painter, _memory, snapshot.Tick, frame.Rules, unit,
                    selected, camera, unit.View.Kind == UnitKind.Vehicle && _submerged.Contains(unit.View.Id),
                    _ownOrAllied.Contains(unit.View.Id), !ReduceFlashes && _hits.IsFlashing(unit.View.Id, snapshot.Tick, _frame));
            }
        }
    }
}
