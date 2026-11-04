// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Sim.Rules;

namespace Rts.Game.Archive;

/// <summary>
/// Draws the rotating preview in the reading pane with the real sprite, or the generated placeholder when the id has no
/// art yet. ASSUMED (M3-17): owns its own <see cref="SpriteLibrary"/> (a second GPU copy of the sheets, loaded when the
/// Archive opens and released when it closes) because the scene renderer does not expose its resolver.
/// </summary>
public sealed class ArchiveSprites : IDisposable
{
    private readonly SpriteLibrary? _library;
    private readonly SpriteResolver _resolver;
    private readonly SpritePainter _painter = new();
    private readonly SimRules? _rules;

    /// <summary>Scans and loads the sprites under <paramref name="spritesDir"/>; a missing or broken folder gives placeholders only.</summary>
    public ArchiveSprites(GraphicsDevice device, string spritesDir, SimRules? rules)
    {
        _rules = rules;
        try
        {
            _library = SpriteLibrary.Scan(spritesDir);
            _library.LoadAll(device);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException)
        {
            _library?.Dispose();
            _library = null;
        }
        _resolver = new SpriteResolver(_library);
    }

    /// <summary>The kind and footprint of a rules id, or null when the rules do not know it.</summary>
    public static (SpriteKind Kind, int W, int H)? Describe(SimRules? rules, string id)
    {
        if (rules is null) return null;
        if (rules.Vehicles.Any(v => v.Id == id)) return (SpriteKind.Vehicle, 1, 1);
        if (rules.Infantry.Any(i => i.Id == id)) return (SpriteKind.Infantry, 1, 1);
        BuildingType? b = rules.Buildings.FirstOrDefault(x => x.Id == id);
        return b is null ? null : (SpriteKind.Building, b.FootprintW, b.FootprintH);
    }

    /// <summary>
    /// Draws the sprite for <paramref name="id"/> centred in <paramref name="box"/>, scaled down to fit (never up past 2x).
    /// Units turn slowly with <paramref name="tick"/>. Returns false when the id has no sprite kind.
    /// </summary>
    public bool Draw(SpriteBatch sb, string id, Rectangle box, int tick, Color house)
    {
        (SpriteKind Kind, int W, int H)? d = Describe(_rules, id);
        if (d is null) return false;
        ResolvedSprite sprite = _resolver.Resolve(d.Value.Kind, id, d.Value.W, d.Value.H);
        SpriteManifest m = sprite.Manifest;
        float zoom = Math.Min(2f, Math.Min(box.Width / (float)m.FrameW, box.Height / (float)m.FrameH));
        int frame = d.Value.Kind == SpriteKind.Building
            ? SpriteStates.BuildingFrame(m, RenderMemory.Settled, 100, 100, false, tick)
            : SpriteStates.UnitFrame(m, d.Value.Kind == SpriteKind.Infantry, false, (byte)(tick / 3 % 256), tick);
        var origin = new Vector2(box.Center.X - m.FrameW * zoom / 2f + m.OriginX * zoom,
            box.Center.Y - m.FrameH * zoom / 2f + m.OriginY * zoom);
        _painter.Draw(sb, sprite, frame, origin, house, zoom);
        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _painter.Dispose();
        _library?.Dispose();
    }
}
