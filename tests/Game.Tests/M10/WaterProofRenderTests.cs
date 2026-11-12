// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Game.Tests.Menu;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M10;

/// <summary>M10-13: a headless proof picture. Bakes a coast, lays the water animation on it with the same curves the game uses
/// (software blend, no GPU) and writes scratch/m10b_coast.png when the repo root can be found.</summary>
public sealed class WaterProofRenderTests
{
    [Fact]
    public void Coast_frame_renders_and_differs_between_ticks()
    {
        const int n = 12;
        var land = new LandType[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                land[y * n + x] = x + y < 7 ? LandType.Clear : x + y < 9 ? LandType.Coast : x + y < 11 ? LandType.Shallow : LandType.Water;
        var map = new MapData("coast", "rustbelt", n, n, new ushort[n * n], land, new byte[n * n], new OverlayKind[n * n],
            new byte[n * n], new[] { new CellCoord(0, 0) });
        TerrainBake.Result baked = TerrainBake.Bake(map, TerrainPalette.ForMap(map));
        byte[] shore = WaterShore.Build(map.Land, n, n);

        byte[] a = Compose(baked, map, shore, 10);
        byte[] b = Compose(baked, map, shore, 35);
        Assert.NotEqual(a, b);
        Assert.NotEqual(baked.Rgba, a);

        string? root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "RTS_2027.slnx"))) root = Path.GetDirectoryName(root);
        if (root is null) return;
        Directory.CreateDirectory(Path.Combine(root, "scratch"));
        File.WriteAllBytes(Path.Combine(root, "scratch", "m10b_coast.png"), Png.Encode(a, baked.Width, baked.Height));
    }

    private static byte[] Compose(TerrainBake.Result baked, MapData map, byte[] shore, int tick)
    {
        byte[] px = (byte[])baked.Rgba.Clone();
        float k = baked.Width / (float)(map.Width * Camera.CellPx);
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (!WaterShore.IsWater(map.Land[y * map.Width + x])) continue;
                for (int i = 0; i < WaterWaves.GlintsPerCell; i++)
                {
                    byte al = WaterWaves.Glint(x, y, i, tick, out Rectangle g);
                    Blend(px, baked.Width, k, x * 64 + g.X, y * 64 + g.Y, g.Width, g.Height, 0xD8, 0xEC, 0xF4, al);
                }

                byte m = shore[y * map.Width + x];
                int w = WaterWaves.FoamWidthPx(x, y, tick);
                byte fa = WaterWaves.FoamAlpha(x, y, tick);
                if ((m & WaterShore.North) != 0) Blend(px, baked.Width, k, x * 64, y * 64, 64, w, 0xEE, 0xF4, 0xF2, fa);
                if ((m & WaterShore.South) != 0) Blend(px, baked.Width, k, x * 64, y * 64 + 64 - w, 64, w, 0xEE, 0xF4, 0xF2, fa);
                if ((m & WaterShore.West) != 0) Blend(px, baked.Width, k, x * 64, y * 64, w, 64, 0xEE, 0xF4, 0xF2, fa);
                if ((m & WaterShore.East) != 0) Blend(px, baked.Width, k, x * 64 + 64 - w, y * 64, w, 64, 0xEE, 0xF4, 0xF2, fa);
            }
        }

        // A ship sailing south-east through the shallows, with its wake.
        var wake = new WaterWake();
        for (int t = 0; t <= tick; t++) wake.Update(t, new[] { (new EntityId(1, 1), new Vector2(420 + t * 3f, 500 + t * 3f)) });
        foreach (WakePuff p in wake.Puffs)
        {
            int age = tick - p.Born;
            byte al = WaterWake.AlphaAt(age);
            float r = WaterWake.RadiusAt(age), spread = 3f + age * 0.5f;
            var perp = new Vector2(-p.Dir.Y, p.Dir.X);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 c = p.Pos + perp * (spread * s);
                Blend(px, baked.Width, k, (int)(c.X - r), (int)(c.Y - r), (int)(2 * r), (int)(2 * r), 0xE4, 0xF0, 0xF2, al);
            }
        }

        return px;
    }

    private static void Blend(byte[] px, int width, float k, int x, int y, int w, int h, int r, int g, int b, int alpha)
    {
        if (alpha == 0) return;
        int x0 = (int)(x * k), y0 = (int)(y * k), x1 = Math.Max(x0 + 1, (int)((x + w) * k)), y1 = Math.Max(y0 + 1, (int)((y + h) * k));
        for (int yy = Math.Max(0, y0); yy < y1; yy++)
        {
            for (int xx = Math.Max(0, x0); xx < Math.Min(width, x1); xx++)
            {
                int i = (yy * width + xx) * 4;
                if (i + 2 >= px.Length) return;
                px[i] = (byte)(px[i] + (r - px[i]) * alpha / 255);
                px[i + 1] = (byte)(px[i + 1] + (g - px[i + 1]) * alpha / 255);
                px[i + 2] = (byte)(px[i + 2] + (b - px[i + 2]) * alpha / 255);
            }
        }
    }
}
