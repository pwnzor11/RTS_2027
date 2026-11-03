// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// Which wall sprite state a wall block shows (D-072). Render-only: the sim has no notion of wall shapes. The state
/// comes from a 4-neighbour mask of the owner's own walls; the letters in a state name are the directions the wall
/// continues in (<c>end_n</c> continues north only, <c>corner_se</c> south and east, <c>t_n</c> north, east and west,
/// <c>post</c> nowhere). Masks and results are cached per snapshot tick by <see cref="WallTiler"/>.
/// </summary>
public static class WallTiling
{
    /// <summary>Mask bit for a wall in the cell to the north.</summary>
    public const int North = 1;

    /// <summary>Mask bit for a wall in the cell to the east.</summary>
    public const int East = 2;

    /// <summary>Mask bit for a wall in the cell to the south.</summary>
    public const int South = 4;

    /// <summary>Mask bit for a wall in the cell to the west.</summary>
    public const int West = 8;

    /// <summary>Suffix of the damaged twin of every wall state.</summary>
    public const string DamagedSuffix = "_damaged";

    private static readonly string[] Names =
    {
        "post", "end_n", "end_e", "corner_ne",
        "end_s", "v", "corner_se", "t_e",
        "end_w", "corner_nw", "h", "t_n",
        "corner_sw", "t_w", "t_s", "cross",
    };

    /// <summary>The sprite state for a neighbour mask (bits <see cref="North"/>, <see cref="East"/>, <see cref="South"/>, <see cref="West"/>).</summary>
    /// <param name="mask">Neighbour mask, 0..15 (higher bits are ignored).</param>
    /// <param name="damaged">Whether to use the damaged twin.</param>
    public static string StateFor(int mask, bool damaged)
    {
        string name = Names[mask & 15];
        return damaged ? name + DamagedSuffix : name;
    }

    /// <summary>The neighbour mask of a cell given a test for "own wall here".</summary>
    /// <param name="cell">The wall's cell.</param>
    /// <param name="isWall">True when the given cell holds a wall of the same owner.</param>
    public static int MaskOf(CellCoord cell, Func<CellCoord, bool> isWall)
    {
        ArgumentNullException.ThrowIfNull(isWall);
        int mask = 0;
        if (isWall(new CellCoord(cell.X, cell.Y - 1))) mask |= North;
        if (isWall(new CellCoord(cell.X + 1, cell.Y))) mask |= East;
        if (isWall(new CellCoord(cell.X, cell.Y + 1))) mask |= South;
        if (isWall(new CellCoord(cell.X - 1, cell.Y))) mask |= West;
        return mask;
    }

    /// <summary>
    /// The sheet frame for a wall state, or -1 when the manifest lacks it (an older single-variant wall sheet), so the
    /// caller keeps the plain idle/damaged frame.
    /// </summary>
    /// <param name="manifest">The wall's manifest.</param>
    /// <param name="mask">Neighbour mask.</param>
    /// <param name="damaged">Whether the wall is damaged.</param>
    /// <param name="tick">Simulation tick.</param>
    public static int FrameFor(SpriteManifest manifest, int mask, bool damaged, int tick)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        string state = StateFor(mask, damaged);
        if (!SpriteStates.HasState(manifest, state)) return -1;
        return manifest.FrameIndex(state, 0, Math.Max(0, tick));
    }
}

/// <summary>
/// Per-snapshot cache of wall masks: the set of wall cells per owner is built once when the snapshot tick changes and
/// each wall's mask is computed on first use after that.
/// </summary>
public sealed class WallTiler
{
    private readonly HashSet<(byte Player, int X, int Y)> _cells = new();
    private readonly Dictionary<EntityId, int> _masks = new();
    private int _tick = int.MinValue;
    private int _count = -1;

    /// <summary>Rebuilds the cache when the snapshot changed (tick or building count). Cheap otherwise.</summary>
    /// <param name="tick">Snapshot tick.</param>
    /// <param name="buildings">Buildings in the snapshot.</param>
    /// <param name="isWallType">Whether a building type index is a wall.</param>
    public void Observe(int tick, IReadOnlyList<BuildingView> buildings, Func<int, bool> isWallType)
    {
        ArgumentNullException.ThrowIfNull(buildings);
        ArgumentNullException.ThrowIfNull(isWallType);
        if (tick == _tick && buildings.Count == _count) return;
        _tick = tick;
        _count = buildings.Count;
        _cells.Clear();
        _masks.Clear();
        foreach (BuildingView b in buildings)
        {
            if (isWallType(b.TypeIndex)) _cells.Add((b.Player, b.Cell.X, b.Cell.Y));
        }
    }

    /// <summary>The neighbour mask of one wall, cached until the next snapshot change.</summary>
    /// <param name="wall">A wall building from the observed snapshot.</param>
    public int Mask(BuildingView wall)
    {
        if (_masks.TryGetValue(wall.Id, out int cached)) return cached;
        byte player = wall.Player;
        int mask = WallTiling.MaskOf(wall.Cell, c => _cells.Contains((player, c.X, c.Y)));
        _masks[wall.Id] = mask;
        return mask;
    }
}
