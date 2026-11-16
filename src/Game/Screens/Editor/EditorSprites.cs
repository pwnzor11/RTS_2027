// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Render;
using Rts.Sim.Map;

namespace Rts.Game.Screens.Editor;

/// <summary>
/// The sprites the map editor draws for its map: the decal texture, the land-linked props and the blocking props. It is built with the game
/// view's own code (<see cref="TerrainPalette.ForMap(MapData, MapMeta)"/>, <see cref="MapHasher"/>, <see cref="DecorBake"/>,
/// <see cref="PropPlanner"/>, <see cref="PropAtlases"/>) from the same map, theater and seed that <see cref="DecorLayer"/> uses, so the editor
/// shows the sprites the game draws for that map. It is rebuilt when the map changes, never per frame.
/// </summary>
public sealed class EditorSprites : IDisposable
{
    /// <summary>One sprite the editor draws: the planned prop (cell, offset, kind, variant), its atlas frame, and whether it is a blocking prop.</summary>
    /// <param name="Prop">The planned prop.</param>
    /// <param name="Frame">The atlas frame the prop reads.</param>
    /// <param name="Blocking">True for a blocking prop (a tree, wreck, block, pylon or snag), false for a land-linked decoration.</param>
    public readonly record struct Sprite(PropInstance Prop, PropRect Frame, bool Blocking);

    /// <summary>The pure result of planning a map (no graphics device needed, so tests can check it).</summary>
    /// <param name="Seed">The map hash the planners use.</param>
    /// <param name="Decals">The decal bake, or null when the theater has no decals or none were stamped.</param>
    /// <param name="Atlas">The prop atlas, or null when the map has no sprites.</param>
    /// <param name="Sprites">The sprites in draw order: land-linked first, then blocking props, each sorted by row.</param>
    public sealed record Plan(ulong Seed, DecorBake.Result? Decals, IPropAtlas? Atlas, Sprite[] Sprites);

    private Texture2D? _decals;
    private Texture2D? _atlas;
    private int _width, _height;
    private Sprite[] _sprites = Array.Empty<Sprite>();

    /// <summary>The sprites in draw order (empty before <see cref="Rebuild"/>).</summary>
    public IReadOnlyList<Sprite> Sprites => _sprites;

    /// <summary>Plans a map exactly as <see cref="DecorLayer"/> does for the game view: its own theater palette, seed, decals and sprites.</summary>
    /// <param name="map">The map (for the editor, the document's <c>ToMapData()</c>).</param>
    public static Plan PlanFor(MapData map)
    {
        ArgumentNullException.ThrowIfNull(map);
        TerrainPalette palette = TerrainPalette.ForMap(map);
        ulong seed = MapHasher.Compute(map);
        DecorBake.Result? decals = null;
        if (palette.Decor.Decals.Count > 0)
        {
            DecorBake.Result baked = DecorBake.Bake(map, palette, seed);
            if (baked.Count > 0) decals = baked;
        }

        PropInstance[] props = PropPlanner.Plan(map, palette, seed);
        PropInstance[] blocking = PropPlanner.PlanBlocking(map);
        if (props.Length + blocking.Length == 0) return new Plan(seed, decals, null, Array.Empty<Sprite>());

        IPropAtlas atlas = PropAtlases.Create(palette, TerrainPalette.TheaterIdFor(map, null));
        var sprites = new Sprite[props.Length + blocking.Length];
        for (int i = 0; i < props.Length; i++) sprites[i] = new Sprite(props[i], atlas.Frame(props[i].Kind, props[i].Variant), false);
        for (int i = 0; i < blocking.Length; i++) sprites[props.Length + i] = new Sprite(blocking[i], atlas.Frame(blocking[i].Kind, blocking[i].Variant), true);
        return new Plan(seed, decals, atlas, sprites);
    }

    /// <summary>Plans <paramref name="map"/> and uploads its decal bake and prop atlas to the device, replacing any earlier ones.</summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="map">The map to show.</param>
    public void Rebuild(GraphicsDevice device, MapData map)
    {
        ArgumentNullException.ThrowIfNull(device);
        Plan plan = PlanFor(map);
        DisposeTextures();
        _width = map.Width;
        _height = map.Height;
        if (plan.Decals is { Count: > 0 } decals)
        {
            _decals = new Texture2D(device, decals.Width, decals.Height);
            _decals.SetData(decals.Rgba);
        }

        if (plan.Atlas is { } atlas)
        {
            _atlas = new Texture2D(device, atlas.Width, atlas.Height);
            _atlas.SetData(atlas.Rgba);
        }

        _sprites = plan.Sprites;
    }

    /// <summary>
    /// Draws the decals and the sprites over the map rectangle whose top-left is <paramref name="origin"/>, at <paramref name="cellPx"/> pixels per
    /// cell. Land-linked sprites and decals follow <paramref name="showDecor"/>; blocking props follow <paramref name="showBlocking"/>.
    /// Sprites are scaled from the game's 64 px cell by <c>cellPx / 64</c>, and each frame is anchored on its cell centre as in the game.
    /// </summary>
    /// <param name="batch">The batch, already begun.</param>
    /// <param name="origin">Screen position of the map's top-left cell.</param>
    /// <param name="cellPx">Editor cell size in pixels.</param>
    /// <param name="showDecor">Whether decals and land-linked sprites are drawn.</param>
    /// <param name="showBlocking">Whether blocking props are drawn.</param>
    public void Draw(SpriteBatch batch, Point origin, int cellPx, bool showDecor, bool showBlocking)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (showDecor && _decals is not null)
            batch.Draw(_decals, new Rectangle(origin.X, origin.Y, _width * cellPx, _height * cellPx), Color.White);
        if (_atlas is null) return;

        float scale = cellPx / (float)Camera.CellPx;
        foreach (Sprite s in _sprites)
        {
            if (s.Blocking ? !showBlocking : !showDecor) continue;
            PropInstance p = s.Prop;
            float left = ((p.CellX + 0.5f) * Camera.CellPx + p.OffX - s.Frame.OriginX) * scale;
            float top = ((p.CellY + 0.5f) * Camera.CellPx + p.OffY - s.Frame.OriginY) * scale;
            int l = (int)MathF.Round(left), t = (int)MathF.Round(top);
            int r = (int)MathF.Round(left + s.Frame.Width * scale), b = (int)MathF.Round(top + s.Frame.Height * scale);
            batch.Draw(_atlas, new Rectangle(origin.X + l, origin.Y + t, r - l, b - t), new Rectangle(s.Frame.X, s.Frame.Y, s.Frame.Width, s.Frame.Height), Color.White);
        }
    }

    /// <summary>Releases the textures.</summary>
    public void Dispose()
    {
        DisposeTextures();
        _sprites = Array.Empty<Sprite>();
    }

    private void DisposeTextures()
    {
        _decals?.Dispose();
        _decals = null;
        _atlas?.Dispose();
        _atlas = null;
    }
}
