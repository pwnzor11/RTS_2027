// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Bakes a map's decals (craters, stains, cracks, snow patches, ripples, ...) into one transparent texture the size of the terrain bake,
/// once per map (M12-01). Pure integer maths, no GPU types. Which decal sits where comes from <see cref="DecorHash"/> of (map hash, cell),
/// so nothing is stored in the map file and the sim never sees it.
/// </summary>
/// <remarks>
/// Algorithm: for every cell without an overlay and not within three cells of a start, one roll (hash salt 1, 0..999) walks the rules
/// that allow the cell's land type in file order; the first whose cumulative <see cref="DecalRule.Permille"/> exceeds the roll is stamped,
/// centred in the cell with a jitter of up to a quarter cell. Each texel of a stamp is kept only when its own cell is a land type the rule
/// allows and has no overlay, so a decal never spills onto water, cliffs or resource fields. Stamps composite in cell order (premultiplied
/// "over").
/// </remarks>
public static class DecorBake
{
    /// <summary>A baked decal layer.</summary>
    /// <param name="Width">Texels across (map width times TexelCells), equal to the terrain bake.</param>
    /// <param name="Height">Texels down.</param>
    /// <param name="Rgba">Premultiplied RGBA bytes, row-major; alpha 0 where there is no decal.</param>
    /// <param name="Count">Decals stamped.</param>
    public readonly record struct Result(int Width, int Height, byte[] Rgba, int Count);

    private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

    /// <summary>Bakes the decals of <paramref name="map"/> with the table of <paramref name="palette"/>.</summary>
    /// <param name="map">The map.</param>
    /// <param name="palette">Gives the decal table and the texel size.</param>
    /// <param name="seed">The map hash (<see cref="MapHasher.Compute"/>).</param>
    public static Result Bake(MapData map, TerrainPalette palette, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(palette);
        int s = palette.TexelCells, w = map.Width, h = map.Height;
        int tw = w * s, th = h * s;
        var rgba = new byte[tw * th * 4];
        IReadOnlyList<DecalRule> rules = palette.Decor.Decals;
        if (rules.Count == 0) return new Result(tw, th, rgba, 0);

        bool[] nearStart = new bool[w * h];
        foreach (CellCoord start in map.StartPositions)
        {
            for (int y = Math.Max(0, start.Y - 3); y <= Math.Min(h - 1, start.Y + 3); y++)
                for (int x = Math.Max(0, start.X - 3); x <= Math.Min(w - 1, start.X + 3); x++) nearStart[y * w + x] = true;
        }

        var canvas = new Canvas(map, s, tw, th, rgba);
        int count = 0;
        for (int cy = 0; cy < h; cy++)
        {
            for (int cx = 0; cx < w; cx++)
            {
                int k = cy * w + cx;
                if (map.Overlay[k] != OverlayKind.None || nearStart[k]) continue;
                LandType land = map.Land[k];
                int roll = (int)(DecorHash.Mix(seed, cx, cy, 1) % 1000u), acc = 0;
                DecalRule? pick = null;
                foreach (DecalRule rule in rules)
                {
                    if (!rule.Allows(land)) continue;
                    acc += rule.Permille;
                    if (roll < acc) { pick = rule; break; }
                }

                if (pick is null) continue;
                int lo = Math.Max(2, pick.SizeMin * s / 16), hi = Math.Max(lo, pick.SizeMax * s / 16);
                int size = lo + (int)(DecorHash.Mix(seed, cx, cy, 2) % (uint)(hi - lo + 1));
                int jitter = Math.Max(1, s / 4);
                int px = cx * s + s / 2 + (int)(DecorHash.Mix(seed, cx, cy, 3) % (uint)(2 * jitter + 1)) - jitter;
                int py = cy * s + s / 2 + (int)(DecorHash.Mix(seed, cx, cy, 4) % (uint)(2 * jitter + 1)) - jitter;
                if (canvas.Stamp(pick, px, py, size, seed, cx, cy)) count++;
            }
        }

        return new Result(tw, th, rgba, count);
    }

    private sealed class Canvas
    {
        private readonly MapData _map;
        private readonly int _s, _tw, _th;
        private readonly byte[] _rgba;
        private DecalRule _rule = null!;

        public Canvas(MapData map, int s, int tw, int th, byte[] rgba)
        {
            _map = map;
            _s = s;
            _tw = tw;
            _th = th;
            _rgba = rgba;
        }

        public bool Stamp(DecalRule rule, int px, int py, int size, ulong seed, int cx, int cy)
        {
            _rule = rule;
            TerrainPalette.Rgb col = rule.Colour;
            int r = Math.Max(1, size / 2);
            // A disc or crater that would be cut by other ground (a river bank, a cliff, a resource field) is not drawn at all: half a crater looks wrong.
            if (rule.Shape is DecalShape.Blob or DecalShape.Ring)
            {
                for (int i = 0; i < 8; i++)
                    if (!Allowed(px + Dx[i] * (r + 1), py + Dy[i] * (r + 1))) return false;
            }

            switch (rule.Shape)
            {
                case DecalShape.Blob:
                    for (int dy = -r - 1; dy <= r + 1; dy++)
                        for (int dx = -r - 1; dx <= r + 1; dx++)
                        {
                            int n = (int)(DecorHash.Mix(seed, px + dx >> 1, py + dy >> 1, 7) % 5u) - 2;
                            if (dx * dx + dy * dy <= r * r + r * n) Put(px + dx, py + dy, col, 255);
                        }
                    break;
                case DecalShape.Soft:
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int d2 = dx * dx + dy * dy, r2 = r * r;
                            if (d2 < r2) Put(px + dx, py + dy, col, 255 * (r2 - d2) / r2);
                        }
                    break;
                case DecalShape.Ring:
                    {
                        TerrainPalette.Rgb lit = TerrainBake.Mix(col, new TerrainPalette.Rgb(255, 255, 255), 320);
                        TerrainPalette.Rgb dark = TerrainBake.Mix(col, new TerrainPalette.Rgb(0, 0, 0), 450);
                        int inner = Math.Max(0, r - Math.Max(1, _s / 12));
                        for (int dy = -r; dy <= r; dy++)
                            for (int dx = -r; dx <= r; dx++)
                            {
                                int d2 = dx * dx + dy * dy;
                                if (d2 > r * r) continue;
                                if (d2 >= inner * inner) Put(px + dx, py + dy, dx + dy < 0 ? lit : dark, 255);
                                else Put(px + dx, py + dy, col, 190);
                            }
                    }
                    break;
                case DecalShape.Crack:
                    for (int arm = 0; arm < 2; arm++)
                    {
                        int x = px, y = py, dir = (int)(DecorHash.Mix(seed, cx, cy, 5) % 8u) + 4 * arm;
                        for (int i = 0; i < r; i++)
                        {
                            if (i % 3 == 2) dir += (int)(DecorHash.Mix(seed, cx * 31 + i, cy * 17 + arm, 6) % 3u) - 1;
                            int d = (dir & 7);
                            x += Dx[d];
                            y += Dy[d];
                            Put(x, y, col, 255);
                        }
                    }
                    break;
                case DecalShape.Dots:
                    {
                        int dots = 3 + size / 5;
                        for (int i = 0; i < dots; i++)
                        {
                            uint hv = DecorHash.Mix(seed, cx * 64 + i, cy, 8);
                            int dx = (int)(hv % (uint)(2 * r + 1)) - r, dy = (int)((hv >> 8) % (uint)(2 * r + 1)) - r;
                            if (dx * dx + dy * dy > r * r) continue;
                            Put(px + dx, py + dy, col, 255);
                            if ((hv >> 20 & 1u) != 0) Put(px + dx + 1, py + dy, col, 255);
                        }
                    }
                    break;
                case DecalShape.Ripple:
                    {
                        int gap = Math.Max(2, _s / 5);
                        for (int line = -1; line <= 1; line++)
                            for (int dx = -r; dx <= r; dx++)
                            {
                                int wave = Math.Abs((dx + 4 * line & 7) - 4) / 2 - 1;   // -1..1 triangle wave
                                Put(px + dx, py + line * gap + wave, col, 255 * (r + 1 - Math.Abs(dx)) / (r + 1));
                            }
                    }
                    break;
                case DecalShape.Stripe:
                    {
                        int thick = Math.Max(1, _s / 8), dash = Math.Max(2, _s / 4);
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if ((dx + r) / dash % 2 != 0) continue;
                            for (int t = 0; t < thick; t++) Put(px + dx, py + t, col, 255);
                        }
                    }
                    break;
            }

            return true;
        }

        private bool Allowed(int tx, int ty)
        {
            if (tx < 0 || ty < 0 || tx >= _tw || ty >= _th) return false;
            int k = ty / _s * _map.Width + tx / _s;
            return _rule.Allows(_map.Land[k]) && _map.Overlay[k] == OverlayKind.None;
        }

        private void Put(int tx, int ty, TerrainPalette.Rgb col, int coverage)
        {
            if (tx < 0 || ty < 0 || tx >= _tw || ty >= _th) return;
            int k = ty / _s * _map.Width + tx / _s;
            if (!_rule.Allows(_map.Land[k]) || _map.Overlay[k] != OverlayKind.None) return;
            int a = coverage * _rule.Alpha / 255;
            if (a <= 0) return;
            int o = (ty * _tw + tx) * 4, inv = 255 - a;
            _rgba[o] = (byte)(col.R * a / 255 + _rgba[o] * inv / 255);
            _rgba[o + 1] = (byte)(col.G * a / 255 + _rgba[o + 1] * inv / 255);
            _rgba[o + 2] = (byte)(col.B * a / 255 + _rgba[o + 2] * inv / 255);
            _rgba[o + 3] = (byte)(a + _rgba[o + 3] * inv / 255);
        }
    }
}
