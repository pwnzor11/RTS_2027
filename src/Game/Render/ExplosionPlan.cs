// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>One explosion to spawn for an <see cref="FxEvent"/>: its sprite, its offset from the event position in cells
/// and its scale on top of the camera zoom.</summary>
/// <param name="Kind">Effect sprite id.</param>
/// <param name="DxCells">Horizontal offset from the event position, in cells.</param>
/// <param name="DyCells">Vertical offset from the event position, in cells.</param>
/// <param name="Scale">Size multiplier (1 = the sprite as drawn).</param>
public readonly record struct ExplosionSpot(string Kind, float DxCells, float DyCells, float Scale);

/// <summary>
/// Turns one sim effects event into the explosions the renderer spawns (view only, never sim state). A building death covers its
/// footprint: the sim sends the larger footprint side as <see cref="FxEvent.Size"/>, and the death gets one large explosion per
/// side up to <see cref="MaxBuildingBlasts"/>, spread around the centre and scaled up by <see cref="ScalePerExtraCell"/> per cell
/// beyond the first. Units and detonations keep a single small explosion.
/// </summary>
public static class ExplosionPlan
{
    /// <summary>Most explosions one building death spawns.</summary>
    // ASSUMED (M3-12): a building death shows at most 4 explosions, however large the footprint.
    public const int MaxBuildingBlasts = 4;

    /// <summary>Extra size per footprint cell beyond the first (0.25 = a 3x3 building blows up at 1.5x).</summary>
    // ASSUMED (M3-12): each extra footprint cell adds a quarter to the explosion size.
    public const float ScalePerExtraCell = 0.25f;

    /// <summary>Largest explosion scale, so a huge building does not fill the screen.</summary>
    // ASSUMED (M3-12): explosions never grow beyond twice their sprite size.
    public const float MaxScale = 2f;

    /// <summary>Distance of the outer blasts from the centre, in cells, per extra footprint cell.</summary>
    // ASSUMED (M3-12): outer blasts sit a quarter cell out for each extra footprint cell.
    private const float SpreadPerExtraCell = 0.25f;

    /// <summary>The explosions for one effects event, in a fixed order.</summary>
    public static IReadOnlyList<ExplosionSpot> For(FxEvent e)
    {
        if (e.Kind != FxKind.BuildingDestroyed)
            return new[] { new ExplosionSpot("explosion_small", 0f, 0f, 1f) };

        int side = Math.Max(1, e.Size);
        int count = Math.Clamp(side, 1, MaxBuildingBlasts);
        float scale = Math.Min(MaxScale, 1f + ScalePerExtraCell * (side - 1));
        var spots = new ExplosionSpot[count];
        if (count == 1)
        {
            spots[0] = new ExplosionSpot("explosion_large", 0f, 0f, scale);
            return spots;
        }

        float radius = SpreadPerExtraCell * (side - 1);
        for (int k = 0; k < count; k++)
        {
            double angle = 2 * Math.PI * k / count;
            spots[k] = new ExplosionSpot("explosion_large", (float)(radius * Math.Cos(angle)), (float)(radius * Math.Sin(angle)), scale);
        }
        return spots;
    }
}
