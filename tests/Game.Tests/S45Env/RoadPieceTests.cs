// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Tests.Menu;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S45Env;

/// <summary>M16-V-04: road neighbour mask to piece, the plan of cells that need art, the rectangles drawn for them, and a headless proof picture.</summary>
public sealed class RoadPieceTests
{
    private const int N = RoadPieces.North, E = RoadPieces.East, S = RoadPieces.South, W = RoadPieces.West;
    private static readonly TerrainPalette.Rgb Road = new(0x4E, 0x4A, 0x42), Edge = new(0x20, 0x1E, 0x1A), Grass = new(0x23, 0x27, 0x1E);

    [Theory]
    [InlineData(0, RoadPiece.Isolated)]
    [InlineData(N, RoadPiece.EndCap)]
    [InlineData(E, RoadPiece.EndCap)]
    [InlineData(S, RoadPiece.EndCap)]
    [InlineData(W, RoadPiece.EndCap)]
    [InlineData(N | S, RoadPiece.Straight)]
    [InlineData(E | W, RoadPiece.Straight)]
    [InlineData(N | E, RoadPiece.Corner)]
    [InlineData(S | W, RoadPiece.Corner)]
    [InlineData(N | E | S, RoadPiece.TJunction)]
    [InlineData(E | S | W, RoadPiece.TJunction)]
    [InlineData(N | E | S | W, RoadPiece.Cross)]
    public void Mask_picks_the_piece(int mask, RoadPiece expected) => Assert.Equal(expected, RoadPieces.Classify((byte)mask));

    // 16 x 16 clear ground: a long road across row 5, a long one down column 7 (an X at 7,5), a branch down column 3 (a T at 3,5), a lone cell.
    private static LandType[] Sample(out int w, out int h)
    {
        w = h = 16;
        var land = new LandType[w * h];
        Array.Fill(land, LandType.Clear);
        for (int x = 1; x <= 12; x++) land[5 * w + x] = LandType.Road;
        for (int y = 1; y <= 9; y++) land[y * w + 7] = LandType.Road;
        for (int y = 5; y <= 9; y++) land[y * w + 3] = LandType.Road;
        land[12 * w + 10] = LandType.Road;
        return land;
    }

    [Fact]
    public void Mask_reads_the_four_neighbours_and_treats_the_map_edge_as_no_road()
    {
        LandType[] land = Sample(out int w, out int h);
        Assert.Equal(N | E | S | W, RoadPieces.Mask(land, w, h, 7, 5));
        Assert.Equal(E | S | W, RoadPieces.Mask(land, w, h, 3, 5));
        Assert.Equal(E, RoadPieces.Mask(land, w, h, 1, 5));
        Assert.Equal(0, RoadPieces.Mask(land, w, h, 10, 12));
        var edge = new LandType[4];
        Array.Fill(edge, LandType.Road);
        Assert.Equal(E | S, RoadPieces.Mask(edge, 2, 2, 0, 0));
    }

    [Fact]
    public void Plan_lists_only_cells_that_need_art()
    {
        LandType[] land = Sample(out int w, out int h);
        List<RoadCell> plan = RoadPieces.Plan(land, w, h);
        Assert.Contains(new RoadCell(7, 5, RoadPiece.Cross, N | E | S | W), plan);
        Assert.Contains(new RoadCell(3, 5, RoadPiece.TJunction, E | S | W), plan);
        Assert.Contains(new RoadCell(1, 5, RoadPiece.EndCap, E), plan);
        Assert.Contains(new RoadCell(12, 5, RoadPiece.EndCap, W), plan);
        Assert.Contains(new RoadCell(7, 1, RoadPiece.EndCap, S), plan);
        Assert.Contains(new RoadCell(10, 12, RoadPiece.Isolated, 0), plan);
        Assert.DoesNotContain(plan, c => c.Piece is RoadPiece.Straight or RoadPiece.Corner);
        Assert.Equal(5, plan.Count(c => c.Piece == RoadPiece.EndCap));   // (1,5) (12,5) (7,1) (7,9) (3,9)
        Assert.Equal(8, plan.Count);
    }

    private static List<PieceRect> OpsFor(LandType[] land, int w, int h, RoadCell cell)
    {
        var ops = new List<PieceRect>();
        RoadPieces.Ops(cell, land, w, h, 16, Road, Edge, 1, _ => Grass, ops);
        return ops;
    }

    private static int CellOf(PieceRect r, out int cx, out int cy)
    {
        cx = r.X / Camera.CellPx;
        cy = r.Y / Camera.CellPx;
        return cy * 1000 + cx;
    }

    [Fact]
    public void Cross_flares_into_all_four_diagonal_cells()
    {
        LandType[] land = Sample(out int w, out int h);
        var cells = new HashSet<(int, int)>();
        foreach (PieceRect r in OpsFor(land, w, h, new RoadCell(7, 5, RoadPiece.Cross, N | E | S | W)))
        {
            CellOf(r, out int cx, out int cy);
            cells.Add((cx, cy));
            Assert.Equal(Camera.CellPx / 16, r.W);
        }

        Assert.Equal(new HashSet<(int, int)> { (6, 4), (8, 4), (6, 6), (8, 6) }, cells);
    }

    [Fact]
    public void T_junction_flares_only_on_the_side_it_branches_to()
    {
        LandType[] land = Sample(out int w, out int h);
        var cells = new HashSet<(int, int)>();
        foreach (PieceRect r in OpsFor(land, w, h, new RoadCell(3, 5, RoadPiece.TJunction, E | S | W)))
        {
            CellOf(r, out int cx, out int cy);
            cells.Add((cx, cy));
        }

        Assert.Equal(new HashSet<(int, int)> { (2, 6), (4, 6) }, cells);   // south-west and south-east; the north side is open ground
    }

    [Fact]
    public void Flare_never_spreads_onto_water_cliff_or_off_the_map()
    {
        LandType[] land = Sample(out int w, out int h);
        land[4 * w + 6] = LandType.Water;
        land[4 * w + 8] = LandType.Cliff;
        land[6 * w + 6] = LandType.Rock;
        var cells = new HashSet<(int, int)>();
        foreach (PieceRect r in OpsFor(land, w, h, new RoadCell(7, 5, RoadPiece.Cross, N | E | S | W)))
        {
            CellOf(r, out int cx, out int cy);
            cells.Add((cx, cy));
        }

        Assert.Equal(new HashSet<(int, int)> { (8, 6) }, cells);

        var tiny = new[] { LandType.Road, LandType.Road, LandType.Road, LandType.Road };   // a 2 x 2 block has no junction cells, nothing to draw
        Assert.Empty(RoadPieces.Plan(tiny, 2, 2));
    }

    [Fact]
    public void Dead_end_rounds_its_two_far_corners_and_only_those()
    {
        LandType[] land = Sample(out int w, out int h);
        // (7,1) is a dead end whose road continues south: the north-west and north-east corners are rounded.
        List<PieceRect> ops = OpsFor(land, w, h, new RoadCell(7, 1, RoadPiece.EndCap, S));
        Assert.NotEmpty(ops);
        foreach (PieceRect r in ops)
        {
            CellOf(r, out int cx, out int cy);
            Assert.Equal((7, 1), (cx, cy));
            int ly = r.Y - cy * Camera.CellPx;
            Assert.True(ly < Camera.CellPx / 2, "only the open (north) half is touched");
        }

        bool west = ops.Any(r => r.X - 7 * Camera.CellPx < Camera.CellPx / 2), east = ops.Any(r => r.X - 7 * Camera.CellPx >= Camera.CellPx / 2);
        Assert.True(west && east);
        Assert.Contains(ops, r => r.Colour == Grass && r.Alpha == 255);   // cut texels take the neighbouring ground
        Assert.Contains(ops, r => r.Colour == Edge);                      // and the arc takes the road's edge line
        Assert.DoesNotContain(ops, r => r.Colour == Road);
    }

    [Fact]
    public void Lone_road_cell_is_rounded_on_all_four_corners()
    {
        LandType[] land = Sample(out int w, out int h);
        List<PieceRect> ops = OpsFor(land, w, h, new RoadCell(10, 12, RoadPiece.Isolated, 0));
        int cell = Camera.CellPx;
        bool Corner(Func<int, int, bool> f) => ops.Any(r => f(r.X - 10 * cell, r.Y - 12 * cell));
        Assert.True(Corner((x, y) => x < cell / 2 && y < cell / 2));
        Assert.True(Corner((x, y) => x >= cell / 2 && y < cell / 2));
        Assert.True(Corner((x, y) => x < cell / 2 && y >= cell / 2));
        Assert.True(Corner((x, y) => x >= cell / 2 && y >= cell / 2));
    }

    [Fact]
    public void Ops_are_repeatable_and_stay_on_the_texel_grid()
    {
        LandType[] land = Sample(out int w, out int h);
        foreach (RoadCell c in RoadPieces.Plan(land, w, h))
        {
            List<PieceRect> a = OpsFor(land, w, h, c), b = OpsFor(land, w, h, c);
            Assert.Equal(a, b);
            Assert.All(a, r => Assert.True(r.X % 4 == 0 && r.Y % 4 == 0 && r.W == 4 && r.H == 4));
        }
    }

    [Fact]
    public void Proof_picture_junctions_and_dead_ends_on_the_real_bake()
    {
        LandType[] land = Sample(out int w, out int h);
        var map = new MapData("roads", "rustbelt", w, h, new ushort[w * h], land, new byte[w * h], new OverlayKind[w * h],
            new byte[w * h], new[] { new CellCoord(0, 0) });
        TerrainPalette palette = TerrainPalette.ForMap(map);
        TerrainBake.Result baked = TerrainBake.Bake(map, palette);
        byte[] after = (byte[])baked.Rgba.Clone();
        var (road, edge, edgeT) = RoadPieceLayer.Colours(palette);
        int s = palette.TexelCells, unit = Camera.CellPx / s;
        var ops = new List<PieceRect>();
        foreach (RoadCell c in RoadPieces.Plan(map.Land, w, h))
            RoadPieces.Ops(c, map.Land, w, h, s, road, edge, edgeT, l => palette.ShadesFor(l).Base, ops);
        Assert.NotEmpty(ops);
        foreach (PieceRect r in ops)
        {
            int tx = r.X / unit, ty = r.Y / unit;
            int o = (ty * baked.Width + tx) * 4;
            for (int c3 = 0; c3 < 3; c3++)
            {
                int src = c3 == 0 ? r.Colour.R : c3 == 1 ? r.Colour.G : r.Colour.B;
                after[o + c3] = (byte)((after[o + c3] * (255 - r.Alpha) + src * r.Alpha) / 255);
            }
        }

        Assert.NotEqual(baked.Rgba, after);
        string? root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "RTS_2027.slnx"))) root = Path.GetDirectoryName(root);
        if (root is null) return;
        Directory.CreateDirectory(Path.Combine(root, "scratch"));
        // enlarge 6x so the texel steps are visible
        const int k = 6;
        byte[] Up(byte[] src)
        {
            var big = new byte[baked.Width * k * baked.Height * k * 4];
            for (int y = 0; y < baked.Height * k; y++)
                for (int x = 0; x < baked.Width * k; x++)
                    Array.Copy(src, ((y / k) * baked.Width + x / k) * 4, big, (y * baked.Width * k + x) * 4, 4);
            return big;
        }

        File.WriteAllBytes(Path.Combine(root, "scratch", "fxb_roads_after.png"), Png.Encode(Up(after), baked.Width * k, baked.Height * k));
        File.WriteAllBytes(Path.Combine(root, "scratch", "fxb_roads_before.png"), Png.Encode(Up(baked.Rgba), baked.Width * k, baked.Height * k));
    }
}
