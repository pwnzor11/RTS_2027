// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Render;

/// <summary>M12-02 and M12-03: land-linked sprites, the prop atlas contract, the decor toggle and the cost of it all.</summary>
public sealed class PropTests
{
    private static MapData Generated(Biome biome, ulong seed = 7, MapSize size = MapSize.Medium) =>
        MapGenerator.Generate(new MapGenParams(biome, size, 2, seed));

    public static IEnumerable<object[]> AllKinds() => Enum.GetValues<PropKind>().SelectMany(k => Enumerable.Range(0, PropArt.Variants).Select(v => new object[] { k, v }));

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void EveryFrameIsDrawnInsideItsCellAndHasBody(PropKind kind, int variant)
    {
        TerrainPalette p = TerrainPalette.Load("rustbelt");
        byte[] rgba = PropArt.Render(kind, variant, p);
        Assert.Equal(PropArt.FramePx * PropArt.FramePx * 4, rgba.Length);

        int solid = 0, edge = 0;
        for (int y = 0; y < PropArt.FramePx; y++)
            for (int x = 0; x < PropArt.FramePx; x++)
            {
                byte a = rgba[(y * PropArt.FramePx + x) * 4 + 3];
                if (a > 200) solid++;
                if (a > 0 && (x == 0 || y == 0 || x == PropArt.FramePx - 1 || y == PropArt.FramePx - 1)) edge++;
            }

        Assert.True(solid >= 60, $"{kind}/{variant}: {solid} solid pixels");
        Assert.Equal(0, edge);
        Assert.Equal(rgba, PropArt.Render(kind, variant, p));   // deterministic
    }

    [Fact]
    public void LargerVariantsCoverMore()
    {
        TerrainPalette p = TerrainPalette.Load("rustbelt");
        foreach (PropKind kind in Enum.GetValues<PropKind>())
        {
            int[] area = Enumerable.Range(0, PropArt.Variants)
                .Select(v => CountSolid(PropArt.Render(kind, v, p))).ToArray();
            Assert.True(area[0] < area[1] && area[1] < area[2], $"{kind}: {string.Join(",", area)}");
        }
    }

    [Theory]
    [InlineData("rustbelt", LandType.Rock, PropKind.Boulder)]
    [InlineData("rustbelt", LandType.Rough, PropKind.Rubble)]
    [InlineData("mountains", LandType.Rock, PropKind.Boulder)]
    public void SpritesAreALightStepAboveTheGroundTheyStandOn(string theater, LandType ground, PropKind kind)
    {
        TerrainPalette p = TerrainPalette.Load(theater);
        // The brightest solid pixel of the large variant is clearly lighter than the ground's base colour (it is the lit face).
        byte[] rgba = PropArt.Render(kind, 2, p);
        double best = 0;
        for (int i = 0; i < rgba.Length; i += 4)
            if (rgba[i + 3] == 255) best = Math.Max(best, TerrainPalette.Luminance(new TerrainPalette.Rgb(rgba[i], rgba[i + 1], rgba[i + 2])));
        double floor = TerrainPalette.Luminance(p.ShadesFor(ground).Base);
        Assert.True((best + 0.05) / (floor + 0.05) >= 1.5, $"{theater} {kind}: lit face {best:0.000} vs ground {floor:0.000}");
    }

    [Fact]
    public void AtlasFramesAreInsideAndDisjointAndVariantsWrap()
    {
        IPropAtlas atlas = new ProceduralPropAtlas(TerrainPalette.Load("glass_coast"));
        Assert.Equal(atlas.Width * atlas.Height * 4, atlas.Rgba.Length);
        var seen = new HashSet<(int, int)>();
        foreach (PropKind kind in Enum.GetValues<PropKind>())
        {
            Assert.True(atlas.VariantCount(kind) >= 1);
            for (int v = 0; v < atlas.VariantCount(kind); v++)
            {
                PropRect r = atlas.Frame(kind, v);
                Assert.InRange(r.X + r.Width, 1, atlas.Width);
                Assert.InRange(r.Y + r.Height, 1, atlas.Height);
                Assert.True(seen.Add((r.X, r.Y)), "frames do not overlap");
                Assert.Equal(r, atlas.Frame(kind, v + atlas.VariantCount(kind)));
                Assert.Equal(r, atlas.Frame(kind, v - atlas.VariantCount(kind)));
            }
        }

        // the atlas holds exactly the pixels PropArt draws
        PropRect f = atlas.Frame(PropKind.GlassCrest, 1);
        byte[] direct = PropArt.Render(PropKind.GlassCrest, 1, TerrainPalette.Load("glass_coast"));
        for (int y = 0; y < f.Height; y++)
            Assert.True(direct.AsSpan(y * f.Width * 4, f.Width * 4).SequenceEqual(atlas.Rgba.AsSpan(((f.Y + y) * atlas.Width + f.X) * 4, f.Width * 4)));
    }

    [Theory]
    [InlineData(Biome.RustBelt, PropKind.Boulder, PropKind.Rubble, PropKind.IceSlab)]
    [InlineData(Biome.GlassCoast, PropKind.GlassCrest, PropKind.Rubble, null)]
    [InlineData(Biome.Mountains, PropKind.Boulder, PropKind.Rubble, PropKind.IceSlab)]
    [InlineData(Biome.AshPlains, PropKind.Boulder, PropKind.Rubble, null)]
    public void PlanFollowsTheLandAndTheBiomeTable(Biome biome, PropKind rock, PropKind rough, PropKind? bank)
    {
        MapData map = Generated(biome);
        TerrainPalette pal = TerrainPalette.ForMap(map);
        ulong seed = MapHasher.Compute(map);
        PropInstance[] plan = PropPlanner.Plan(map, pal, seed);

        Assert.Equal(plan, PropPlanner.Plan(map, pal, seed));
        Assert.NotEmpty(plan);
        Assert.True(plan.Select(p => p.Kind).Distinct().All(k => k == rock || k == rough || k == bank), "only the kinds in the biome's table");

        foreach (PropInstance p in plan)
        {
            int k = p.CellY * map.Width + p.CellX;
            LandType land = map.Land[k];
            if (p.Kind == rock) Assert.Equal(LandType.Rock, land);
            else if (p.Kind == rough) Assert.Equal(LandType.Rough, land);
            else Assert.Equal(LandType.Water, land);
            Assert.Equal(OverlayKind.None, map.Overlay[k]);
            foreach (CellCoord s in map.StartPositions)
                Assert.True(Math.Max(Math.Abs(s.X - p.CellX), Math.Abs(s.Y - p.CellY)) > PropPlanner.StartKeepOut, "no prop in a start's keep-out");
            Assert.InRange(p.Variant, 0, PropArt.Variants - 1);
        }

        for (int i = 1; i < plan.Length; i++) Assert.True(plan[i - 1].CellY <= plan[i].CellY, "sorted by row");
        Assert.True(plan.Length < map.Width * map.Height / 8, "at most about 8 % of cells carry a prop (design 16 section 2.7)");
    }

    [Fact]
    public void EveryRockClusterGetsAtLeastOneBoulder()
    {
        MapData map = Generated(Biome.Mountains);
        PropInstance[] plan = PropPlanner.Plan(map, TerrainPalette.ForMap(map), MapHasher.Compute(map));
        var propCells = plan.Where(p => p.Kind == PropKind.Boulder).Select(p => (X: (int)p.CellX, Y: (int)p.CellY)).ToHashSet();
        int w = map.Width, h = map.Height, clusters = 0, empty = 0;
        var seen = new bool[w * h];
        for (int s = 0; s < w * h; s++)
        {
            if (seen[s] || map.Land[s] != LandType.Rock) continue;
            clusters++;
            var stack = new Stack<int>();
            stack.Push(s);
            seen[s] = true;
            bool any = false, usable = false;
            while (stack.Count > 0)
            {
                int k = stack.Pop();
                int x = k % w, y = k / w;
                any |= propCells.Contains((x, y));
                usable |= map.Overlay[k] == OverlayKind.None && !map.StartPositions.Any(c => Math.Max(Math.Abs(c.X - x), Math.Abs(c.Y - y)) <= PropPlanner.StartKeepOut);
                foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                {
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int n = ny * w + nx;
                    if (seen[n] || map.Land[n] != LandType.Rock) continue;
                    seen[n] = true;
                    stack.Push(n);
                }
            }

            if (usable && !any) empty++;
        }

        Assert.True(clusters > 0);
        Assert.Equal(0, empty);
    }

    [Fact]
    public void ClassicPaletteHasNoDecorAtAll()
    {
        MapData map = Generated(Biome.RustBelt);
        TerrainPalette classic = TerrainPalette.Load(RepoFile("data/theaters/variants/classic/rustbelt.toml"));
        Assert.Empty(PropPlanner.Plan(map, classic, 1));
        Assert.Equal(0, DecorBake.Bake(map, classic, 1).Count);
    }

    [Fact]
    public void FirstAtRowFindsTheRowStart()
    {
        PropInstance[] plan = { New(0, 1), New(3, 1), New(1, 4), New(2, 4), New(0, 9) };
        Assert.Equal(0, PropPlanner.FirstAtRow(plan, 0));
        Assert.Equal(0, PropPlanner.FirstAtRow(plan, 1));
        Assert.Equal(2, PropPlanner.FirstAtRow(plan, 2));
        Assert.Equal(2, PropPlanner.FirstAtRow(plan, 4));
        Assert.Equal(4, PropPlanner.FirstAtRow(plan, 5));
        Assert.Equal(5, PropPlanner.FirstAtRow(plan, 10));
    }

    [Fact]
    public void DecorToggleIsAFreeKeyInTheSystemGroupAndOnlyTheQuickKeysFollowIt()
    {
        GameAction[] all = Enum.GetValues<GameAction>();
        Assert.Equal(GameAction.QuickLoad, all[^1]);   // D-205 appended QuickSave and QuickLoad
        Assert.Equal(GameAction.QuickSave, all[^2]);
        Assert.Equal(GameAction.DecorToggle, all[^3]);
        Assert.Equal(GameAction.LastAlert, all[^4]);
        ActionInfo info = GameKeys.InfoOf(GameAction.DecorToggle);
        Assert.Equal(GameKeys.System, info.Group);
        Assert.Equal("decor_toggle", info.Id);
        Assert.Single(GameKeys.Infos, i => i.Default == info.Default && i.Context == info.Context);
        Assert.Contains(GameKeys.Rows, r => r.Action == GameAction.DecorToggle);
    }

    [Fact]
    public void DecorSettingsStartOnAndFlip()
    {
        bool was = DecorSettings.Enabled;
        try
        {
            DecorSettings.Enabled = false;
            Assert.False(DecorSettings.Enabled);
            DecorSettings.Enabled = true;
            Assert.True(DecorSettings.Enabled);
        }
        finally { DecorSettings.Enabled = was; }
    }

    [Fact]
    public void PlanAndPerFrameCullingAreCheapOnTheBiggestMap()
    {
        MapData map = Generated(Biome.Mountains, 5, MapSize.Large);
        TerrainPalette pal = TerrainPalette.ForMap(map);
        ulong seed = MapHasher.Compute(map);
        PropPlanner.Plan(map, pal, seed);   // warm up

        long t0 = Stopwatch.GetTimestamp();
        PropInstance[] plan = PropPlanner.Plan(map, pal, seed);
        double planMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        double atlasMs = Time(() => new ProceduralPropAtlas(pal));
        Assert.True(plan.Length > 0);
        Assert.True(planMs < 1000, $"prop plan took {planMs:0} ms");
        Assert.True(atlasMs < 1000, $"atlas took {atlasMs:0} ms");

        // One frame's work is: find the first row, walk the props in view. Do it for a 30 x 17 cell view 2000 times.
        long t1 = Stopwatch.GetTimestamp();
        long visited = 0;
        for (int i = 0; i < 2000; i++)
        {
            int y0 = i * 7 % (map.Height - 17), x0 = i * 13 % (map.Width - 30);
            for (int j = PropPlanner.FirstAtRow(plan, y0 - 1); j < plan.Length; j++)
            {
                PropInstance p = plan[j];
                if (p.CellY > y0 + 18) break;
                if (p.CellX >= x0 - 1 && p.CellX <= x0 + 31) visited++;
            }
        }

        double perFrameUs = Stopwatch.GetElapsedTime(t1).TotalMilliseconds * 1000 / 2000;
        Assert.True(visited > 0);
        Assert.True(perFrameUs < 2000, $"culling one frame took {perFrameUs:0} us (budget 2000 us; a 60 fps frame is 16667 us)");
    }

    private static double Time(Action a)
    {
        long t = Stopwatch.GetTimestamp();
        a();
        return Stopwatch.GetElapsedTime(t).TotalMilliseconds;
    }

    private static PropInstance New(int x, int y) => new(PropKind.Boulder, 0, (short)x, (short)y, 0, 0);

    private static int CountSolid(byte[] rgba)
    {
        int n = 0;
        for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] > 128) n++;
        return n;
    }

    private static string RepoFile(string relative)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException($"{relative} not found above {AppContext.BaseDirectory}");
    }
}
