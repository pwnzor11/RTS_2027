// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests;

/// <summary>S43 terrain detail pass: noise range, determinism, per-cell repaint equality, soft blends, and that level 0 is untouched.</summary>
public sealed class TerrainDetailTests
{
    private static string RepoFile(string relative)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException(relative);
    }

    private static MapData Mixed()
    {
        const int n = 8;
        var land = new LandType[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                land[y * n + x] = x < 3 ? LandType.Clear : x < 5 ? LandType.Ash : y < 4 ? LandType.Water : LandType.Cliff;
        land[2 * n + 1] = LandType.Road;
        land[3 * n + 1] = LandType.Road;
        return new MapData("t", "rustbelt", n, n, new ushort[n * n], land, new byte[n * n], new OverlayKind[n * n], new byte[n * n], new[] { new CellCoord(0, 0) });
    }

    [Fact]
    public void ValueNoiseStaysInRangeAndIsDeterministic()
    {
        int lo = int.MaxValue, hi = int.MinValue;
        for (int y = 0; y < 200; y += 3)
            for (int x = 0; x < 200; x += 3)
            {
                int v = TerrainDetail.Vn(x, y, 20, 12, 5u);
                Assert.Equal(v, TerrainDetail.Vn(x, y, 20, 12, 5u));
                lo = Math.Min(lo, v);
                hi = Math.Max(hi, v);
            }

        Assert.InRange(lo, 0, 1023);
        Assert.InRange(hi, 0, 1023);
        Assert.True(hi - lo > 500, "noise should span most of its range");
    }

    [Fact]
    public void DetailBakeIsDeterministicAndDiffersFromThePlainLook()
    {
        TerrainPalette detail = TerrainPalette.Load(RepoFile("data/theaters/rustbelt.toml"));
        Assert.True(detail.Style.Detail > 0);
        TerrainPalette plain = PlainCopy("data/theaters/variants/c/rustbelt.toml");   // S43: variant C now carries [detail]; level 0 is the plain D-181 look
        Assert.Equal(0, plain.Style.Detail);
        MapData map = Mixed();
        TerrainBake.Result a = TerrainBake.Bake(map, detail), b = TerrainBake.Bake(map, detail), p = TerrainBake.Bake(map, plain);
        Assert.Equal(a.Rgba, b.Rgba);
        Assert.NotEqual(a.Rgba, p.Rgba);
        for (int i = 3; i < a.Rgba.Length; i += 4) Assert.Equal(255, a.Rgba[i]);
    }

    [Fact]
    public void RepaintingACellMatchesTheWholeBake()
    {
        TerrainPalette pal = TerrainPalette.Load(RepoFile("data/theaters/rustbelt.toml"));
        MapData map = Mixed();
        TerrainBake.Result whole = TerrainBake.Bake(map, pal);
        CellGrid grid = CellGrid.FromMap(map);
        int s = pal.TexelCells;
        foreach ((int cx, int cy) in new[] { (0, 0), (2, 2), (4, 3), (6, 4), (7, 7) })
        {
            byte[] cell = TerrainBake.PaintCell(pal, grid, cx, cy);
            for (int sy = 0; sy < s; sy++)
                for (int sx = 0; sx < s; sx++)
                    for (int c = 0; c < 4; c++)
                        Assert.Equal(whole.Rgba[((cy * s + sy) * whole.Width + cx * s + sx) * 4 + c], cell[(sy * s + sx) * 4 + c]);
        }
    }

    [Fact]
    public void BordersBetweenGroundTypesAreSoftNotAHardEdge()
    {
        TerrainPalette pal = TerrainPalette.Load(RepoFile("data/theaters/rustbelt.toml"));
        CellGrid grid = CellGrid.FromMap(Mixed());
        int s = pal.TexelCells;
        // Clear (x=2) meets Ash (x=3): texels two cells' worth of reach away from the border are pure ground, those beside it are mixed.
        int ty = 6 * s;   // a row with no road
        TerrainPalette.Rgb nearEdge = TerrainBake.TexelColour(pal, grid, 3 * s - 1, ty);
        TerrainPalette.Rgb pureClear = TerrainDetail.Ground(pal, LandType.Clear, 3 * s - 1, ty, true);
        Assert.NotEqual(pureClear, nearEdge);
    }

    [Fact]
    public void AshGroundHasItsOwnTextureDistinctFromClear()
    {
        TerrainPalette pal = TerrainPalette.Load(RepoFile("data/theaters/rustbelt.toml"));
        var ash = new HashSet<TerrainPalette.Rgb>();
        var clear = new HashSet<TerrainPalette.Rgb>();
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                ash.Add(TerrainDetail.Ground(pal, LandType.Ash, x, y, true));
                clear.Add(TerrainDetail.Ground(pal, LandType.Clear, x, y, true));
            }

        Assert.True(ash.Count > 20 && clear.Count > 20, "ground should carry many tones, not three flat shades");
        Assert.Empty(ash.Intersect(clear));
    }

    /// <summary>A copy of a palette file with the detail pass switched off (level 0), loaded from a temp file.</summary>
    internal static TerrainPalette PlainCopy(string repoPath)
    {
        string text = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(RepoFile(repoPath)), @"(?m)^level = \d", "level = 0");
        string tmp = Path.Combine(Path.GetTempPath(), $"rts_plain_{Guid.NewGuid():N}.toml");
        File.WriteAllText(tmp, text);
        try { return TerrainPalette.Load(tmp); } finally { File.Delete(tmp); }
    }
}
