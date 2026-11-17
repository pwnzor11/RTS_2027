// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M12_16;

/// <summary>M12-16b: a destroyed prop's cell is repainted on the ground texture and the minimap, and nothing else is rebaked.</summary>
public sealed class RedrawDestroyedCellTests
{
    private const int W = 16, H = 16;
    private const int Cell = 8 * W + 8;   // the tree (8, 8)

    /// <summary>A flat map with a tree at (8, 8) on Clear land and a Road cell to its west, so the edge blends matter.</summary>
    private static MapData Start()
    {
        var land = new LandType[W * H];
        Array.Fill(land, LandType.Clear);
        land[8 * W + 7] = LandType.Road;
        var overlay = new OverlayKind[W * H];
        overlay[Cell] = OverlayKind.Tree;
        return Build(land, overlay);
    }

    private static MapData Build(LandType[] land, OverlayKind[] overlay) =>
        new("redraw", "rust_belt", W, H, new ushort[W * H], land, new byte[W * H], overlay, new byte[W * H],
            MapData.CreateFlat(W, H, "redraw").StartPositions);

    /// <summary>The live sim after a tree at <see cref="Cell"/> is crushed: what <c>DestroyProp</c> does.</summary>
    private static MapState Destroyed(MapData start)
    {
        var live = new MapState(start);
        live.SetOverlay(new CellCoord(8, 8), OverlayKind.None, 0);
        live.SetLand(new CellCoord(8, 8), LandType.Rough);
        return live;
    }

    /// <summary>The same map as <see cref="Destroyed"/> leaves it, built from scratch for a full rebake.</summary>
    private static MapData AfterDestroy()
    {
        var land = new LandType[W * H];
        Array.Fill(land, LandType.Clear);
        land[8 * W + 7] = LandType.Road;
        land[Cell] = LandType.Rough;
        return Build(land, new OverlayKind[W * H]);
    }

    [Fact]
    public void TheMinimapPixel_OfADestroyedTree_BecomesRough_AndLosesItsDot()
    {
        TerrainPalette palette = TerrainLook.Palette;
        MapData start = Start();
        var terrain = new MinimapTerrain(start, palette);
        Color dot = Minimap.TerrainColour(palette, LandType.Clear, OverlayKind.Tree, 0);
        Assert.Equal(dot, terrain.Pixels[Cell]);

        IReadOnlyList<int> changed = terrain.Sync(Destroyed(start));

        Assert.Equal(new[] { Cell }, changed.ToArray());
        TerrainPalette.Rgb rough = palette.ShadesFor(LandType.Rough).Base;
        Assert.Equal(new Color(rough.R, rough.G, rough.B), terrain.Pixels[Cell]);
        Assert.NotEqual(dot, terrain.Pixels[Cell]);
        Assert.Equal(Minimap.TerrainColour(palette, LandType.Road, OverlayKind.None, 0), terrain.Pixels[8 * W + 7]);   // untouched neighbour
        Assert.Empty(terrain.Sync(Destroyed(start)));                                                                // nothing new the second time
    }

    [Fact]
    public void TheGroundBake_ListsTheDestroyedCell_AndItsRepaintedRegionMatchesAFullRebake()
    {
        TerrainPalette palette = TerrainLook.Palette;
        MapData start = Start();
        TerrainBake.Result before = TerrainBake.Bake(start, palette);
        var shown = CellGrid.FromMap(start);
        var changed = new List<int>();

        Assert.Equal(1, shown.Collect(Destroyed(start), changed));
        Assert.Equal(new[] { Cell }, changed);
        Assert.Equal(0, shown.Collect(Destroyed(start), new List<int>()));

        int s = palette.TexelCells, tw = before.Width;
        byte[] rgba = (byte[])before.Rgba.Clone();
        List<int> region = CellGrid.RepaintSet(W, H, changed);
        Assert.Contains(8 * W + 7, region);                       // the road west of it: its edge texels read this cell
        Assert.Equal(9, region.Count);
        foreach (int i in region)
        {
            int cx = i % W, cy = i / W;
            byte[] cellTexels = TerrainBake.PaintCell(palette, shown, cx, cy);
            for (int sy = 0; sy < s; sy++)
                for (int sx = 0; sx < s; sx++)
                    Array.Copy(cellTexels, (sy * s + sx) * 4, rgba, ((cy * s + sy) * tw + cx * s + sx) * 4, 4);
        }

        TerrainBake.Result full = TerrainBake.Bake(AfterDestroy(), palette);
        Assert.NotEqual(before.Rgba, full.Rgba);                  // the destroyed cell really changed the texture
        Assert.Equal(full.Rgba, rgba);
    }
}
