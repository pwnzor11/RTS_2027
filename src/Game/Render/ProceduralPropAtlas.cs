// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>
/// The placeholder prop atlas: every <see cref="PropKind"/> drawn by <see cref="PropArt"/> in the colours of one theater palette, packed
/// in a grid (one row per kind, one column per variant). Built without a GPU.
/// </summary>
public sealed class ProceduralPropAtlas : IPropAtlas
{
    private static readonly PropKind[] Kinds = Enum.GetValues<PropKind>();

    /// <summary>Draws the atlas for a palette.</summary>
    public ProceduralPropAtlas(TerrainPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        Width = PropArt.Variants * PropArt.FramePx;
        Height = Kinds.Length * PropArt.FramePx;
        Rgba = new byte[Width * Height * 4];
        foreach (PropKind kind in Kinds)
        {
            for (int v = 0; v < PropArt.Variants; v++)
            {
                byte[] frame = PropArt.Render(kind, v, palette);
                PropRect r = Frame(kind, v);
                for (int y = 0; y < PropArt.FramePx; y++)
                    Buffer.BlockCopy(frame, y * PropArt.FramePx * 4, Rgba, ((r.Y + y) * Width + r.X) * 4, PropArt.FramePx * 4);
            }
        }
    }

    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height { get; }

    /// <inheritdoc />
    public byte[] Rgba { get; }

    /// <inheritdoc />
    public int VariantCount(PropKind kind) => PropArt.Variants;

    /// <inheritdoc />
    public PropRect Frame(PropKind kind, int variant)
    {
        int v = (variant % PropArt.Variants + PropArt.Variants) % PropArt.Variants;
        return new PropRect(v * PropArt.FramePx, (int)kind * PropArt.FramePx, PropArt.FramePx, PropArt.FramePx, PropArt.FramePx / 2, PropArt.FramePx / 2);
    }
}
