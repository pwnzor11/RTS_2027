// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Sim.Map;

namespace Rts.Game.Ui;

/// <summary>
/// The minimap's terrain pixels, one per cell, kept in step with the live sim (M12-16: a destroyed prop turns its cell to Rough and loses its
/// dot). CPU only: <see cref="Sync"/> repaints the changed cells with <see cref="Minimap.TerrainColour"/>, and the <see cref="Minimap"/> uploads
/// just those pixels to its texture.
/// </summary>
public sealed class MinimapTerrain
{
    private readonly CellGrid _shown;
    private readonly TerrainPalette? _palette;
    private readonly List<int> _changed = new();

    /// <summary>Bakes every cell of <paramref name="map"/> with <paramref name="palette"/> (null = the built-in colours).</summary>
    public MinimapTerrain(MapData map, TerrainPalette? palette)
    {
        ArgumentNullException.ThrowIfNull(map);
        _palette = palette;
        _shown = CellGrid.FromMap(map);
        Pixels = new Color[_shown.Land.Length];
        for (int i = 0; i < Pixels.Length; i++) Pixels[i] = Minimap.TerrainColour(palette, _shown.Land[i], _shown.Overlay[i], _shown.Density[i]);
    }

    /// <summary>Cells across.</summary>
    public int Width => _shown.Width;

    /// <summary>The pixel of every cell, row-major.</summary>
    public Color[] Pixels { get; }

    /// <summary>
    /// Brings the pixels up to the live sim. Returns the indices of the cells that changed (ascending), each already repainted in
    /// <see cref="Pixels"/>. The list is reused by the next call.
    /// </summary>
    public IReadOnlyList<int> Sync(MapState live)
    {
        _changed.Clear();
        _shown.Collect(live, _changed);
        foreach (int i in _changed) Pixels[i] = Minimap.TerrainColour(_palette, _shown.Land[i], _shown.Overlay[i], _shown.Density[i]);
        return _changed;
    }
}
