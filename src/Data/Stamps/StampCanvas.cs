// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Data.Stamps;

/// <summary>The writable map layers a stamp is applied to (generator working copy or editor document). Arrays are row-major,
/// <c>y * Width + x</c>, and are written in place. <see cref="Decor"/> may be null when the map has no decal layer (stamp decor is then skipped).
/// <see cref="Starts"/> are cells no stamp may touch within <see cref="StartKeepRadius"/> (Chebyshev); resource cells (Ashfall, Pitchblende) are always kept.</summary>
public sealed class StampCanvas
{
    /// <summary>Default start keep-out radius: the generator's clear start square.</summary>
    public const int DefaultStartKeepRadius = 3;

    /// <summary>Creates a canvas over the given arrays (not copied). Every array needs <c>width * height</c> entries.</summary>
    public StampCanvas(int width, int height, LandType[] land, byte[] heightLevel, OverlayKind[] overlay, byte[] density, byte[]? decor = null,
        IReadOnlyList<CellCoord>? starts = null)
    {
        int n = width * height;
        if (width < 1 || height < 1 || land.Length != n || heightLevel.Length != n || overlay.Length != n || density.Length != n || (decor is not null && decor.Length != n))
            throw new ArgumentException("Every layer must have Width x Height entries.");
        Width = width; Height = height; Land = land; HeightLevel = heightLevel; Overlay = overlay; Density = density; Decor = decor;
        Starts = starts ?? Array.Empty<CellCoord>();
    }

    /// <summary>Width in cells.</summary>
    public int Width { get; }
    /// <summary>Height in cells.</summary>
    public int Height { get; }
    /// <summary>Land layer.</summary>
    public LandType[] Land { get; }
    /// <summary>Height-level layer.</summary>
    public byte[] HeightLevel { get; }
    /// <summary>Overlay layer.</summary>
    public OverlayKind[] Overlay { get; }
    /// <summary>Density layer.</summary>
    public byte[] Density { get; }
    /// <summary>Decal layer, or null when the map has none.</summary>
    public byte[]? Decor { get; }
    /// <summary>Start positions that stamps keep clear.</summary>
    public IReadOnlyList<CellCoord> Starts { get; }
    /// <summary>Chebyshev radius around each start that a stamp may not write.</summary>
    public int StartKeepRadius { get; init; } = DefaultStartKeepRadius;

    /// <summary>A canvas over copies of a map's layers.</summary>
    public static StampCanvas CopyOf(Rts.Sim.Map.MapData map) => new(map.Width, map.Height, map.Land.ToArray(), map.HeightLevel.ToArray(),
        map.Overlay.ToArray(), map.Density.ToArray(), map.Decor?.ToArray(), map.StartPositions);

    /// <summary>True when cell (<paramref name="x"/>, <paramref name="y"/>) is within <see cref="StartKeepRadius"/> of a start.</summary>
    public bool NearStart(int x, int y)
    {
        foreach (CellCoord s in Starts)
            if (Math.Max(Math.Abs(s.X - x), Math.Abs(s.Y - y)) <= StartKeepRadius) return true;
        return false;
    }
}
