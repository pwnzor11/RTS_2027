// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Game.Render;

/// <summary>
/// Draws one frame in order: terrain, occupancy (F3), then buildings and units in one y-sorted pass
/// (<see cref="DrawOrder"/>), projectiles, fog, path (F4) and range (F5) overlays, health bars above the sprites, then
/// the drag box.
/// </summary>
public sealed class SceneRenderer : IDisposable
{
    private static readonly Color DragColour = Color.White;

    private readonly SpriteResolver _sprites;
    private readonly SpriteLibrary? _library;
    private readonly FxPlayer _fx = new();
    private int _fxTick = -1;
    private readonly SpritePainter _painter = new();
    private readonly RenderMemory _memory = new();
    private readonly List<DrawItem> _items = new();
    private readonly TerrainLook _rustBeltTerrain = new();
    private bool _disposed;

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
        if (map is not null && TerrainPalette.IsRustBelt(map.Theater))
        {
            _rustBeltTerrain.Draw(batch, pixel, map, camera);
        }
        else
        {
            TerrainRenderer.Draw(batch, pixel, snapshot, camera, viewport);
        }

        if (frame.Overlays.Occupancy && frame.Sim is not null)
        {
            OccupancyOverlay.Draw(batch, pixel, frame.Sim, snapshot, camera, viewport);
        }

        DrawSprites(batch, pixel, frame, camera);
        DrawEffects(batch, snapshot, camera);

        ProjectileRenderer.Draw(batch, pixel, frame.Projectiles, camera);
        FogRenderer.Draw(batch, pixel, snapshot, camera, viewport);
        if (frame.Overlays.Paths) PathOverlay.Draw(batch, pixel, frame, camera);
        if (frame.Overlays.Ranges) RangeOverlay.Draw(batch, pixel, frame, camera);
        HealthBars.Draw(batch, pixel, frame, camera, _sprites);
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
                    string kind = e.Kind == FxKind.BuildingDestroyed ? "explosion_large" : "explosion_small";
                    _fx.Spawn(kind, Camera.WorldFromLepton(e.Pos), snapshot.Tick);
                }
            }
        }
        _fx.Draw(batch, _library, camera, snapshot.Tick);
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
                    snapshot.Buildings[item.Index], camera);
            }
            else
            {
                RenderUnit unit = frame.Units[item.Index];
                bool selected = frame.Selection.Contains(UnitKey.Of(unit.View));
                UnitRenderer.Draw(batch, pixel, _sprites, _painter, _memory, snapshot.Tick, frame.Rules, unit,
                    selected, camera);
            }
        }
    }
}
