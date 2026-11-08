// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>Where one prop frame sits in an atlas, and the point of the frame that stands on the cell centre.</summary>
/// <param name="X">Left edge in atlas pixels.</param>
/// <param name="Y">Top edge in atlas pixels.</param>
/// <param name="Width">Frame width in pixels at zoom 1.</param>
/// <param name="Height">Frame height in pixels at zoom 1.</param>
/// <param name="OriginX">Pixel of the frame (from its left edge) that is placed on the cell centre.</param>
/// <param name="OriginY">Pixel of the frame (from its top edge) that is placed on the cell centre.</param>
public readonly record struct PropRect(int X, int Y, int Width, int Height, int OriginX, int OriginY);

/// <summary>
/// The prop atlas contract (M12-03): (kind, variant) to a rectangle of one shared texture. The renderer asks only this, so the procedural
/// placeholder art (<see cref="ProceduralPropAtlas"/>) can be replaced by real art (M10) without touching the planner or the renderer.
/// </summary>
public interface IPropAtlas
{
    /// <summary>Atlas width in pixels.</summary>
    int Width { get; }

    /// <summary>Atlas height in pixels.</summary>
    int Height { get; }

    /// <summary>Premultiplied RGBA bytes of the whole atlas, row-major.</summary>
    byte[] Rgba { get; }

    /// <summary>How many variants a kind has (at least 1). A variant number beyond it wraps.</summary>
    int VariantCount(PropKind kind);

    /// <summary>The frame of a kind and variant.</summary>
    PropRect Frame(PropKind kind, int variant);
}
