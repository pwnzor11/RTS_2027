// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;

namespace Rts.Game.Tests.Render;

/// <summary>S15 art-packs pilot: the data-backed prop atlas keeps the IPropAtlas contract and falls back to the procedural art.</summary>
public sealed class LpcPropAtlasTests
{
    private static string? PropsDir()
    {
        string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "sprites", "props"));
        return Directory.Exists(dir) ? dir : null;
    }

    [Theory]
    [InlineData("rustbelt")]
    [InlineData("glass_coast")]
    public void Data_atlas_holds_the_contract_and_the_real_frames(string theater)
    {
        string? dir = PropsDir();
        if (dir is null) return;
        TerrainPalette pal = TerrainPalette.Load(theater);
        IPropAtlas atlas = LpcPropAtlas.Create(pal, dir);
        var lpc = Assert.IsType<LpcPropAtlas>(atlas);
        Assert.Equal(atlas.Width * atlas.Height * 4, atlas.Rgba.Length);
        Assert.Equal(12, lpc.DataFrames.Count);
        foreach (PropKind kind in new[] { PropKind.Boulder, PropKind.Rubble, PropKind.DeadTree, PropKind.ConcreteBlock })
            for (int v = 0; v < PropArt.Variants; v++) Assert.Contains((kind, v), lpc.DataFrames);

        foreach (PropKind kind in Enum.GetValues<PropKind>())
            for (int v = 0; v < atlas.VariantCount(kind); v++)
            {
                PropRect r = atlas.Frame(kind, v);
                Assert.InRange(r.X + r.Width, 1, atlas.Width);
                Assert.InRange(r.Y + r.Height, 1, atlas.Height);
                Assert.InRange(r.OriginX, 0, r.Width);
                Assert.InRange(r.OriginY, 0, r.Height);
                int solid = 0;
                for (int y = 0; y < r.Height; y++)
                    for (int x = 0; x < r.Width; x++) if (atlas.Rgba[((r.Y + y) * atlas.Width + r.X + x) * 4 + 3] > 200) solid++;
                Assert.True(solid >= 60, $"{kind}/{v}: {solid} solid pixels");
                Assert.Equal(r, atlas.Frame(kind, v + atlas.VariantCount(kind)));
            }

        // kinds without LPC art are still the procedural pixels
        PropRect crest = atlas.Frame(PropKind.GlassCrest, 1);
        byte[] direct = PropArt.Render(PropKind.GlassCrest, 1, pal);
        for (int y = 0; y < crest.Height; y++)
            Assert.True(direct.AsSpan(y * crest.Width * 4, crest.Width * 4).SequenceEqual(atlas.Rgba.AsSpan(((crest.Y + y) * atlas.Width + crest.X) * 4, crest.Width * 4)));
    }

    [Fact]
    public void Pixels_are_premultiplied_and_larger_variants_cover_more()
    {
        string? dir = PropsDir();
        if (dir is null) return;
        IPropAtlas atlas = LpcPropAtlas.Create(TerrainPalette.Load("rustbelt"), dir);
        foreach (PropKind kind in new[] { PropKind.Boulder, PropKind.DeadTree })
        {
            int[] area = new int[PropArt.Variants];
            for (int v = 0; v < PropArt.Variants; v++)
            {
                PropRect r = atlas.Frame(kind, v);
                for (int y = 0; y < r.Height; y++)
                    for (int x = 0; x < r.Width; x++)
                    {
                        int i = ((r.Y + y) * atlas.Width + r.X + x) * 4;
                        Assert.True(atlas.Rgba[i] <= atlas.Rgba[i + 3] && atlas.Rgba[i + 1] <= atlas.Rgba[i + 3] && atlas.Rgba[i + 2] <= atlas.Rgba[i + 3]);
                        if (atlas.Rgba[i + 3] > 0) area[v]++;
                    }
            }

            Assert.True(area[0] < area[1] && area[1] < area[2], $"{kind}: {string.Join(",", area)}");
        }
    }

    [Fact]
    public void Missing_or_broken_art_falls_back_to_the_placeholder_atlas()
    {
        TerrainPalette pal = TerrainPalette.Load("rustbelt");
        string empty = Path.Combine(Path.GetTempPath(), "rts_props_none_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            Assert.IsType<ProceduralPropAtlas>(LpcPropAtlas.Create(pal, empty));
            File.WriteAllText(Path.Combine(empty, "lpc_props.props.toml"), "file = \"nope.png\"\n");
            Assert.IsType<ProceduralPropAtlas>(LpcPropAtlas.Create(pal, empty));
        }
        finally { Directory.Delete(empty, true); }
    }

    [Fact]
    public void Tint_pulls_toward_the_land_ramp_but_keeps_brightness_order()
    {
        TerrainPalette pal = TerrainPalette.Load("glass_coast");
        TerrainPalette.LandShades ramp = pal.ShadesFor(Rts.Sim.Rules.LandType.Rough);
        var dark = LpcPropAtlas.Tinted(new TerrainPalette.Rgb(40, 40, 50), ramp);
        var light = LpcPropAtlas.Tinted(new TerrainPalette.Rgb(190, 190, 200), ramp);
        Assert.True(TerrainPalette.Luminance(light) > TerrainPalette.Luminance(dark));
    }
}
