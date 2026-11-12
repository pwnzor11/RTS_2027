// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Render;
using Rts.Game.Screens;
using Rts.Game.Ui;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M10;

public sealed class MapPreviewTests
{
    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");

    [Fact]
    public void A_small_map_is_enlarged_by_a_whole_factor()
    {
        MapData map = MapData.CreateFlat(32, 24);
        MapPreviewImage image = MapPreview.Render(map, 96);
        Assert.Equal(96, image.Width);
        Assert.Equal(72, image.Height);
        Assert.Equal(image.Width * image.Height * 4, image.Rgba.Length);
    }

    [Fact]
    public void A_large_map_is_sampled_down_to_the_box()
    {
        MapData map = MapData.CreateFlat(128, 64);
        MapPreviewImage image = MapPreview.Render(map, 96);
        Assert.Equal(96, image.Width);
        Assert.Equal(48, image.Height);
    }

    [Fact]
    public void The_same_map_gives_the_same_bytes()
    {
        MapData map = MapGenerator.Generate(new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 7));
        Assert.Equal(MapPreview.Render(map).Rgba, MapPreview.Render(map).Rgba);
    }

    [Fact]
    public void Terrain_colours_follow_the_minimap_and_pixels_are_opaque()
    {
        int n = 16 * 16;
        var land = new LandType[n];
        Array.Fill(land, LandType.Water);
        var map = new MapData("w", "rust_belt", 16, 16, new ushort[n], land, new byte[n], new OverlayKind[n], new byte[n], new List<CellCoord>());
        MapPreviewImage image = MapPreview.Render(map, 16);
        var expected = Minimap.TerrainColour(TerrainPalette.ForMap(map), LandType.Water, OverlayKind.None, 0);
        uint want = ((uint)expected.R << 24) | ((uint)expected.G << 16) | ((uint)expected.B << 8) | 255;
        Assert.Equal(want, image.Pixel(3, 3));
        for (int i = 3; i < image.Rgba.Length; i += 4) Assert.Equal(255, image.Rgba[i]);
    }

    [Fact]
    public void A_resource_field_changes_the_pixels_of_its_cells()
    {
        MapData plain = MapData.CreateFlat(16, 16);
        var overlay = new OverlayKind[256];
        var density = new byte[256];
        overlay[8 * 16 + 8] = OverlayKind.Ashfall;
        density[8 * 16 + 8] = 255;
        var rich = new MapData("r", "rust_belt", 16, 16, new ushort[256], new LandType[256], new byte[256], overlay, density, plain.StartPositions);
        Assert.NotEqual(MapPreview.Render(plain, 16, null).Pixel(8, 8), MapPreview.Render(rich, 16, null).Pixel(8, 8));
        Assert.Equal(MapPreview.Render(plain, 16, null).Pixel(12, 4), MapPreview.Render(rich, 16, null).Pixel(12, 4));
    }

    [Fact]
    public void Start_positions_are_marked_and_numbered_in_order()
    {
        MapData map = MapData.CreateFlat(32, 32);   // starts at (2,2) and (29,29)
        MapPreviewImage image = MapPreview.Render(map, 64, null);
        MapPreviewImage bare = MapPreview.Render(new MapData("b", "rust_belt", 32, 32, new ushort[1024], new LandType[1024], new byte[1024], new OverlayKind[1024], new byte[1024], new List<CellCoord>()), 64, null);
        int differing = 0;
        for (int i = 0; i < image.Rgba.Length; i++) if (image.Rgba[i] != bare.Rgba[i]) differing++;
        Assert.True(differing > 0);

        // The two marks have different digits (1 and 2): compare the white-pixel patterns in their boxes.
        int Whites(int x0, int y0)
        {
            int n = 0;
            for (int y = y0; y < y0 + 9; y++)
                for (int x = x0; x < x0 + 5; x++)
                    if (image.Pixel(x, y) == 0xFFFFFFFF) n++;
            return n;
        }

        Assert.True(Whites(0, 0) > 0);
        Assert.NotEqual(Whites(0, 0), Whites(image.Width - 5, image.Height - 9));   // "1" has 8 lit pixels, "2" has 11
    }

    [Fact]
    public void Markers_stay_inside_the_picture_for_a_corner_start()
    {
        int n = 16 * 16;
        var map = new MapData("c", "rust_belt", 16, 16, new ushort[n], new LandType[n], new byte[n], new OverlayKind[n], new byte[n],
            new[] { new CellCoord(0, 0), new CellCoord(15, 15) });
        MapPreviewImage image = MapPreview.Render(map, 16, null);
        Assert.Equal(16, image.Width);
    }

    [Fact]
    public void The_cache_returns_the_same_picture_for_the_same_generated_choice_and_a_new_one_for_a_new_seed()
    {
        MapPreviewCache.Clear();
        var a = new MapChoice(null, new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 11));
        var b = new MapChoice(null, new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 12));
        MapPreviewImage? first = MapPreviewCache.For(RulesDir, a);
        Assert.NotNull(first);
        Assert.Same(first, MapPreviewCache.For(RulesDir, a));
        MapPreviewImage? other = MapPreviewCache.For(RulesDir, b);
        Assert.NotNull(other);
        Assert.NotSame(first, other);
        Assert.Equal(2, MapPreviewCache.Count);
    }

    [Fact]
    public void The_cache_reads_a_curated_map_file_and_gives_null_for_a_missing_one()
    {
        MapPreviewCache.Clear();
        Assert.NotNull(MapPreviewCache.For(RulesDir, new MapChoice(SkirmishSettings.CuratedFile, null)));
        Assert.Null(MapPreviewCache.For(RulesDir, new MapChoice("no_such_map.map.toml", null)));
    }

    [Fact]
    public void The_fit_rectangle_keeps_the_shape_and_centres_it()
    {
        var r = MapPreviewView.Fit(new Microsoft.Xna.Framework.Rectangle(0, 0, 100, 100), 96, 48);
        Assert.Equal(100, r.Width);
        Assert.Equal(50, r.Height);
        Assert.Equal(25, r.Y);
    }
}
