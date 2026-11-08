// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Render;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Ui;

/// <summary>
/// The radar minimap: terrain baked once to one texel per cell (the map's theater palette, the same one the terrain bake uses), a fog layer rewritten when
/// the tick changes (from the snapshot's vision, so the player sees only what the game shows), dots for buildings and units in
/// own / allied / enemy colours (the snapshot already hides enemies in fog), and the camera's viewport rectangle. Input handling
/// lives in the sidebar; the transforms are in <see cref="MinimapMath"/>.
/// </summary>
public sealed class Minimap : IDisposable
{
    private static readonly Color OwnColour = new(0x6C, 0xE0, 0x5E);
    private static readonly Color AllyColour = new(0x5E, 0xC8, 0xF0);
    private static readonly Color EnemyColour = new(0xE8, 0x4A, 0x2E);
    private static readonly Color FrameColour = new(0x6A, 0x72, 0x60);
    private static readonly Color ViewColour = Color.White;

    private readonly MapData _map;
    private readonly Simulation? _sim;
    private readonly byte _local;
    private Texture2D? _terrain;
    private Texture2D? _fog;
    private int _fogTick = -1;
    private readonly Color[] _fogPixels;
    private readonly Color[] _jamPixels;
    private Texture2D? _jam;
    private int _jamKey = -1;
    private bool _jamAny;

    /// <summary>Creates the minimap for a map. <paramref name="sim"/> supplies relations (own / allied / enemy); null draws everyone as enemy.</summary>
    public Minimap(MapData map, Simulation? sim, byte localPlayer)
    {
        _map = map;
        _sim = sim;
        _local = localPlayer;
        _fogPixels = new Color[map.Width * map.Height];
        _jamPixels = new Color[map.Width * map.Height];
    }

    /// <summary>Draws the minimap into <paramref name="area"/>.</summary>
    /// <param name="batch">A begun batch.</param>
    /// <param name="pixel">1x1 white texture.</param>
    /// <param name="area">The square the sidebar reserved.</param>
    /// <param name="snapshot">The current snapshot (its vision gives the fog).</param>
    /// <param name="cameraTopLeftWorld">Camera top-left in zoom-1 world pixels.</param>
    /// <param name="viewSizeWorld">Size of the map view in zoom-1 world pixels.</param>
    public void Draw(SpriteBatch batch, Texture2D pixel, Rectangle area, SimSnapshot snapshot, Vector2 cameraTopLeftWorld,
        Vector2 viewSizeWorld)
    {
        Shapes.Fill(batch, pixel, area, new Color(0, 0, 0, 255));
        MinimapMath m = MinimapMath.Fit(area, _map.Width, _map.Height);
        if (m.Image.IsEmpty) return;
        GraphicsDevice device = pixel.GraphicsDevice;
        _terrain ??= BakeTerrain(device);
        batch.Draw(_terrain, m.Image, Color.White);

        foreach (BuildingView b in snapshot.Buildings)
        {
            Color c = ColourFor(b.Player);
            if (b.Ghost) c = Shapes.Alpha(c, 140);
            Vector2 a = m.PointFromCell(new Vector2(b.Cell.X, b.Cell.Y));
            Vector2 z = m.PointFromCell(new Vector2(b.Cell.X + b.FootprintW, b.Cell.Y + b.FootprintH));
            int w = Math.Max(2, (int)MathF.Round(z.X - a.X)), h = Math.Max(2, (int)MathF.Round(z.Y - a.Y));
            batch.Draw(pixel, new Rectangle((int)MathF.Round(a.X), (int)MathF.Round(a.Y), w, h), c);
        }

        foreach (UnitView u in snapshot.Units)
        {
            Vector2 p = m.PointFromCell(new Vector2(u.Position.X / (float)SimConstants.LeptonsPerCell, u.Position.Y / (float)SimConstants.LeptonsPerCell));
            batch.Draw(pixel, new Rectangle((int)MathF.Round(p.X) - 1, (int)MathF.Round(p.Y) - 1, 2, 2), ColourFor(u.Player));
        }

        DrawFog(batch, device, m.Image, snapshot);
        DrawJam(batch, device, m.Image, snapshot);
        Rectangle view = Rectangle.Intersect(m.ViewportRect(cameraTopLeftWorld, viewSizeWorld), m.Image);
        if (!view.IsEmpty) Shapes.Outline(batch, pixel, view, ViewColour);
        Shapes.Outline(batch, pixel, area, FrameColour);
    }

    private Color ColourFor(byte player)
    {
        if (player == _local) return OwnColour;
        if (_sim is not null && _sim.RelationOf(_local, player) == Relation.Ally) return AllyColour;
        return EnemyColour;
    }

    private void DrawFog(SpriteBatch batch, GraphicsDevice device, Rectangle image, SimSnapshot snapshot)
    {
        if (snapshot.Viewer < 0 || snapshot.Vision.Count != _map.Width * _map.Height) return;
        _fog ??= new Texture2D(device, _map.Width, _map.Height);
        if (_fogTick != snapshot.Tick)
        {
            _fogTick = snapshot.Tick;
            for (int i = 0; i < _fogPixels.Length; i++)
            {
                _fogPixels[i] = snapshot.Vision[i] switch
                {
                    0 => new Color(0, 0, 0, 255),
                    1 => new Color(0, 0, 0, 128),
                    _ => Color.Transparent,
                };
            }

            _fog.SetData(_fogPixels);
        }

        batch.Draw(_fog, image, Color.White);
    }

    // M3: Static Broadcast. Jammed cells (per viewer, from the snapshot) are covered with flickering static.
    private void DrawJam(SpriteBatch batch, GraphicsDevice device, Rectangle image, SimSnapshot snapshot)
    {
        if (snapshot.Ground.Count != _map.Width * _map.Height) return;
        int key = snapshot.Tick / MinimapJam.FlickerTicks;
        if (_jamKey != key)
        {
            _jamKey = key;
            _jamAny = MinimapJam.Fill(_jamPixels, snapshot);
            if (_jamAny)
            {
                _jam ??= new Texture2D(device, _map.Width, _map.Height);
                _jam.SetData(_jamPixels);
            }
        }

        if (_jamAny && _jam is not null) batch.Draw(_jam, image, Color.White);
    }

    private Texture2D BakeTerrain(GraphicsDevice device)
    {
        TerrainPalette? palette;
        try
        {
            palette = TerrainPalette.ForMap(_map);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or TypeInitializationException)
        {
            palette = null;
        }

        var pixels = new Color[_map.Width * _map.Height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = TerrainColour(palette, _map.Land[i], _map.Overlay[i], _map.Density[i]);
        var texture = new Texture2D(device, _map.Width, _map.Height);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>The minimap colour of a cell. With a theater palette: the land's base shade, tinted by ashfall or pitchblende.</summary>
    public static Color TerrainColour(TerrainPalette? palette, LandType land, OverlayKind overlay, byte density)
    {
        if (overlay == OverlayKind.Wall) return new Color(0x70, 0x70, 0x68);
        if (OverlayKinds.IsProp(overlay)) return new Color(0x2A, 0x2A, 0x26);   // M12-04: blocking props are dark dots (design 16 open question 1)
        Color baseColour = palette is null ? Fallback(land) : ToColor(palette.ShadesFor(land).Base);
        if (palette is not null)
        {
            // A palette with its own resource colour (S15 looks) shows every resource cell clearly, brighter when richer.
            int d = palette.NormalDensity(density);
            if (overlay == OverlayKind.Ashfall && palette.MinimapAshfall is { } ashMini) return Color.Lerp(baseColour, ToColor(ashMini), 0.55f + d / 255f * 0.45f);
            if (overlay == OverlayKind.Pitchblende && palette.MinimapPitchblende is { } pitchMini) return Color.Lerp(baseColour, ToColor(pitchMini), 0.55f + d / 255f * 0.45f);
            if (overlay == OverlayKind.Ashfall) return Color.Lerp(baseColour, ToColor(palette.AshfallSpeck), density / 255f * 0.6f);
            if (overlay == OverlayKind.Pitchblende) return Color.Lerp(baseColour, ToColor(palette.PitchChunk), density / 255f * 0.8f);
        }
        else
        {
            if (overlay == OverlayKind.Ashfall) return Color.Lerp(baseColour, new Color(0x40, 0x40, 0x40), density / 255f * 0.5f);
            if (overlay == OverlayKind.Pitchblende) return Color.Lerp(baseColour, new Color(0x50, 0x30, 0x70), density / 255f * 0.8f);
        }

        return baseColour;
    }

    private static Color ToColor(TerrainPalette.Rgb c) => new(c.R, c.G, c.B);

    private static Color Fallback(LandType land) => land switch
    {
        LandType.Clear => new Color(0x7A, 0x6E, 0x50),
        LandType.Road => new Color(0x8A, 0x86, 0x7C),
        LandType.Rough => new Color(0x66, 0x5A, 0x40),
        LandType.Ash => new Color(0x58, 0x56, 0x52),
        LandType.Water => new Color(0x30, 0x58, 0x7C),
        LandType.Rock => new Color(0x4A, 0x44, 0x3E),
        _ => new Color(0x36, 0x30, 0x2C),
    };

    /// <inheritdoc />
    public void Dispose()
    {
        _terrain?.Dispose();
        _fog?.Dispose();
        _jam?.Dispose();
    }
}
