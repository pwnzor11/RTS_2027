// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Sprites;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Menu;

/// <summary>
/// Draws a list of <see cref="DrawOp"/>s with SpriteBatch. Scene art is drawn with linear filtering (so the sprites scale smoothly
/// with the window), glows additively, and text with the game's normal point-sampled UI font. The caller's batch must be begun
/// with the default deferred, point-clamp, alpha-blend state; it is begun the same way again when <see cref="Draw"/> returns.
/// </summary>
public sealed class DrawOpRenderer : IDisposable
{
    private enum Mode { None, Scene, Additive, Ui }

    private readonly Texture2D _pixel;
    private readonly MenuSpriteCache _sprites;
    private readonly Texture2D _glow;
    private readonly Texture2D _vignette;
    private readonly Texture2D _grain;
    private Mode _mode;

    /// <summary>Builds the shared light, vignette and grain textures on <paramref name="device"/>.</summary>
    public DrawOpRenderer(GraphicsDevice device, Texture2D pixel, SpriteLibrary? sprites)
    {
        _pixel = pixel;
        _sprites = new MenuSpriteCache(device, sprites);

        const int g = 64;
        var glow = new Color[g * g];
        for (int y = 0; y < g; y++)
        {
            for (int x = 0; x < g; x++)
            {
                float d = MathF.Sqrt(MathF.Pow((x + 0.5f) / g * 2f - 1f, 2) + MathF.Pow((y + 0.5f) / g * 2f - 1f, 2));
                byte v = (byte)(LightMath.GlowFalloff(d) * 255f);
                glow[y * g + x] = new Color(v, v, v, v);
            }
        }
        _glow = new Texture2D(device, g, g);
        _glow.SetData(glow);

        const int vw = 64, vh = 36;
        var vig = new Color[vw * vh];
        for (int y = 0; y < vh; y++)
        {
            for (int x = 0; x < vw; x++)
            {
                byte a = (byte)(LightMath.VignetteDarkness((x + 0.5f) / vw * 2f - 1f, (y + 0.5f) / vh * 2f - 1f) * 255f);
                vig[y * vw + x] = new Color((byte)0, (byte)0, (byte)0, a);
            }
        }
        _vignette = new Texture2D(device, vw, vh);
        _vignette.SetData(vig);

        var grain = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                float v = LightMath.GrainValue(x, y);
                byte a = (byte)(MathF.Abs(v) * 255f);
                byte c = v > 0f ? a : (byte)0;
                grain[y * 128 + x] = new Color(c, c, c, a);
            }
        }
        _grain = new Texture2D(device, 128, 128);
        _grain.SetData(grain);
    }

    /// <summary>Draws the ops in order.</summary>
    public void Draw(SpriteBatch sb, IReadOnlyList<DrawOp> ops, Viewport viewport)
    {
        sb.End();
        _mode = Mode.None;
        foreach (DrawOp op in ops)
        {
            switch (op.Kind)
            {
                case DrawOpKind.Rect: Use(sb, Mode.Scene); Rect(sb, op); break;
                case DrawOpKind.Sprite: Use(sb, Mode.Scene); Sprite(sb, op); break;
                case DrawOpKind.Glow: Use(sb, op.Additive ? Mode.Additive : Mode.Scene); Glow(sb, op); break;
                case DrawOpKind.Vignette:
                    Use(sb, Mode.Scene);
                    sb.Draw(_vignette, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White * op.Amount);
                    break;
                case DrawOpKind.Grain: Use(sb, Mode.Scene); Grain(sb, op, viewport); break;
                case DrawOpKind.Text:
                    Use(sb, Mode.Ui);
                    if (op.Scale > 0f)
                        BitmapFont.Draw(sb, _pixel, op.Id ?? "", new Vector2(MathF.Round(op.X), MathF.Round(op.Y)), op.Colour.Premultiplied(), new Rts.Game.Ui.UiUnit(op.Scale),
                            op.Bold ? FontWeight.Bold : FontWeight.Regular);
                    else
                        BitmapFont.Draw(sb, _pixel, op.Id ?? "", new Vector2(MathF.Round(op.X), MathF.Round(op.Y)), op.Colour.Premultiplied(), op.Frame,
                            op.Bold ? FontWeight.Bold : FontWeight.Regular);
                    break;
            }
        }
        Use(sb, Mode.Ui);
    }

    private void Use(SpriteBatch sb, Mode mode)
    {
        if (mode == _mode) return;
        if (_mode != Mode.None) sb.End();
        _mode = mode;
        switch (mode)
        {
            case Mode.Scene: sb.Begin(samplerState: SamplerState.LinearClamp); break;
            case Mode.Additive: sb.Begin(blendState: BlendState.Additive, samplerState: SamplerState.LinearClamp); break;
            default: sb.Begin(samplerState: SamplerState.PointClamp); break;
        }
    }

    private void Rect(SpriteBatch sb, DrawOp op)
    {
        int x = (int)MathF.Round(op.X), y = (int)MathF.Round(op.Y);
        int w = (int)MathF.Round(op.X + op.W) - x, h = (int)MathF.Round(op.Y + op.H) - y;
        if (w <= 0 || h <= 0) return;
        if (op.Colour == op.Colour2 || h < 8)
        {
            sb.Draw(_pixel, new Rectangle(x, y, w, h), op.Colour.Premultiplied());
            return;
        }
        // A gradient is a stack of bands whose edges are whole pixels, so neighbours never leave a seam or overlap.
        int bands = Math.Clamp(h / 6, 2, 64);
        int prev = y;
        for (int i = 0; i < bands; i++)
        {
            int next = y + (int)MathF.Round(h * (i + 1) / (float)bands);
            if (next > prev)
                sb.Draw(_pixel, new Rectangle(x, prev, w, next - prev), Rgba.Lerp(op.Colour, op.Colour2, (i + 0.5f) / bands).Premultiplied());
            prev = next;
        }
    }

    private void Glow(SpriteBatch sb, DrawOp op)
    {
        if (op.W < 0.5f || op.H < 0.5f) return;
        sb.Draw(_glow, new Rectangle((int)(op.X - op.W), (int)(op.Y - op.H), (int)(op.W * 2), (int)(op.H * 2)), op.Colour.Premultiplied());
    }

    private void Sprite(SpriteBatch sb, DrawOp op)
    {
        if (op.Id is null || !_sprites.TryGet(op.Id, op.Colour, out Texture2D texture, out SpriteManifest m)) return;
        int cols = texture.Width / m.FrameW;
        var source = new Rectangle(op.Frame % cols * m.FrameW, op.Frame / cols * m.FrameH, m.FrameW, m.FrameH);
        // The sheet is premultiplied: brightness scales the colour only, opacity scales all four channels.
        byte k = (byte)(255f * Math.Clamp(op.Amount * op.Alpha, 0f, 1f));
        var tint = new Color(k, k, k, (byte)(255f * Math.Clamp(op.Alpha, 0f, 1f)));
        sb.Draw(texture, new Vector2(op.X, op.Y), source, tint, 0f, new Vector2(m.OriginX, m.OriginY), op.Scale, SpriteEffects.None, 0f);
    }

    private void Grain(SpriteBatch sb, DrawOp op, Viewport viewport)
    {
        const int cell = 2; // each grain texel is 2x2 screen pixels
        (int ox, int oy) = LightMath.GrainOffset(op.Frame);
        int tile = 128 * cell;
        Color tint = Color.White * op.Amount;
        // The tile starts at a different phase each frame; draw enough tiles to cover the window from that start.
        int sx = -ox * cell, sy = -oy * cell;
        for (int y = sy; y < viewport.Height; y += tile)
            for (int x = sx; x < viewport.Width; x += tile)
                sb.Draw(_grain, new Rectangle(x, y, tile, tile), tint);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sprites.Dispose();
        _glow.Dispose();
        _vignette.Dispose();
        _grain.Dispose();
    }
}
