// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// A copy of the per-cell inputs the terrain bake and the minimap read: land, overlay and density, row-major. A renderer keeps one as
/// "the state its pixels show". <see cref="Collect"/> compares it with the live sim and reports the cells that changed, so a destroyed
/// prop (M12-16: overlay cleared, Clear, Road or Ash land turned Rough) repaints only its own cells instead of rebaking the map.
/// </summary>
/// <remarks>
/// The snapshot's Land and the <see cref="MapData"/> are the starting map and never change during play, so they cannot be compared
/// against; the live values are on <see cref="MapState"/>. Comparing per cell also needs no event, so a change is never lost when a frame
/// runs several sim ticks and only the last snapshot is kept.
/// </remarks>
public sealed class CellGrid
{
    /// <summary>Wraps arrays (not copied). Each array has Width * Height entries, row-major.</summary>
    public CellGrid(int width, int height, LandType[] land, OverlayKind[] overlay, byte[] density)
    {
        ArgumentNullException.ThrowIfNull(land);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(density);
        Width = width;
        Height = height;
        Land = land;
        Overlay = overlay;
        Density = density;
    }

    /// <summary>Cells across.</summary>
    public int Width { get; }

    /// <summary>Cells down.</summary>
    public int Height { get; }

    /// <summary>Land type per cell.</summary>
    public LandType[] Land { get; }

    /// <summary>Overlay per cell.</summary>
    public OverlayKind[] Overlay { get; }

    /// <summary>Density per cell.</summary>
    public byte[] Density { get; }

    /// <summary>A copy of a map's starting cell inputs.</summary>
    public static CellGrid FromMap(MapData map)
    {
        ArgumentNullException.ThrowIfNull(map);
        int n = map.Width * map.Height;
        var land = new LandType[n];
        var overlay = new OverlayKind[n];
        var density = new byte[n];
        for (int i = 0; i < n; i++)
        {
            land[i] = map.Land[i];
            overlay[i] = map.Overlay[i];
            density[i] = map.Density[i];
        }

        return new CellGrid(map.Width, map.Height, land, overlay, density);
    }

    /// <summary>
    /// Compares this copy with the live state. Every cell whose land, overlay or density differs gets its live values copied in and its index
    /// appended to <paramref name="changed"/> (ascending). Returns how many cells changed. Pure and deterministic; no GPU types.
    /// </summary>
    public int Collect(MapState live, List<int> changed)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(changed);
        if (live.Width != Width || live.Height != Height) throw new ArgumentException("The live map has another size.", nameof(live));
        int count = 0;
        for (int i = 0; i < Land.Length; i++)
        {
            var c = new CellCoord(i % Width, i / Width);
            LandType land = live.LandAt(c);
            OverlayKind overlay = live.OverlayAt(c);
            byte density = live.DensityAt(c);
            if (land == Land[i] && overlay == Overlay[i] && density == Density[i]) continue;
            Land[i] = land;
            Overlay[i] = overlay;
            Density[i] = density;
            changed.Add(i);
            count++;
        }

        return count;
    }

    /// <summary>
    /// The cells whose texels can depend on the changed cells: each changed cell and its eight neighbours inside the map (the bake reads
    /// four neighbours; the diagonals are repainted too, which is harmless). Ascending, no duplicates.
    /// </summary>
    public static List<int> RepaintSet(int width, int height, IReadOnlyList<int> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var set = new SortedSet<int>();
        foreach (int i in changed)
        {
            int x = i % width, y = i / width;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < width && ny < height) set.Add(ny * width + nx);
                }
        }

        return set.ToList();
    }
}
