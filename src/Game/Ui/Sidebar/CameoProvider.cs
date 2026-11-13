// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sprites;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// Cameo pictures: the idle frame 0 of the sprite for a rules id, drawn scaled to fit a button, or nothing (the sidebar then draws
/// a labelled placeholder tile). Sheets are read lazily from <c>data/sprites</c> the first time an id is asked for; a missing or
/// unreadable sheet is remembered as "none" and never retried.
/// </summary>
public sealed class CameoProvider : IDisposable
{
    // Rules id -> sprite id, where the art kept an earlier name.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal) { ["auth_tank"] = "tank_v2", ["exp_canister_skiff"] = "exp_glide_tank" };   // s23 Q-80: shares the Glide Tank art

    private readonly GraphicsDevice _device;
    private readonly SpriteLibrary? _library;
    private readonly Dictionary<string, SpriteSheet?> _sheets = new(StringComparer.Ordinal);

    /// <summary>Scans <paramref name="spritesRoot"/> for manifests; a missing folder just means every cameo is a placeholder.</summary>
    public CameoProvider(GraphicsDevice device, string spritesRoot)
    {
        _device = device;
        try
        {
            _library = SpriteLibrary.ScanTolerant(spritesRoot, Console.Error.WriteLine);   // S18 GAMEREND-02
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _library = null;
        }
    }

    /// <summary>Draws the cameo for <paramref name="rulesId"/> inside <paramref name="area"/>; false if it has no sprite.</summary>
    public bool TryDraw(SpriteBatch batch, string rulesId, Rectangle area, Color house)
    {
        SpriteSheet? sheet = SheetFor(rulesId);
        if (sheet is null) return false;
        int frame;
        try
        {
            frame = sheet.State("idle").First;
        }
        catch (KeyNotFoundException)
        {
            frame = 0;
        }

        float scale = MathF.Min(area.Width / (float)sheet.FrameW, area.Height / (float)sheet.FrameH);
        // Centre the frame on the area; Draw puts the frame's origin pixel at the position given.
        Vector2 centre = new(area.X + area.Width / 2f, area.Y + area.Height / 2f);
        Vector2 offset = (sheet.Origin - new Vector2(sheet.FrameW / 2f, sheet.FrameH / 2f)) * scale;
        sheet.Draw(batch, frame, centre + offset, house, scale);
        return true;
    }

    private SpriteSheet? SheetFor(string rulesId)
    {
        if (_library is null) return null;
        if (_sheets.TryGetValue(rulesId, out SpriteSheet? cached)) return cached;
        string spriteId = Aliases.TryGetValue(rulesId, out string? alias) ? alias : rulesId;
        SpriteSheet? sheet = null;
        if (_library.Ids.Contains(spriteId))
        {
            try
            {
                sheet = SpriteSheet.Load(_device, _library.Manifest(spriteId).SourcePath);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException)
            {
                sheet = null;
            }
        }

        _sheets[rulesId] = sheet;
        return sheet;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (SpriteSheet? sheet in _sheets.Values) sheet?.Dispose();
        _sheets.Clear();
        _library?.Dispose();
    }
}
