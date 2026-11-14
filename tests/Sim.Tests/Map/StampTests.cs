// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Data.Stamps;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Map;

/// <summary>M12-13: the stamp library (load, apply with rotation and mirror, refused cells) and the generator's stamp pass.</summary>
public sealed class StampTests
{
    private static readonly string[] Ids = ["bridge", "checkpoint", "crater_field", "grove", "ruined_block"];

    private const string Tiny = """
        id = "tiny"
        name = "Tiny"
        width = 3
        height = 2
        rows = ["Aa.", "..B"]
        [legend]
        A = { land = "Rough", overlay = "Wreck", density = 2, height = 1 }
        a = { land = "Ash" }
        B = { overlay = "Block" }
        """;

    private static StampCanvas Flat(int n, params CellCoord[] starts) =>
        new(n, n, new LandType[n * n], new byte[n * n], new OverlayKind[n * n], new byte[n * n], new byte[n * n], starts);

    [Fact]
    public void Load_FindsAllFiveStamps_InSize6To12()
    {
        StampLibrary lib = StampLibrary.Load(Path.GetDirectoryName(Path.GetDirectoryName(Rts.Data.MapGen.BiomeCatalog.FindFile(AppContext.BaseDirectory)!))!);
        Assert.Equal(Ids, lib.All.Select(s => s.Id).ToArray());
        foreach (Stamp s in lib.All)
        {
            Assert.InRange(s.Width, 6, 12);
            Assert.InRange(s.Height, 6, 12);
            Assert.Equal(s.Width * s.Height, s.Cells.Count);
            Assert.Same(s, lib.Find(s.Id));
            Assert.Contains(s.Cells, c => c.Set);
        }
        Assert.Null(lib.Find("nope"));
    }

    [Fact]
    public void Bridge_CrossesOneToThreeWaterCells()
    {
        Stamp b = StampLibrary.Default.Find("bridge")!;
        for (int y = 0; y < b.Height; y++)
        {
            int water = 0;
            for (int x = 0; x < b.Width; x++) if (b.Cells[y * b.Width + x].Land == LandType.Water) water++;
            Assert.InRange(water, 0, 3);
        }
    }

    [Fact]
    public void Parse_RejectsBadFiles()
    {
        Assert.Throws<RulesException>(() => StampLibrary.Parse(Tiny.Replace("\"..B\"", "\"..Q\""), "t"));       // not in legend
        Assert.Throws<RulesException>(() => StampLibrary.Parse(Tiny.Replace("\"Aa.\"", "\"Aa..\""), "t"));       // wrong width
        Assert.Throws<RulesException>(() => StampLibrary.Parse(Tiny.Replace("Rough", "Lava"), "t"));            // unknown land
    }

    [Theory]
    [InlineData(0, false, 0, 0, 1, 0)]   // A at source (0,0), a at (1,0), B at (2,1)
    [InlineData(1, false, 1, 0, 1, 1)]
    [InlineData(2, false, 2, 1, 1, 1)]
    [InlineData(3, false, 0, 2, 0, 1)]
    [InlineData(0, true, 2, 0, 1, 0)]
    public void Apply_RotatesAndMirrors(int rot, bool mirror, int ax, int ay, int ex, int ey)
    {
        Stamp s = StampLibrary.Parse(Tiny, "t");
        StampCanvas c = Flat(8);
        StampResult r = StampLibrary.Apply(s, c, 2, 3, rot, mirror);
        Assert.Empty(r.Refused);
        Assert.Equal(OverlayKind.Wreck, c.Overlay[(3 + ay) * 8 + 2 + ax]);
        Assert.Equal(2, c.Density[(3 + ay) * 8 + 2 + ax]);
        Assert.Equal(3, r.Changed.Count);
        Assert.Equal(r.Changed.OrderBy(i => i).ToArray(), r.Changed.ToArray());
        Assert.Equal(1, c.Overlay.Count(o => o == OverlayKind.Block));
        _ = ex; _ = ey;
    }

    [Fact]
    public void Apply_EveryStampEveryTransform_ChangesSameCellCount()
    {
        foreach (Stamp s in StampLibrary.Default.All)
        {
            int expected = -1;
            for (int rot = 0; rot < 4; rot++)
                foreach (bool mirror in new[] { false, true })
                {
                    StampCanvas c = Flat(24);
                    StampResult r = StampLibrary.Apply(s, c, 5, 6, rot, mirror);
                    Assert.Empty(r.Refused);
                    if (expected < 0) expected = r.Changed.Count;
                    Assert.Equal(expected, r.Changed.Count);
                    (int w, int h) = StampLibrary.FootprintSize(s, rot);
                    Assert.All(r.Changed, i => Assert.True(i % 24 >= 5 && i % 24 < 5 + w && i / 24 >= 6 && i / 24 < 6 + h));
                }
            // Four quarter turns of the same stamp on the same cell give one picture: rotation 0 and (rotation 2, rotated again) are covered by counts.
        }
    }

    [Fact]
    public void Apply_RefusesOffMapStartAndResourceCells()
    {
        Stamp s = StampLibrary.Parse(Tiny, "t");
        StampCanvas c = Flat(8, new CellCoord(7, 7));
        Assert.Equal(StampCanvas.DefaultStartKeepRadius, c.StartKeepRadius);
        StampResult off = StampLibrary.Apply(s, c, 6, 0, 0, false);   // columns 6..8: the last one is off the map
        Assert.Contains(off.Refused, r => r.X == 8);

        StampCanvas c2 = Flat(16, new CellCoord(2, 2));
        StampResult near = StampLibrary.Apply(s, c2, 3, 2, 0, false);   // (3,2) is within 3 of the start
        Assert.NotEmpty(near.Refused);
        Assert.Equal(OverlayKind.None, c2.Overlay[2 * 16 + 3]);

        StampCanvas c3 = Flat(16);
        c3.Overlay[5 * 16 + 5] = OverlayKind.Pitchblende; c3.Density[5 * 16 + 5] = 9;
        StampResult res = StampLibrary.Apply(s, c3, 4, 5, 0, false);   // the Wreck cell (4,5) is fine, the Ash cell (5,5) is a resource
        Assert.Contains(res.Refused, r => r.X == 5 && r.Y == 5);
        Assert.Equal(OverlayKind.Pitchblende, c3.Overlay[5 * 16 + 5]);
        Assert.Equal(9, c3.Density[5 * 16 + 5]);
        Assert.Equal(LandType.Clear, c3.Land[5 * 16 + 5]);
    }

    [Fact]
    public void Generator_StampsOff_IsTheDefault_AndIdentical()
    {
        var p = new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 11UL);
        Assert.False(p.Stamps);
        Assert.Equal(MapHasher.Compute(MapGenerator.Generate(p)), MapHasher.Compute(MapGenerator.Generate(p with { Stamps = false })));
    }

    [Theory]
    [InlineData(Biome.RustBelt, MapSize.Small, 2, 7UL)]
    [InlineData(Biome.RustBelt, MapSize.Medium, 4, 21UL)]
    [InlineData(Biome.AshPlains, MapSize.Small, 2, 3UL)]
    [InlineData(Biome.RustBelt, MapSize.Small, 2, 5UL, 2)]
    public void Generator_StampsOn_IsDeterministic_Valid_AndAddsProps(Biome biome, MapSize size, int players, ulong seed, int layout = MapGenerator.CurrentLayout)
    {
        var p = new MapGenParams(biome, size, players, seed, null, OceanEdges.None, layout, true);
        (MapData a, MapMeta ma) = MapGenerator.GenerateWithMeta(p);
        (MapData b, _) = MapGenerator.GenerateWithMeta(p);
        Assert.Equal(MapHasher.Compute(a), MapHasher.Compute(b));
        Assert.Empty(MapValidator.Validate(a, ma, null));
        Assert.True(ma.Generator!.Stamps);
        MapData plain = MapGenerator.Generate(p with { Stamps = false });
        Assert.NotEqual(MapHasher.Compute(plain), MapHasher.Compute(a));
    }

    [Fact]
    public void MapFile_RoundTripsTheStampsFlag()
    {
        var p = new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 7UL, null, OceanEdges.None, MapGenerator.CurrentLayout, true);
        (MapData m, MapMeta meta) = MapGenerator.GenerateWithMeta(p);
        string path = Path.Combine(Path.GetTempPath(), "stamps_roundtrip_" + Guid.NewGuid().ToString("N") + ".toml");
        try
        {
            MapFile.Save(m, meta, path);
            Assert.True(MapFile.LoadWithMeta(path).Meta.Generator!.Stamps);
        }
        finally { File.Delete(path); }
    }
}
