// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Sprites;

namespace Rts.Game.Render;

/// <summary>
/// V-05: turns the <c>[anchors]</c> of a vehicle's manifests into offsets from the unit's ground point, in zoom-1 world pixels, for the muzzle
/// flash and the movement dust. Pure view maths (float is fine here). A null result means "no anchor: use the old generic offset".
/// </summary>
public static class AnchorOffsets
{
    /// <summary>
    /// Where a shot leaves the unit, relative to its origin. With a companion turret sheet that has a muzzle table the point is the turret
    /// sheet's anchor for the turret facing, placed at the turret pivot of the hull facing (as the turret itself is drawn); otherwise the body
    /// sheet's muzzle table for the hull facing (a fixed gun). Null when neither exists.
    /// </summary>
    /// <param name="body">The unit's body manifest.</param>
    /// <param name="turretSheet">The companion turret manifest (<c>&lt;id&gt;_turret</c>), or null.</param>
    /// <param name="hullFacing">Hull facing byte.</param>
    /// <param name="turretFacing">Turret facing byte, or null to use the hull facing.</param>
    /// <param name="unitScale">Total unit sprite scale before the zoom (<see cref="UnitRenderer.DrawScale"/> times <see cref="SpriteLayout.UnitDrawScale"/>).</param>
    public static Vector2? Muzzle(SpriteManifest body, SpriteManifest? turretSheet, byte hullFacing, byte? turretFacing, float unitScale)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (turretSheet?.Anchors is { HasMuzzle: true } ta && turretSheet.Pivot is { } pivot && ta.Muzzle(turretFacing ?? hullFacing) is { } t)
        {
            int hullFacings = body.States[0].Facings;
            double hullAngle = hullFacings == 1 ? 0 : 360.0 * Rts.Sim.Numerics.Facing.ToFacings(hullFacing, hullFacings) / hullFacings;
            (int px, int py) = UnitLayerPlan.PivotOffset(pivot, hullAngle, turretSheet.PxPerCell);
            return new Vector2(px + t.Dx, py + t.Dy) * (turretSheet.ArtScale * unitScale);
        }
        if (body.Anchors is { HasMuzzle: true } ba && ba.Muzzle(hullFacing) is { } m)
        {
            return new Vector2(m.Dx, m.Dy) * (body.ArtScale * unitScale);
        }
        return null;
    }

    /// <summary>Where the dust comes up behind a moving vehicle (the rear ground contact centre), relative to its origin; null without a dust table.</summary>
    /// <param name="body">The unit's body manifest.</param>
    /// <param name="hullFacing">Hull facing byte.</param>
    /// <param name="unitScale">Total unit sprite scale before the zoom.</param>
    public static Vector2? Dust(SpriteManifest body, byte hullFacing, float unitScale)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.Anchors is { HasDust: true } a && a.Dust(hullFacing) is { } d)
        {
            return new Vector2(d.Dx, d.Dy) * (body.ArtScale * unitScale);
        }
        return null;
    }
}
