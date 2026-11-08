// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests;

/// <summary>S15 terrain looks (M11-07, M11-08): contrast, the optional edge/tone/field passes, and the variant palette files.</summary>
public sealed class TerrainLooksTests
{
    private static readonly string[] Files = { "rustbelt", "glass_coast", "mountains", "ash_plains" };
    private static readonly LandType[] Required = { LandType.Road, LandType.Ash, LandType.Water, LandType.Cliff, LandType.Rough, LandType.Shallow };

    public static IEnumerable<object[]> VariantFiles()
    {
        foreach (string look in new[] { "a", "b", "c" })
        {
            foreach (string file in Files) yield return new object[] { look, file };
        }
    }

    [Fact]
    public void ContrastMatchesWcagExtremes()
    {
        Assert.Equal(21.0, TerrainPalette.Contrast(new TerrainPalette.Rgb(0, 0, 0), new TerrainPalette.Rgb(255, 255, 255)), 3);
        var grey = new TerrainPalette.Rgb(120, 120, 120);
        Assert.Equal(1.0, TerrainPalette.Contrast(grey, grey), 6);
        Assert.Equal(TerrainPalette.Contrast(grey, new TerrainPalette.Rgb(10, 20, 30)), TerrainPalette.Contrast(new TerrainPalette.Rgb(10, 20, 30), grey), 9);
    }

    [Theory]
    [MemberData(nameof(ShippedFiles))]
    public void ShippedPalettesHaveNoStyleSoTheLookIsUnchanged(string file)
    {
        TerrainPalette p = TerrainPalette.Load(RepoFile($"data/theaters/{file}.toml"));

        Assert.Equal(TerrainPalette.TerrainStyle.None, p.Style);
        Assert.Null(p.MinimapAshfall);
        Assert.Null(p.MinimapPitchblende);
    }

    public static IEnumerable<object[]> ShippedFiles() => Files.Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(VariantFiles))]
    public void VariantParsesAndSeparatesTheSixTerrainTypes(string look, string file)
    {
        TerrainPalette p = TerrainPalette.Load(RepoFile($"data/theaters/variants/{look}/{file}.toml"));

        for (int i = 0; i < Required.Length; i++)
        {
            for (int j = i + 1; j < Required.Length; j++)
            {
                double c = TerrainPalette.Contrast(p.ShadesFor(Required[i]).Base, p.ShadesFor(Required[j]).Base);
                Assert.True(c >= 1.5, $"{look}/{file}: {Required[i]} vs {Required[j]} contrast {c:0.00} is below 1.5");
            }
        }

        Assert.Equal(look == "a", !p.Style.HasEdgeEffects && p.Style.ToneAmp == 0);
        Assert.Equal(look == "c", p.Style.Ashfall is not null && p.Style.Pitchblende is not null && p.MinimapAshfall is not null && p.MinimapPitchblende is not null);
    }

    [Theory]
    [MemberData(nameof(VariantFiles))]
    public void VariantBakeIsDeterministicAndCheap(string look, string file)
    {
        TerrainPalette p = TerrainPalette.Load(RepoFile($"data/theaters/variants/{look}/{file}.toml"));
        MapData map = MapFile.Load(RepoFile("data/maps/rustbelt_m2.map.toml"));

        TerrainBake.Result a = TerrainBake.Bake(map, p);
        TerrainBake.Result b = TerrainBake.Bake(map, p);

        Assert.Equal(map.Width * p.TexelCells, a.Width);
        Assert.Equal(a.Rgba, b.Rgba);
    }

    [Fact]
    public void ToneIsBoundedSmoothAndDeterministic()
    {
        int min = int.MaxValue, max = int.MinValue, maxStep = 0, prev = TerrainBake.Tone(0, 5, 112, 7);
        for (int x = 0; x < 600; x++)
        {
            int t = TerrainBake.Tone(x, 5, 112, 7);
            Assert.Equal(t, TerrainBake.Tone(x, 5, 112, 7));
            min = Math.Min(min, t);
            max = Math.Max(max, t);
            maxStep = Math.Max(maxStep, Math.Abs(t - prev));
            prev = t;
        }

        Assert.InRange(min, -7, 7);
        Assert.InRange(max, -7, 7);
        Assert.True(max - min >= 4, "tone should actually vary across the map");
        Assert.True(maxStep <= 1, "tone changes by at most one level per texel");
        Assert.Equal(0, TerrainBake.Tone(10, 10, 112, 0));
    }

    [Fact]
    public void ShoreBandIsOnWaterNextToLandOnly()
    {
        TerrainPalette p = Variant("b", "rustbelt");
        MapData map = Grid(5, LandType.Clear, (1, 1, LandType.Water), (2, 1, LandType.Water), (1, 2, LandType.Water), (2, 2, LandType.Water));
        TerrainBake.Result with = TerrainBake.Bake(map, p);
        TerrainBake.Result without = TerrainBake.Bake(map, WithoutStyle(p));
        int s = p.TexelCells;
        TerrainPalette.Rgb foam = p.Style.Shore!.Value;

        // Left edge of the lake (cell 1,1): foam; the lake's inside (the shared border between cells 1,1 and 2,1) is not touched.
        double edge = TerrainPalette.Contrast(Px(with, 1 * s, 1 * s + s / 2), foam);
        double edgePlain = TerrainPalette.Contrast(Px(without, 1 * s, 1 * s + s / 2), foam);
        Assert.True(edge < edgePlain, "shore texel is closer to the foam colour than the plain water");
        Assert.Equal(Px(without, 2 * s - 1, 1 * s + s / 2), Px(with, 2 * s - 1, 1 * s + s / 2));
        Assert.Equal(Px(without, 0, 0), Px(with, 0, 0));
    }

    [Fact]
    public void RoadEdgeLineIsOnTheRoadRimOnly()
    {
        TerrainPalette p = Variant("b", "rustbelt");
        MapData map = Grid(5, LandType.Clear, (1, 1, LandType.Road), (2, 1, LandType.Road), (3, 1, LandType.Road));
        TerrainBake.Result baked = TerrainBake.Bake(map, p);
        int s = p.TexelCells;
        TerrainPalette.Rgb line = p.Style.RoadEdge!.Value;

        TerrainPalette.Rgb top = Px(baked, 2 * s + s / 2, 1 * s);                 // top edge of the middle road cell
        TerrainPalette.Rgb middle = Px(baked, 2 * s + s / 2, 1 * s + s / 2);
        Assert.True(TerrainPalette.Contrast(top, line) < 1.5, "road edge texel takes the line colour");
        Assert.True(TerrainPalette.Contrast(middle, line) > 1.5, "road middle does not");
        // The seam between two road cells is not an edge.
        Assert.True(TerrainPalette.Contrast(Px(baked, 2 * s, 1 * s + s / 2), line) > 1.5);
    }

    [Fact]
    public void CliffGetsALightLipAndCastsAShadowSouthButNotNorth()
    {
        TerrainPalette p = Variant("b", "rustbelt");
        MapData map = Grid(5, LandType.Clear, (2, 2, LandType.Cliff));
        TerrainBake.Result with = TerrainBake.Bake(map, p);
        TerrainBake.Result without = TerrainBake.Bake(map, WithoutStyle(p));
        int s = p.TexelCells;

        Assert.True(TerrainPalette.Luminance(Px(with, 2 * s + s / 2, 2 * s)) > TerrainPalette.Luminance(Px(without, 2 * s + s / 2, 2 * s)), "north lip is lighter");
        Assert.True(TerrainPalette.Luminance(Px(with, 2 * s + s / 2, 3 * s)) < TerrainPalette.Luminance(Px(without, 2 * s + s / 2, 3 * s)), "ground south of the cliff is shadowed");
        Assert.Equal(Px(without, 2 * s + s / 2, 2 * s - 1), Px(with, 2 * s + s / 2, 2 * s - 1));   // north of the cliff: no shadow
    }

    [Fact]
    public void FieldsGrowRicherWithDensityAndStayInsideTheCell()
    {
        TerrainPalette p = Variant("c", "rustbelt");
        int s = p.TexelCells;
        foreach (OverlayKind kind in new[] { OverlayKind.Ashfall, OverlayKind.Pitchblende })
        {
            TerrainBake.Result empty = TerrainBake.Bake(Cell(OverlayKind.None, 0), p);
            TerrainPalette.FieldLook look = kind == OverlayKind.Ashfall ? p.Style.Ashfall! : p.Style.Pitchblende!;
            int sparse = Heap(TerrainBake.Bake(Cell(kind, 20), p), look, p, s);
            int rich = Heap(TerrainBake.Bake(Cell(kind, 250), p), look, p, s);

            Assert.True(sparse > 0, $"{kind}: a sparse field is still drawn");
            Assert.True(rich > sparse, $"{kind}: rich ({rich}) covers more than sparse ({sparse})");

            // nothing outside the field cell changes
            TerrainBake.Result field = TerrainBake.Bake(Cell(kind, 250), p);
            for (int ty = 0; ty < field.Height; ty++)
            {
                for (int tx = 0; tx < field.Width; tx++)
                {
                    if (tx / s == 1 && ty / s == 1) continue;
                    Assert.Equal(Px(empty, tx, ty), Px(field, tx, ty));
                }
            }
        }
    }

    [Fact]
    public void FieldRimIsDarkerThanTheGroundItMeets()
    {
        TerrainPalette p = Variant("c", "rustbelt");
        int s = p.TexelCells;
        TerrainBake.Result field = TerrainBake.Bake(Cell(OverlayKind.Ashfall, 255), p);
        TerrainBake.Result empty = TerrainBake.Bake(Cell(OverlayKind.None, 0), p);

        int darker = 0;
        for (int tx = s; tx < 2 * s; tx++)
        {
            if (TerrainPalette.Luminance(Px(field, tx, s)) < TerrainPalette.Luminance(Px(empty, tx, s))) darker++;
        }

        Assert.True(darker >= s - 2, $"the field's top rim row is darker than plain ground ({darker}/{s})");
    }

    [Fact]
    public void MinimapUsesTheResourceColourWhenThePaletteHasOne()
    {
        TerrainPalette c = Variant("c", "rustbelt");
        TerrainPalette plain = TerrainPalette.Load(RepoFile("data/theaters/rustbelt.toml"));

        var rich = Rts.Game.Ui.Minimap.TerrainColour(c, LandType.Ash, OverlayKind.Ashfall, 255);
        var bare = Rts.Game.Ui.Minimap.TerrainColour(c, LandType.Ash, OverlayKind.None, 0);
        var old = Rts.Game.Ui.Minimap.TerrainColour(plain, LandType.Ash, OverlayKind.Ashfall, 255);

        Assert.Equal(c.MinimapAshfall!.Value.R, rich.R);
        Assert.NotEqual(bare, rich);
        Assert.NotEqual(rich, old);
        // sparse is dimmer than rich but still clearly not the bare ground
        var sparse = Rts.Game.Ui.Minimap.TerrainColour(c, LandType.Ash, OverlayKind.Ashfall, 10);
        Assert.NotEqual(bare, sparse);
        Assert.True(sparse.R < rich.R || sparse.G < rich.G);
    }

    /// <summary>Texels in the field cell painted with one of the deposit colours.</summary>
    private static int Heap(TerrainBake.Result r, TerrainPalette.FieldLook look, TerrainPalette p, int s)
    {
        int n = 0;
        for (int ty = s; ty < 2 * s; ty++)
        {
            for (int tx = s; tx < 2 * s; tx++)
            {
                TerrainPalette.Rgb c = Px(r, tx, ty);
                if (c == look.Body || c == look.Light || c == look.Shade || c == p.PitchGlint) n++;
            }
        }

        return n;
    }

    private static TerrainPalette.Rgb Px(TerrainBake.Result r, int x, int y)
    {
        int o = (y * r.Width + x) * 4;
        return new TerrainPalette.Rgb(r.Rgba[o], r.Rgba[o + 1], r.Rgba[o + 2]);
    }

    /// <summary>Look B's rust belt palette without its edge lines, shore and cliff options (tone and texel size unchanged).</summary>
    private static TerrainPalette WithoutStyle(TerrainPalette variant)
    {
        string[] drop = { "road_edge", "shore", "cliff_top", "cliff_shadow" };
        IEnumerable<string> lines = File.ReadAllText(RepoFile("data/theaters/variants/b/rustbelt.toml")).Replace("\r\n", "\n").Split('\n')
            .Where(l => !drop.Any(d => l.StartsWith(d, StringComparison.Ordinal)));
        TerrainPalette p = TerrainPalette.FromToml(string.Join("\n", lines));
        Assert.False(p.Style.HasEdgeEffects);
        Assert.True(p.Style.ToneAmp > 0);
        return p;
    }

    private static TerrainPalette Variant(string look, string file) => TerrainPalette.Load(RepoFile($"data/theaters/variants/{look}/{file}.toml"));

    private static MapData Cell(OverlayKind overlay, int density)
    {
        const int n = 4;
        var ovl = new OverlayKind[n * n];
        var dens = new byte[n * n];
        ovl[n + 1] = overlay;
        dens[n + 1] = (byte)density;
        var land = new LandType[n * n];
        Array.Fill(land, LandType.Ash);
        return new MapData("test", "rustbelt", n, n, new ushort[n * n], land, new byte[n * n], ovl, dens, new[] { new CellCoord(0, 0) });
    }

    private static MapData Grid(int n, LandType fill, params (int X, int Y, LandType Type)[] cells)
    {
        var land = new LandType[n * n];
        Array.Fill(land, fill);
        foreach ((int x, int y, LandType t) in cells) land[y * n + x] = t;
        return new MapData("test", "rustbelt", n, n, new ushort[n * n], land, new byte[n * n], new OverlayKind[n * n], new byte[n * n], new[] { new CellCoord(0, 0) });
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
