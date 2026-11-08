// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Pathing;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Pathing;
using Rts.Sim.World;

namespace Rts.Sim.Tests.Map;

/// <summary>M12-04 (D-180 D1): map format 4, the blocking prop overlay kinds, the optional decor layer and the three prop validator codes.</summary>
public sealed class MapFormat4Tests
{
    private static readonly OverlayKind[] Props = { OverlayKind.Tree, OverlayKind.Wreck, OverlayKind.Block, OverlayKind.Pylon, OverlayKind.Snag };

    private static string Temp(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), "rts_map4_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name);
    }

    private static MapData WithProps(int n, Func<int, OverlayKind> prop, byte[]? decor = null)
    {
        MapData flat = MapData.CreateFlat(n, n, "props");
        var ovl = new OverlayKind[n * n];
        var den = new byte[n * n];
        for (int i = 0; i < n * n; i++) { ovl[i] = prop(i); if (ovl[i] != OverlayKind.None) den[i] = (byte)(i % 3); }
        return new MapData("props", "rust_belt", n, n, new ushort[n * n], new LandType[n * n], new byte[n * n], ovl, den, flat.StartPositions, decor);
    }

    [Fact]
    public void OverlayValues_AreAppendOnlyAfterWall()
    {
        Assert.Equal(3, (int)OverlayKind.Wall);
        Assert.Equal(new[] { 4, 5, 6, 7, 8 }, Props.Select(p => (int)p).ToArray());
        Assert.Equal(OverlayKind.Snag, OverlayKinds.Last);
        foreach (OverlayKind p in Props) { Assert.True(OverlayKinds.IsProp(p)); Assert.False(OverlayKinds.IsResource(p)); Assert.True(OverlayKinds.BlocksGround(p)); }
        Assert.True(OverlayKinds.BlocksGround(OverlayKind.Wall));
        Assert.False(OverlayKinds.IsProp(OverlayKind.Wall));
        Assert.False(OverlayKinds.BlocksGround(OverlayKind.Ashfall));
    }

    [Fact]
    public void Prop_BlocksEveryGroundLocomotion_NotAir_AndBumpsVersion()
    {
        TerrainTable table = PathServiceTests.Table();
        var m = new MapState(MapData.CreateFlat(8, 8));
        var c = new CellCoord(3, 3);
        int v = m.Version;
        foreach (OverlayKind p in Props)
        {
            m.SetOverlay(c, p, 1);
            Assert.Equal(0, m.SpeedPercent(c, Locomotion.Foot, table));
            Assert.Equal(0, m.SpeedPercent(c, Locomotion.Tracked, table));
            Assert.Equal(0, m.SpeedPercent(c, Locomotion.Wheeled, table));
            Assert.True(m.SpeedPercent(c, Locomotion.Air, table) > 0);
            Assert.False(m.IsBuildable(c, ScriptedGame.Terrain()));
        }
        Assert.Equal(v + 1, m.Version);   // None -> prop bumps once; prop -> prop does not
        m.SetOverlay(c, OverlayKind.None, 0);
        Assert.Equal(v + 2, m.Version);
    }

    [Fact]
    public void PathService_RoutesAroundAWallOfProps_AndFailsWhenSealed()
    {
        TerrainTable table = PathServiceTests.Table();
        var m = new MapState(MapData.CreateFlat(12, 12));
        for (int y = 0; y < 11; y++) m.SetOverlay(new CellCoord(6, y), OverlayKind.Tree, 0);   // a gap at y = 11
        var s = new PathService(m, table);
        int id = s.Request(new CellCoord(2, 2), new CellCoord(10, 2), Locomotion.Foot);
        s.Process();
        Assert.Equal(PathStatus.Found, s.Poll(id, 0, out IReadOnlyList<CellCoord> path));
        Assert.Contains(path, c => c.Y == 11);
        Assert.DoesNotContain(path, c => m.OverlayAt(c) == OverlayKind.Tree);

        m.SetOverlay(new CellCoord(6, 11), OverlayKind.Wreck, 0);
        int id2 = s.Request(new CellCoord(2, 2), new CellCoord(10, 2), Locomotion.Foot);
        s.Process();
        Assert.NotEqual(PathStatus.Found, s.Poll(id2, 0, out _));
    }

    [Fact]
    public void Prop_BlocksBuildingPlacement()
    {
        var sim = new Simulation(new SimSetup(1, WithProps(16, i => i == 5 * 16 + 5 ? OverlayKind.Block : OverlayKind.None), 2,
            ScriptedGame.RulesWith(new[] { ScriptedGame.Vehicle("t", 20, 10) }, new[] { ScriptedGame.Infantry("i", 9, 10) }), 1) { AllowDebug = true });
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(0, 2, new CellCoord(5, 5)));    // turret on the prop
        Assert.Equal(PlacementResult.Blocked, sim.CanPlace(0, 1, new CellCoord(4, 4)));    // hq footprint covers it
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, 2, new CellCoord(8, 8)));
    }

    [Fact]
    public void Props_AreNeverResources()
    {
        // A prop is not a field: the harvest and regrowth tests read only Ashfall and Pitchblende.
        foreach (OverlayKind p in Props) Assert.False(OverlayKinds.IsResource(p));
        MapData map = WithProps(8, i => i == 10 ? OverlayKind.Tree : OverlayKind.None);
        Assert.Equal(0, MapAnalysis4.FieldCount(map));
    }

    [Fact]
    public void Format4_RoundTrips_PropsAndDecor_AndDecorIsNotHashed()
    {
        var decor = new byte[64];
        decor[9] = 2; decor[20] = 1;
        MapData withDecor = WithProps(8, i => i % 7 == 0 ? Props[i % 5] : OverlayKind.None, decor);
        MapData withoutDecor = WithProps(8, i => i % 7 == 0 ? Props[i % 5] : OverlayKind.None);
        Assert.Equal(MapHasher.Compute(withoutDecor), MapHasher.Compute(withDecor));   // decor is render only

        string path = Temp("p.map.toml");
        MapFile.Save(withDecor, path);
        Assert.Contains("format_version = 4", File.ReadAllText(path));
        MapData back = MapFile.Load(path);
        Assert.Equal(MapHasher.Compute(withDecor), MapHasher.Compute(back));
        Assert.Equal(withDecor.Overlay.ToArray(), back.Overlay.ToArray());
        Assert.Equal(withDecor.Density.ToArray(), back.Density.ToArray());
        Assert.Equal(decor, back.Decor!.ToArray());

        string none = Temp("n.map.toml");
        MapFile.Save(withoutDecor, none);
        Assert.DoesNotContain("decor", File.ReadAllText(none));
        Assert.Null(MapFile.Load(none).Decor);
    }

    [Fact]
    public void PropInAFormat3File_IsRejected_AndBadKindIsRejected()
    {
        MapData map = WithProps(8, i => i == 12 ? OverlayKind.Pylon : OverlayKind.None);
        string path = Temp("o.map.toml");
        MapFile.Save(map, path);
        File.WriteAllText(path, File.ReadAllText(path).Replace("format_version = 4", "format_version = 3"));
        Assert.Throws<MapFileException>(() => MapFile.Load(path));

        var ovl = new byte[64];
        ovl[3] = (byte)OverlayKinds.Last + 1;
        string bad = Temp("b.map.toml");
        MapFile.Save(WithProps(8, _ => OverlayKind.None), bad);
        string text = File.ReadAllText(bad).Replace("overlay = \"" + Convert.ToBase64String(new byte[64]) + "\"", "overlay = \"" + Convert.ToBase64String(ovl) + "\"");
        File.WriteAllText(bad, text);
        Assert.Throws<MapFileException>(() => MapFile.Load(bad));
    }

    [Fact]
    public void ExistingMaps_KeepTheirHash_WhenNoPropsArePresent()
    {
        MapData flat = MapData.CreateFlat(16, 16, "same");
        MapData same = new("same", flat.Theater, 16, 16, new ushort[256], new LandType[256], new byte[256], new OverlayKind[256], new byte[256], flat.StartPositions, new byte[256]);
        Assert.Equal(MapHasher.Compute(flat), MapHasher.Compute(same));
    }

    private const int N = 40;

    private static MapData Crafted(Action<OverlayKind[], byte[]> edit)
    {
        var land = new LandType[N * N];
        var ovl = new OverlayKind[N * N];
        var den = new byte[N * N];
        var starts = new List<CellCoord> { new(6, 6), new(N - 7, N - 7) };
        foreach (CellCoord s in starts)
        {
            int sx = s.X < N / 2 ? 1 : -1;
            foreach ((int x, int y) in new[] { (s.X + 5 * sx, s.Y), (s.X, s.Y + 5 * sx) }) { ovl[y * N + x] = OverlayKind.Ashfall; den[y * N + x] = 6; land[y * N + x] = LandType.Ash; }
        }
        edit(ovl, den);
        return new MapData("crafted", "rustbelt", N, N, new ushort[N * N], land, new byte[N * N], ovl, den, starts);
    }

    private static string[] Codes(MapData m) => MapValidator.Validate(m, null, null).Select(i => i.Code).ToArray();

    [Fact]
    public void Validator_PropCodes()
    {
        Assert.Empty(Codes(Crafted((o, d) => o[20 * N + 20] = OverlayKind.Tree)));   // a harmless prop

        Assert.Contains("prop_in_start_square", Codes(Crafted((o, d) => o[7 * N + 7] = OverlayKind.Block)));
        Assert.Contains("prop_on_resource", Codes(Crafted((o, d) => o[6 * N + 12] = OverlayKind.Wreck)));   // beside the field at (11,6)

        // a prop column from top to bottom cuts the map in two
        Assert.Contains("prop_blocks_path", Codes(Crafted((o, d) => { for (int y = 0; y < N; y++) o[y * N + 20] = OverlayKind.Snag; })));
    }
}

/// <summary>Counts resource fields of a map (test helper).</summary>
internal static class MapAnalysis4
{
    public static int FieldCount(MapData map)
    {
        Rts.Data.MapGen.MapAnalysis.FieldIds(map, out int count);
        return count;
    }
}
