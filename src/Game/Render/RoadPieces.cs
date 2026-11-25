// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// Road junction and end-cap pieces (design 18 section 3, M16-V-04 part 2). The baked terrain draws a road as flat cells with an edge line, so a
/// T or X junction is a square crossing and a dead end stops square. This picks the piece from the 4-neighbour road mask and draws, over the
/// bake, a flare in each inside corner of a junction and rounded corners on a dead end, from the road's own palette colours and texel grid.
/// Pure maths: it returns rectangles and never touches the map data, the generator or the sim. View only.
/// </summary>
public static class RoadPieces
{
    /// <summary>Road neighbour to the north.</summary>
    public const byte North = 1;

    /// <summary>Road neighbour to the east.</summary>
    public const byte East = 2;

    /// <summary>Road neighbour to the south.</summary>
    public const byte South = 4;

    /// <summary>Road neighbour to the west.</summary>
    public const byte West = 8;

    private const ulong Seed = 0x524F_4144UL;

    /// <summary>True for the road land type.</summary>
    public static bool IsRoad(LandType land) => land == LandType.Road;

    /// <summary>Ground a junction flare may spread onto: plain open land only, never water, cliff, rock or another road.</summary>
    public static bool CanFlareOnto(LandType land) => land is LandType.Clear or LandType.Rough or LandType.Ash;

    /// <summary>The road neighbours of cell (<paramref name="x"/>, <paramref name="y"/>) as a mask; off-map counts as no road.</summary>
    public static byte Mask(IReadOnlyList<LandType> land, int width, int height, int x, int y)
    {
        byte m = 0;
        if (RoadAt(land, width, height, x, y - 1)) m |= North;
        if (RoadAt(land, width, height, x + 1, y)) m |= East;
        if (RoadAt(land, width, height, x, y + 1)) m |= South;
        if (RoadAt(land, width, height, x - 1, y)) m |= West;
        return m;
    }

    /// <summary>The piece for a neighbour mask.</summary>
    public static RoadPiece Classify(byte mask)
    {
        int n = System.Numerics.BitOperations.PopCount((uint)(mask & 15));
        return n switch
        {
            0 => RoadPiece.Isolated,
            1 => RoadPiece.EndCap,
            2 => mask is (North | South) or (East | West) ? RoadPiece.Straight : RoadPiece.Corner,
            3 => RoadPiece.TJunction,
            _ => RoadPiece.Cross,
        };
    }

    /// <summary>Every road cell that needs art: dead ends, lone cells, T and X junctions, in row-major order. Straights and corners need none.</summary>
    public static List<RoadCell> Plan(IReadOnlyList<LandType> land, int width, int height)
    {
        var cells = new List<RoadCell>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsRoad(land[y * width + x])) continue;
                byte m = Mask(land, width, height, x, y);
                RoadPiece p = Classify(m);
                if (p is RoadPiece.Isolated or RoadPiece.EndCap or RoadPiece.TJunction or RoadPiece.Cross) cells.Add(new RoadCell(x, y, p, m));
            }
        }

        return cells;
    }

    /// <summary>
    /// The rectangles for one planned cell. <paramref name="texelCells"/> is the terrain bake's texels per cell (the art is drawn on that grid),
    /// <paramref name="road"/> / <paramref name="edge"/> the road's own colours, <paramref name="edgeTexels"/> its edge line width, and
    /// <paramref name="ground"/> gives the colour of a neighbouring land type for the cut corners of a dead end.
    /// </summary>
    public static void Ops(RoadCell cell, IReadOnlyList<LandType> land, int width, int height, int texelCells,
        TerrainPalette.Rgb road, TerrainPalette.Rgb edge, int edgeTexels, Func<LandType, TerrainPalette.Rgb> ground, List<PieceRect> ops)
    {
        int s = texelCells > 0 && Camera.CellPx % texelCells == 0 ? texelCells : 16;
        int u = Camera.CellPx / s;
        int radius = Math.Max(2, s / 5);
        int edgeT = Math.Clamp(edgeTexels, 1, radius - 1);
        switch (cell.Piece)
        {
            case RoadPiece.TJunction:
            case RoadPiece.Cross:
                for (int dy = -1; dy <= 1; dy += 2)
                {
                    for (int dx = -1; dx <= 1; dx += 2)
                    {
                        byte sideX = dx > 0 ? East : West, sideY = dy > 0 ? South : North;
                        if ((cell.Mask & sideX) == 0 || (cell.Mask & sideY) == 0) continue;
                        int nx = cell.X + dx, ny = cell.Y + dy;
                        if ((uint)nx >= (uint)width || (uint)ny >= (uint)height || !CanFlareOnto(land[ny * width + nx])) continue;
                        Flare(nx, ny, dx, dy, s, u, radius, edgeT, road, edge, ops);
                    }
                }

                break;
            case RoadPiece.EndCap:
            case RoadPiece.Isolated:
                for (int dy = -1; dy <= 1; dy += 2)
                {
                    for (int dx = -1; dx <= 1; dx += 2)
                    {
                        // A corner is rounded when neither of its two sides continues as road.
                        byte sideX = dx > 0 ? East : West, sideY = dy > 0 ? South : North;
                        if ((cell.Mask & (sideX | sideY)) != 0) continue;
                        LandType near = LandAtOr(land, width, height, cell.X + dx, cell.Y + dy, LandAtOr(land, width, height, cell.X + dx, cell.Y, LandType.Clear));
                        if (IsRoad(near)) near = LandType.Clear;
                        Round(cell.X, cell.Y, dx, dy, s, u, radius, edgeT, road, edge, ground(near), ops);
                    }
                }

                break;
        }
    }

    // A quarter disc of road colour spread into diagonal cell (cx, cy), centred on the corner it shares with the junction.
    private static void Flare(int cx, int cy, int dx, int dy, int s, int u, int radius, int edgeT, TerrainPalette.Rgb road, TerrainPalette.Rgb edge, List<PieceRect> ops)
    {
        for (int j = 0; j < radius; j++)
        {
            for (int i = 0; i < radius; i++)
            {
                int d2 = (2 * i + 1) * (2 * i + 1) + (2 * j + 1) * (2 * j + 1);
                if (d2 > 4 * radius * radius) continue;
                int inner = radius - edgeT;
                bool ring = d2 > 4 * inner * inner;
                int tx = dx > 0 ? i : s - 1 - i;     // the corner nearest the junction: left edge when the diagonal is to the east
                int ty = dy > 0 ? j : s - 1 - j;
                int wx = cx * Camera.CellPx + tx * u, wy = cy * Camera.CellPx + ty * u;
                ops.Add(ring ? new PieceRect(wx, wy, u, u, edge, 217) : new PieceRect(wx, wy, u, u, Tone(road, wx, wy), 255));
            }
        }
    }

    // Rounds one outer corner of a dead-end cell: texels outside the arc take the neighbouring ground, texels on the arc take the edge line.
    private static void Round(int cx, int cy, int dx, int dy, int s, int u, int radius, int edgeT, TerrainPalette.Rgb road, TerrainPalette.Rgb edge, TerrainPalette.Rgb ground, List<PieceRect> ops)
    {
        for (int j = 0; j < radius; j++)
        {
            for (int i = 0; i < radius; i++)
            {
                int ox = 2 * i + 1 - 2 * radius, oy = 2 * j + 1 - 2 * radius;   // from the arc's centre
                int d2 = ox * ox + oy * oy;
                int tx = dx > 0 ? s - 1 - i : i;                                  // i, j count in from the cell's own corner
                int ty = dy > 0 ? s - 1 - j : j;
                int wx = cx * Camera.CellPx + tx * u, wy = cy * Camera.CellPx + ty * u;
                int inner = radius - edgeT;
                if (d2 > 4 * radius * radius) ops.Add(new PieceRect(wx, wy, u, u, Tone(ground, wx, wy), 255));
                else if (d2 > 4 * inner * inner) ops.Add(new PieceRect(wx, wy, u, u, edge, 217));
            }
        }
    }

    // A little texel-to-texel variation (Mix of the integer hash), so a patch is not a flat colour against the noisy bake.
    private static TerrainPalette.Rgb Tone(TerrainPalette.Rgb c, int wx, int wy)
    {
        uint h = DecorHash.Mix(Seed, wx, wy, 5);
        int d = (h & 7) == 0 ? 6 : (h & 7) == 1 ? -6 : 0;
        return new TerrainPalette.Rgb((byte)Math.Clamp(c.R + d, 0, 255), (byte)Math.Clamp(c.G + d, 0, 255), (byte)Math.Clamp(c.B + d, 0, 255));
    }

    private static bool RoadAt(IReadOnlyList<LandType> land, int width, int height, int x, int y) =>
        (uint)x < (uint)width && (uint)y < (uint)height && IsRoad(land[y * width + x]);

    private static LandType LandAtOr(IReadOnlyList<LandType> land, int width, int height, int x, int y, LandType fallback) =>
        (uint)x < (uint)width && (uint)y < (uint)height ? land[y * width + x] : fallback;
}
