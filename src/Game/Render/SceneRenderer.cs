// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Game.Veterancy;
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

    private readonly SpriteResolver _sprites;
    private readonly SpriteLibrary? _library;
    private readonly FxPlayer _fx = new();
    private int _fxTick = -1;
    private readonly SpritePainter _painter = new();
    private readonly RenderMemory _memory = new();
    private readonly WallTiler _walls = new();
    private readonly List<DrawItem> _items = new();
    private readonly ThemedTerrainLook _terrain = new();
    private readonly List<WreckView> _wreckViews = new();      // M6-09: wreck and storm layers
    private readonly List<WreckDraw> _wrecks = new();
    private readonly List<StormView> _stormViews = new();
    private readonly List<StormDraw> _storms = new();
    private bool _disposed;
    private readonly HashSet<EntityId> _submerged = new();                    // M7-14: dived ships the viewer can see
    private readonly HashSet<EntityId> _ownOrAllied = new();                  // M7-17: dived ships the viewer owns or is allied with
    private readonly List<ViewActor> _subScratch = new();
    private readonly List<ViewActor> _rankScratch = new();                 // VET-5: veterancy ranks and promotion flashes
    private readonly List<(RankKey Key, int Rank)> _ranks = new();
    private readonly Dictionary<RankKey, int> _rankMap = new();
    private readonly List<RankKey> _promoted = new();
    private readonly PromotionTracker _promotions = new();

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
    }

    /// <summary>Draws the frame. <paramref name="pixel"/> is a 1x1 white texture; the batch is already begun.</summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera, Viewport viewport)
    {
        SimSnapshot snapshot = frame.Snapshot;
        MapData? map = frame.Sim?.Setup.Map;
        if (map is not null)
        {
            _terrain.Draw(batch, pixel, map, camera);
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

        if (frame.Sim is not null && frame.Rules is not null)   // M6-09: wrecks lie on the ground, under buildings and units
        {
            WreckStormOverlay.PlanWrecks(frame.Sim.ViewFor(0), frame.Rules.Economy.WreckTicks, _wreckViews, _wrecks);
            WreckStormOverlay.DrawWrecks(batch, pixel, _wrecks, camera);
        }

        DrawSprites(batch, pixel, frame, camera);
        GarrisonOverlay.Draw(batch, snapshot, camera);
        DrawEffects(batch, snapshot, camera);

        ProjectileRenderer.Draw(batch, pixel, frame.Projectiles, camera);
        GroundOverlay.DrawBlackout(batch, pixel, snapshot, camera, viewport);
        if (frame.Sim is not null)   // M6-09: ash storms over the sprites, under the fog
        {
            WreckStormOverlay.PlanStorms(frame.Sim.ViewFor(0), snapshot.Tick, snapshot.MapWidth, snapshot.MapHeight, _stormViews, _storms);
            WreckStormOverlay.DrawStorms(batch, pixel, _storms, camera);
        }

        FogRenderer.Draw(batch, pixel, snapshot, camera, viewport);
        if (frame.Overlays.Paths) PathOverlay.Draw(batch, pixel, frame, camera);
        if (frame.Overlays.Ranges) RangeOverlay.Draw(batch, pixel, frame, camera);
        // VET-5: ranks come from the viewer's view only (fog-honest); the flash comes from the client-side tracker.
        if (frame.Sim is not null) RankLookup.Fill(frame.Sim.ViewFor((byte)Math.Max(0, snapshot.Viewer)), _rankScratch, _ranks);
        else _ranks.Clear();
        _rankMap.Clear();
        foreach ((RankKey key, int rank) in _ranks) _rankMap[key] = rank;
        _promotions.Update(snapshot.Tick, _ranks, _promoted);
        PromotionFlash.Draw(batch, pixel, frame.Units, camera, _promotions, snapshot.Tick);
        HealthBars.Draw(batch, pixel, frame, camera, _sprites, _rankMap, HealthBarsAlways);
        NoPowerOverlay.Draw(batch, pixel, frame, camera);   // D-170: blinking marker over a building stopped for lack of power
        if (frame.DragRect is Rectangle box) Shapes.Outline(batch, pixel, box, DragColour);
    }

    // Explosions (M2-19): the sim's per-tick events of a snapshot are turned into one-shot effects exactly once,
    // however many frames show that snapshot; the player then animates them until they finish.
    private void DrawEffects(SpriteBatch batch, SimSnapshot snapshot, Camera camera)
    {
        if (_library is null) return;
        if (snapshot.Tick != _fxTick)
        {
            _fxTick = snapshot.Tick;
            if (snapshot.FxEvents is { Count: > 0 } events)
            {
                foreach (FxEvent e in events)
                {
                    Vector2 centre = Camera.WorldFromLepton(e.Pos);
                    foreach (ExplosionSpot s in ExplosionPlan.For(e))
                        _fx.Spawn(s.Kind, centre + new Vector2(s.DxCells, s.DyCells) * Camera.CellPx, snapshot.Tick, s.Scale);
                }
            }
        }
        _fx.Draw(batch, _library, camera, snapshot.Tick);
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

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _painter.Dispose();
    }

    private void DrawSprites(SpriteBatch batch, Texture2D pixel, SceneFrame frame, Camera camera)
    {
        SimSnapshot snapshot = frame.Snapshot;
        _memory.Observe(snapshot);
        _walls.Observe(snapshot.Tick, snapshot.Buildings,
            t => frame.Rules is { } r && (uint)t < (uint)r.Buildings.Count && r.Buildings[t].Wall);

        _submerged.Clear();
        _ownOrAllied.Clear();
        if (frame.Sim is not null)
        {
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

        _items.Clear();
        for (int i = 0; i < snapshot.Buildings.Count; i++)
        {
            BuildingView b = snapshot.Buildings[i];
            float bottom = (b.Cell.Y + b.FootprintH) * Camera.CellPx;
            _items.Add(new DrawItem(DrawKind.Building, i, bottom, b.Cell.X * Camera.CellPx));
        }
        for (int i = 0; i < frame.Units.Count; i++)
        {
            RenderUnit unit = frame.Units[i];
            _items.Add(new DrawItem(DrawKind.Unit, i, unit.World.Y, unit.World.X));
        }
        _items.Sort(DrawOrder.Instance);

        foreach (DrawItem item in _items)
        {
            if (item.Kind == DrawKind.Building)
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
                    _ownOrAllied.Contains(unit.View.Id));
            }
        }
    }
}
