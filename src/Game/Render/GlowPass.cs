// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Render;

/// <summary>One soft light for the additive pass, view only.</summary>
/// <param name="X">Centre X in zoom-1 world pixels.</param>
/// <param name="Y">Centre Y in zoom-1 world pixels.</param>
/// <param name="Radius">Radius in zoom-1 pixels.</param>
/// <param name="Rgb">Colour 0xRRGGBB.</param>
/// <param name="Intensity">0..1 strength.</param>
public readonly record struct GlowSource(float X, float Y, float Radius, uint Rgb, float Intensity);

/// <summary>One short flash (an explosion core or a muzzle flash) that fades over its life.</summary>
/// <param name="Source">The light at full strength.</param>
/// <param name="Born">Sim tick it started.</param>
/// <param name="Life">Ticks it lasts.</param>
public readonly record struct GlowFlash(GlowSource Source, int Born, int Life);

/// <summary>The numbers and shapes of the glow pass. Every number is ASSUMED and the owner's to tune. Pure.</summary>
public static class GlowModel
{
    /// <summary>Edge of the generated glow texture in pixels.</summary>
    public const int TexturePx = 64;

    /// <summary>Most flashes alive at once; the oldest goes first. ASSUMED.</summary>
    public const int MaxFlashes = 48;

    /// <summary>Most lights drawn in one frame (flashes plus burning things); the strongest are kept. ASSUMED.</summary>
    public const int MaxSources = 96;

    /// <summary>Life of an explosion core in ticks (0.5 s). ASSUMED.</summary>
    public const int ExplosionLife = 10;

    /// <summary>Life of a muzzle flash in ticks. ASSUMED.</summary>
    public const int MuzzleLife = 4;

    /// <summary>Radius in zoom-1 pixels of a small explosion's core. ASSUMED.</summary>
    public const float SmallExplosionRadius = 40f;

    /// <summary>Radius of a large explosion's core. ASSUMED.</summary>
    public const float LargeExplosionRadius = 76f;

    /// <summary>Radius of a muzzle flash. ASSUMED.</summary>
    public const float MuzzleRadius = 22f;

    /// <summary>Radius of the glow of a burning vehicle; a burning building uses this times its larger footprint side. ASSUMED.</summary>
    public const float BurnRadius = 34f;

    /// <summary>Colour of explosion cores (warm orange). ASSUMED.</summary>
    public const uint ExplosionRgb = 0xFFA040;

    /// <summary>Colour of muzzle flashes (pale yellow). ASSUMED.</summary>
    public const uint MuzzleRgb = 0xFFE090;

    /// <summary>Colour of fires (deep orange). ASSUMED.</summary>
    public const uint FireRgb = 0xFF7020;

    /// <summary>Brightness 0..1 of the glow at <paramref name="d"/> of the radius (0 = the centre, 1 = the edge): a smooth step down, exactly 0 at the edge so no square shows.</summary>
    public static float Falloff(float d)
    {
        if (d >= 1f) return 0f;
        float t = 1f - Math.Max(0f, d);
        return t * t * (3f - 2f * t);
    }

    /// <summary>The glow texture as RGBA bytes, <see cref="TexturePx"/> square: white, with colour and alpha both set to the falloff, for additive blending.</summary>
    public static byte[] BuildPixels()
    {
        int n = TexturePx;
        var rgba = new byte[n * n * 4];
        float half = n / 2f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                byte v = (byte)Math.Clamp((int)MathF.Round(Falloff(MathF.Sqrt(dx * dx + dy * dy)) * 255f), 0, 255);
                int i = (y * n + x) * 4;
                rgba[i] = rgba[i + 1] = rgba[i + 2] = rgba[i + 3] = v;
            }
        }

        return rgba;
    }

    /// <summary>Strength 0..1 of a flash <paramref name="age"/> ticks old of <paramref name="life"/>: full at birth, down to 0 at the end, fastest at first (a core flares and dies).</summary>
    public static float FlashStrength(int age, int life)
    {
        if (age < 0 || life <= 0 || age >= life) return 0f;
        float t = 1f - age / (float)life;
        return t * t;
    }

    /// <summary>
    /// Flicker factor 0.7..1 of a fire: changes every 3 ticks, from a hash of <paramref name="seed"/> (the thing's id) and the tick; the same inputs give the same
    /// value in every replay. <paramref name="steady"/> (the "Reduce flashes" setting) gives a constant 0.85.
    /// </summary>
    public static float Flicker(uint seed, int tick, bool steady)
    {
        if (steady) return 0.85f;
        uint h = DecorHash.Mix(0x474C_4F57UL, (int)seed, tick / 3, 7u);
        return 0.7f + (h & 0xFF) / 255f * 0.3f;
    }

    /// <summary>The colour of a light as an additive tint: its colour scaled by <paramref name="strength"/>, opaque (the blend adds, so dark means nothing).</summary>
    public static Color Tint(uint rgb, float strength)
    {
        float s = Math.Clamp(strength, 0f, 1f);
        return new Color((int)((rgb >> 16 & 0xFF) * s), (int)((rgb >> 8 & 0xFF) * s), (int)((rgb & 0xFF) * s), (int)(255 * s));
    }
}

/// <summary>The short flashes alive now, with a cap (the oldest goes first). Pure.</summary>
public sealed class GlowField
{
    private readonly List<GlowFlash> _flashes = new();

    /// <summary>The flashes, oldest first.</summary>
    public IReadOnlyList<GlowFlash> Items => _flashes;

    /// <summary>Adds a flash, dropping the oldest when the field is full.</summary>
    public void Add(GlowFlash flash)
    {
        if (_flashes.Count >= GlowModel.MaxFlashes) _flashes.RemoveAt(0);
        _flashes.Add(flash);
    }

    /// <summary>Drops the flashes whose life is over at <paramref name="tick"/> (and those from the future, after a restart).</summary>
    public void Prune(int tick) => _flashes.RemoveAll(f => tick < f.Born || tick - f.Born >= f.Life);

    /// <summary>Forgets everything.</summary>
    public void Clear() => _flashes.Clear();

    /// <summary>Adds the live flashes of <paramref name="tick"/> to <paramref name="into"/>, each at its current strength.</summary>
    public void Collect(int tick, List<GlowSource> into)
    {
        foreach (GlowFlash f in _flashes)
        {
            float k = GlowModel.FlashStrength(tick - f.Born, f.Life);
            if (k > 0f) into.Add(f.Source with { Intensity = f.Source.Intensity * k });
        }
    }
}

/// <summary>
/// The additive glow pass (design 18, "Blend mode"): a soft radial texture, generated once, drawn with <see cref="BlendState.Additive"/> for explosion cores,
/// muzzle flashes and burning vehicles and buildings (the game has no lit windows). It briefly ends the sprite batch the scene draws in and begins an
/// additive one, then puts the normal batch back.
/// </summary>
public sealed class GlowPass : IDisposable
{
    private Texture2D? _texture;

    /// <summary>Draws <paramref name="sources"/> (nothing is drawn, and the batch is not touched, when none is in view).</summary>
    public void Draw(SpriteBatch batch, GraphicsDevice device, IReadOnlyList<GlowSource> sources, Camera camera, Viewport viewport)
    {
        if (sources.Count == 0) return;
        Rectangle view = viewport.Bounds;
        bool any = false;
        foreach (GlowSource s in sources)
        {
            if (Visible(s, camera, view)) { any = true; break; }
        }

        if (!any) return;
        if (_texture is null)
        {
            _texture = new Texture2D(device, GlowModel.TexturePx, GlowModel.TexturePx);
            _texture.SetData(GlowModel.BuildPixels());
        }

        batch.End();
        batch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.LinearClamp);
        foreach (GlowSource s in sources)
        {
            if (!Visible(s, camera, view)) continue;
            Rectangle dest = camera.ScreenRect(s.X - s.Radius, s.Y - s.Radius, s.X + s.Radius, s.Y + s.Radius);
            batch.Draw(_texture, dest, GlowModel.Tint(s.Rgb, s.Intensity));
        }

        batch.End();
        batch.Begin(samplerState: SamplerState.PointClamp);   // the scene's own batch state (RtsGame.Draw)
    }

    private static bool Visible(GlowSource s, Camera camera, Rectangle view)
    {
        if (s.Intensity <= 0.01f) return false;
        return camera.ScreenRect(s.X - s.Radius, s.Y - s.Radius, s.X + s.Radius, s.Y + s.Radius).Intersects(view);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
    }
}
