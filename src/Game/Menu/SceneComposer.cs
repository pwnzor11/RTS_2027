// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;

namespace Rts.Game.Menu;

/// <summary>
/// Turns a <see cref="SceneDef"/> and a time into <see cref="DrawOp"/>s. Pure: the same scene, time and window size always give
/// the same list. Procedural skylines are generated once per layer and cached.
/// </summary>
public sealed class SceneComposer
{
    private readonly Func<string, SpriteManifest?> _manifestOf;
    private readonly Dictionary<LayerDef, List<Piece>> _skylines = new(ReferenceEqualityComparer.Instance);

    /// <summary>Creates a composer. <paramref name="manifestOf"/> returns a sprite's manifest, or null when the id is unknown.</summary>
    public SceneComposer(Func<string, SpriteManifest?> manifestOf) => _manifestOf = manifestOf;

    /// <summary>The camera position the scene has at <paramref name="seconds"/> (canvas pixels).</summary>
    public static (float X, float Y) Camera(SceneDef scene, float seconds) =>
        (ParallaxMath.Drift(seconds, scene.DriftX, scene.DriftPeriod),
         ParallaxMath.Drift(seconds, scene.DriftY, scene.DriftPeriod * 0.7f, 1.3f));

    /// <summary>Appends the scene's ops to <paramref name="ops"/>.</summary>
    /// <param name="scene">The scene.</param>
    /// <param name="seconds">Time on the scene's own clock.</param>
    /// <param name="viewW">Window width in pixels.</param>
    /// <param name="viewH">Window height in pixels.</param>
    /// <param name="alpha">Overall opacity 0..1 (the cross-fade).</param>
    /// <param name="ops">The list to append to.</param>
    public void Compose(SceneDef scene, float seconds, float viewW, float viewH, float alpha, List<DrawOp> ops)
    {
        if (alpha <= 0.001f) return;
        float k = ParallaxMath.CoverScale(viewW, viewH);
        (float ox, float oy) = ParallaxMath.CoverOrigin(viewW, viewH, k);
        (float camX, float camY) = Camera(scene, seconds);
        foreach (LayerDef layer in scene.Layers)
        {
            var map = new Map(ox + ParallaxMath.LayerOffset(camX, layer.Parallax) * k, oy + ParallaxMath.LayerOffset(camY, layer.Parallax) * k, k, viewW, viewH);
            switch (layer.Kind)
            {
                case LayerKind.Gradient: Gradient(layer, map, alpha, ops); break;
                case LayerKind.Glow: Glow(layer, map, seconds, alpha, ops); break;
                case LayerKind.Skyline: Skyline(layer, map, seconds, alpha, ops); break;
                case LayerKind.Ground: Ground(layer, map, alpha, ops); break;
                case LayerKind.Sprites: Sprites(layer, map, seconds, alpha, ops); break;
                case LayerKind.Particles: Particles(layer, map, seconds, alpha, ops); break;
            }
        }
        if (scene.Grade.A > 0) ops.Add(DrawOp.Rect(0, 0, viewW, viewH, scene.Grade.Fade(alpha)));
    }

    // Canvas -> screen: x' = ox + x*k, with the layer's parallax shift already inside ox/oy.
    private readonly record struct Map(float Ox, float Oy, float K, float ViewW, float ViewH)
    {
        public float X(float v) => Ox + v * K;
        public float Y(float v) => Oy + v * K;
    }

    private static void Gradient(LayerDef l, Map m, float alpha, List<DrawOp> ops)
    {
        float top = l.Y0 <= 0f ? 0f : m.Y(l.Y0);
        float bottom = l.Y1 >= ParallaxMath.RefHeight ? m.ViewH : m.Y(l.Y1);
        if (bottom <= top) return;
        ops.Add(DrawOp.Rect(0, top, m.ViewW, bottom - top, l.Color.Fade(alpha), l.Color2.Fade(alpha)));
    }

    private static void Glow(LayerDef l, Map m, float seconds, float alpha, List<DrawOp> ops)
    {
        float flick = l.Flicker <= 0f ? 1f
            : 1f + l.Flicker * 0.5f * (MathF.Sin(MathF.Tau * l.FlickerHz * seconds + l.Seed) + MathF.Sin(MathF.Tau * l.FlickerHz * 2.7f * seconds + l.Seed * 1.7f)) * 0.5f;
        Rgba c = l.Color.Fade(Math.Clamp(l.Intensity * flick, 0f, 1f) * alpha);
        float r = l.Radius * m.K;
        ops.Add(DrawOp.Glow(m.X(l.X), m.Y(l.Y), r, r, c, l.Additive));
    }

    private static void Ground(LayerDef l, Map m, float alpha, List<DrawOp> ops)
    {
        float baseY = m.Y(l.Y);
        float rough = l.Roughness * m.K;
        ops.Add(DrawOp.Rect(0, baseY + rough, m.ViewW, MathF.Max(0f, m.ViewH - baseY - rough) + 2f, l.Color.Fade(alpha), l.Color2.Fade(alpha)));
        if (rough <= 0.5f) return;
        const float step = 10f;
        for (float sx = 0; sx < m.ViewW; sx += step)
        {
            float v = (sx - m.Ox) / m.K;
            float h = 0.5f + 0.5f * (0.6f * MathF.Sin(v * 0.013f + l.Seed) + 0.4f * MathF.Sin(v * 0.047f + l.Seed * 2.1f));
            float edge = baseY + rough * (1f - h);
            ops.Add(DrawOp.Rect(sx, edge, step + 0.5f, baseY + rough - edge + 1f, l.Color.Fade(alpha)));
        }
    }

    private void Sprites(LayerDef l, Map m, float seconds, float alpha, List<DrawOp> ops)
    {
        foreach (SpriteItem item in l.Items)
        {
            float x = item.Speed == 0f ? item.X : -400f + ParallaxMath.Wrap(item.X + item.Speed * seconds + 400f, item.LoopWidth);
            float sx = m.X(x);
            float sy = m.Y(item.Y);
            float brightness = 1f - item.Shade;
            float a = item.Alpha * alpha;
            SpriteManifest? man = _manifestOf(item.Sprite);
            if (man is null)
            {
                Silhouette(item, m, sx, sy, a, ops);
                continue;
            }

            float s = item.Scale * m.K;
            float w = man.FrameW * s;
            if (sx + w < 0 || sx - w > m.ViewW) continue; // well off screen
            if (item.Shadow)
            {
                float rx = man.FrameW * (item.Anchor == "bottom" ? 0.42f : 0.30f) * s;
                ops.Add(DrawOp.Glow(sx, sy, rx, rx * 0.28f, new Rgba(0, 0, 0, (byte)(150 * a)), additive: false));
            }

            int tick = (int)(seconds * item.TicksPerSecond);
            int frame;
            try { frame = man.FrameIndex(item.State, item.Facing, Math.Max(0, tick)); }
            catch (KeyNotFoundException) { frame = 0; }

            float px = sx;
            float py = sy;
            if (item.Anchor == "bottom")
            {
                px += (man.OriginX - man.FrameW / 2f) * s;
                py += (man.OriginY - man.FrameH) * s;
            }
            ops.Add(DrawOp.Sprite(item.Sprite, frame, px, py, s, PlayerColours.Choice(item.House).ToRgba(), brightness, a));
        }
    }

    // A dark vehicle/building-like block standing in for art that does not exist yet.
    private static void Silhouette(SpriteItem item, Map m, float sx, float sy, float a, List<DrawOp> ops)
    {
        float s = m.K * item.Scale / 2f;
        float w = item.FallbackWidth * s;
        float h = item.FallbackHeight * s;
        float dim = 1f - item.Shade;
        var body = new Rgba((byte)(item.FallbackColour.R * dim), (byte)(item.FallbackColour.G * dim), (byte)(item.FallbackColour.B * dim));
        var rim = new Rgba((byte)Math.Min(255, body.R + 46), (byte)Math.Min(255, body.G + 40), (byte)Math.Min(255, body.B + 30));
        if (item.FallbackShape == "blimp")
        {
            // An airship in the smog: a soft dark ellipse with a gondola, anchored by its centre.
            ops.Add(DrawOp.Glow(sx, sy, w * 0.62f, h * 0.7f, body.Fade(a), additive: false));
            ops.Add(DrawOp.Glow(sx, sy, w * 0.5f, h * 0.5f, body.Fade(a), additive: false));
            ops.Add(DrawOp.Rect(sx - w * 0.06f, sy + h * 0.38f, w * 0.12f, h * 0.12f, body.Fade(a)));
            return;
        }
        if (item.Shadow) ops.Add(DrawOp.Glow(sx, sy, w * 0.6f, w * 0.14f, new Rgba(0, 0, 0, (byte)(150 * a)), additive: false));
        ops.Add(DrawOp.Rect(sx - w / 2, sy - h * 0.62f, w, h * 0.62f, body.Fade(a)));
        ops.Add(DrawOp.Rect(sx - w * 0.3f, sy - h, w * 0.6f, h * 0.4f, body.Fade(a)));
        ops.Add(DrawOp.Rect(sx - w * 0.3f, sy - h, w * 0.6f, MathF.Max(1f, m.K * 2f), rim.Fade(a)));
        ops.Add(DrawOp.Rect(sx - w / 2, sy - h * 0.62f, w, MathF.Max(1f, m.K * 2f), rim.Fade(a)));
        ops.Add(DrawOp.Rect(sx - w / 2, sy - h * 0.14f, w, h * 0.14f, new Rgba(8, 8, 8).Fade(a)));
    }

    private void Particles(LayerDef l, Map m, float seconds, float alpha, List<DrawOp> ops)
    {
        const float margin = 320f;
        float spanX = ParallaxMath.RefWidth + 2 * margin;
        float spanY = ParallaxMath.RefHeight + 2 * margin;
        int count = (int)(l.Count * (spanX * spanY) / (ParallaxMath.RefWidth * ParallaxMath.RefHeight));
        for (int i = 0; i < count; i++)
        {
            var r = new SceneRng(l.Seed * 1009 + i);
            float x0 = r.Next(), y0 = r.Next(), size = r.Range(l.SizeMin, l.SizeMax), phase = r.Range(0f, MathF.Tau), speed = r.Range(0.6f, 1.4f);
            float vx = l.VelX * speed, vy = l.VelY * speed;
            float x = -margin + ParallaxMath.Wrap(x0 * spanX + vx * seconds + l.Sway * MathF.Sin(seconds * 0.5f * speed + phase), spanX);
            float y = -margin + ParallaxMath.Wrap(y0 * spanY + vy * seconds, spanY);
            float sx = m.X(x), sy = m.Y(y);
            if (sx < -20 || sx > m.ViewW + 20 || sy < -20 || sy > m.ViewH + 20) continue;
            float sz = size * m.K;
            switch (l.Particle)
            {
                case ParticleKind.Ember:
                {
                    float flick = 0.55f + 0.45f * MathF.Sin(seconds * 6f * speed + phase);
                    // Fade out as it rises towards the top of its life.
                    float life = ParallaxMath.Frac((y + margin) / spanY);
                    float fadeLife = vy < 0f ? life : 1f - life;
                    Rgba c = l.Color.Fade(flick * Math.Clamp(fadeLife * 2f, 0f, 1f) * alpha);
                    ops.Add(DrawOp.Glow(sx, sy, sz * 3f, sz * 3f, c, additive: true));
                    ops.Add(DrawOp.Rect(sx - sz / 2, sy - sz / 2, sz, sz, (l.Rim.A > 0 ? l.Rim : l.Color).Fade(flick * alpha)));
                    break;
                }
                default:
                {
                    float soft = size > l.SizeMax * 0.8f ? 1f : 0f; // the biggest flakes are out of focus
                    float a = (0.45f + 0.55f * ((phase / MathF.Tau))) * alpha;
                    if (soft > 0f) ops.Add(DrawOp.Glow(sx, sy, sz * 2.2f, sz * 2.2f, l.Color.Fade(a * 0.55f), additive: false));
                    else ops.Add(DrawOp.Rect(sx, sy, sz, sz, l.Color.Fade(a)));
                    break;
                }
            }
        }
    }

    // ---- procedural skylines ----

    private enum PieceKind { Body, Window, Rim, Thin, Flare }

    // A rectangle relative to the strip: x from the strip start, y up from the base line (so top = Up + Height).
    private readonly record struct Piece(PieceKind Kind, float X, float W, float Up, float H);

    private void Skyline(LayerDef l, Map m, float seconds, float alpha, List<DrawOp> ops)
    {
        if (!_skylines.TryGetValue(l, out List<Piece>? pieces)) _skylines[l] = pieces = Generate(l);
        float periodPx = l.Period * m.K;
        float baseY = m.Y(l.Y);
        float start = m.Ox;
        int first = (int)MathF.Floor((0f - start) / periodPx) - 1;
        int last = (int)MathF.Ceiling((m.ViewW - start) / periodPx) + 1;
        Rgba body = l.Color.Fade(alpha), body2 = l.Color2.Fade(alpha);
        float down = 700f * m.K;
        for (int n = first; n <= last; n++)
        {
            float x0 = start + n * periodPx;
            foreach (Piece p in pieces)
            {
                float sx = x0 + p.X * m.K, w = p.W * m.K;
                if (sx > m.ViewW || sx + w < 0) continue;
                float top = baseY - (p.Up + p.H) * m.K, h = p.H * m.K;
                switch (p.Kind)
                {
                    case PieceKind.Body:
                        ops.Add(DrawOp.Rect(sx, top, w, h + (p.Up <= 0.01f ? down : 0f), body, body2));
                        break;
                    case PieceKind.Thin:
                        ops.Add(DrawOp.Rect(sx, top, w, h, body));
                        break;
                    case PieceKind.Rim:
                        if (l.Rim.A > 0) ops.Add(DrawOp.Rect(sx, top, w, MathF.Max(1f, h), l.Rim.Fade(alpha)));
                        break;
                    case PieceKind.Window:
                        if (l.Lit.A > 0) ops.Add(DrawOp.Rect(sx, top, w, h, l.Lit.Fade(alpha)));
                        break;
                    case PieceKind.Flare:
                        if (l.Lit.A > 0)
                        {
                            float flick = 0.8f + 0.2f * MathF.Sin(seconds * 3.1f + p.X * 0.013f);
                            float r = p.W * m.K;
                            ops.Add(DrawOp.Glow(sx, top, r, r, l.Lit.Fade(flick * alpha), additive: true));
                        }
                        break;
                }
            }
        }
    }

    private static List<Piece> Generate(LayerDef l)
    {
        var r = new SceneRng(l.Seed);
        var p = new List<Piece>();
        switch (l.Style)
        {
            case SkylineStyle.Ruins: Ruins(l, ref r, p); break;
            case SkylineStyle.Stacks: Stacks(l, ref r, p); break;
            case SkylineStyle.Ridge: Ridge(l, ref r, p); break;
            default: Pylons(l, ref r, p); break;
        }
        return p;
    }

    private static void Ruins(LayerDef l, ref SceneRng r, List<Piece> p)
    {
        float x = r.Range(0f, 40f);
        while (x < l.Period - 30f)
        {
            float w = MathF.Min(r.Range(70f, 190f), l.Period - x);
            float h = r.Range(l.MinHeight, l.MaxHeight);
            int slices = w > 110f ? 3 : 2;
            float sw = w / slices;
            float tallest = 0f, tallestX = x;
            for (int s = 0; s < slices; s++)
            {
                // Broken tops: each slice is a different fraction of the tower's height.
                float sh = h * (s == 0 ? r.Range(0.85f, 1f) : r.Range(0.45f, 1f));
                float sx = x + s * sw;
                p.Add(new Piece(PieceKind.Body, sx, sw + 0.5f, 0f, sh));
                p.Add(new Piece(PieceKind.Rim, sx, sw + 0.5f, sh - 3f, 3f));
                for (float wy = 22f; wy < sh - 20f; wy += 30f)
                    for (float wx = 8f; wx < sw - 14f; wx += 20f)
                        if (r.Next() < 0.07f) p.Add(new Piece(PieceKind.Window, sx + wx, 7f, wy, 10f));
                if (sh > tallest) { tallest = sh; tallestX = sx + sw * 0.5f; }
            }
            if (r.Next() < 0.35f) p.Add(new Piece(PieceKind.Thin, tallestX, 3f, tallest, r.Range(30f, 90f)));
            x += w + r.Range(-20f, 40f);
        }
    }

    private static void Stacks(LayerDef l, ref SceneRng r, List<Piece> p)
    {
        float x = r.Range(0f, 60f);
        while (x < l.Period - 60f)
        {
            float w = MathF.Min(r.Range(200f, 400f), l.Period - x);
            float hallH = r.Range(l.MinHeight, l.MinHeight + (l.MaxHeight - l.MinHeight) * 0.3f);
            p.Add(new Piece(PieceKind.Body, x, w, 0f, hallH));
            p.Add(new Piece(PieceKind.Rim, x, w, hallH - 3f, 3f));
            // A row of long furnace-light slits along the hall.
            for (float wx = 14f; wx < w - 30f; wx += 46f)
                if (r.Next() < 0.6f) p.Add(new Piece(PieceKind.Window, x + wx, 28f, hallH * 0.35f, 6f));
            int stacks = 1 + (int)(r.Next() * 3f);
            for (int s = 0; s < stacks; s++)
            {
                float cw = r.Range(26f, 44f);
                float cx = x + r.Range(10f, MathF.Max(12f, w - cw - 10f));
                float ch = r.Range(l.MaxHeight * 0.55f, l.MaxHeight);
                p.Add(new Piece(PieceKind.Body, cx, cw, hallH - 4f, ch));
                p.Add(new Piece(PieceKind.Body, cx - 5f, cw + 10f, hallH + ch - 12f, 12f)); // flared lip
                p.Add(new Piece(PieceKind.Rim, cx - 5f, cw + 10f, hallH + ch - 2f, 2f));
                p.Add(new Piece(PieceKind.Rim, cx, cw, hallH + ch * 0.5f, 3f)); // band
                p.Add(new Piece(PieceKind.Flare, cx + cw / 2, 120f + cw * 2, hallH + ch + 4f, 0f));
            }
            x += w + r.Range(10f, 90f);
        }
    }

    private static void Ridge(LayerDef l, ref SceneRng r, List<Piece> p)
    {
        // Sines with whole-number cycles per period, so the strip tiles without a seam.
        int n1 = 2 + (int)(r.Next() * 2), n2 = 5 + (int)(r.Next() * 3), n3 = 11 + (int)(r.Next() * 5);
        float p1 = r.Range(0f, MathF.Tau), p2 = r.Range(0f, MathF.Tau), p3 = r.Range(0f, MathF.Tau);
        const float step = 8f;
        for (float x = 0f; x < l.Period; x += step)
        {
            float u = x / l.Period * MathF.Tau;
            float f = 0.5f + 0.5f * (0.6f * MathF.Sin(n1 * u + p1) + 0.28f * MathF.Sin(n2 * u + p2) + 0.12f * MathF.Sin(n3 * u + p3));
            float h = l.MinHeight + (l.MaxHeight - l.MinHeight) * Math.Clamp(f, 0f, 1f);
            p.Add(new Piece(PieceKind.Body, x, step + 0.5f, 0f, h));
        }
    }

    private static void Pylons(LayerDef l, ref SceneRng r, List<Piece> p)
    {
        float x = r.Range(60f, 300f);
        while (x < l.Period - 120f)
        {
            float h = r.Range(l.MaxHeight * 0.7f, l.MaxHeight);
            float pw = r.Range(9f, 13f);
            p.Add(new Piece(PieceKind.Body, x, pw, 0f, h));
            p.Add(new Piece(PieceKind.Body, x - 58f, 130f + pw, h - 40f, 8f));
            p.Add(new Piece(PieceKind.Body, x - 38f, 90f + pw, h - 130f, 7f));
            p.Add(new Piece(PieceKind.Thin, x - 56f, 5f, h - 62f, 22f));
            p.Add(new Piece(PieceKind.Thin, x + pw + 50f, 5f, h - 62f, 22f));
            x += r.Range(700f, 1150f);
        }
    }
}
