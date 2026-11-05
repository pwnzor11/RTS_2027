// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Render;

/// <summary>
/// How M3 unit states look (pure, so it is unit-tested): an underground unit is a faint ghost with a tunnel marker, a stealthed
/// unit is semi-transparent. The snapshot only contains underground or stealthed units the viewer may know about (own units and
/// enemies a Seismograph or detector reveals), so the renderer draws whatever it is given and hides nothing itself.
/// </summary>
public static class UnitVisuals
{
    /// <summary>Opacity of an underground unit (a faint ghost).</summary>
    public const float UndergroundAlpha = 0.30f;

    /// <summary>Opacity of a stealthed unit.</summary>
    public const float StealthAlpha = 0.50f;

    /// <summary>Opacity 0..1 to draw a unit's sprite with. Underground wins over stealth.</summary>
    public static float Alpha(UnitView view) => view.Underground ? UndergroundAlpha : view.Stealthed ? StealthAlpha : 1f;

    /// <summary>M7-14: opacity of a dived ship the viewer may see (own, allied, or an enemy found by sonar).</summary>
    public const float SubmergedAlpha = 0.45f;

    /// <summary>Opacity 0..1 as <see cref="Alpha(UnitView)"/>, with a dived ship (<paramref name="submerged"/>) semi-transparent. The faintest state wins.</summary>
    public static float Alpha(UnitView view, bool submerged) => submerged ? Math.Min(Alpha(view), SubmergedAlpha) : Alpha(view);

    /// <summary>Whether to draw the small dashed tunnel marker under the unit.</summary>
    public static bool ShowTunnelMarker(UnitView view) => view.Underground;

    /// <summary>M3-21: whether to draw the dive marker (a dashed ring that closes in) under a burrower in its dive animation. Drawn in
    /// addition to the <c>dive</c> sprite layer when the manifest has one, so a dive reads even on placeholder art.</summary>
    public static bool ShowDiveMarker(UnitView view) => view.Diving && !view.Underground;

    /// <summary>M3-21: turret facing to draw: the snapshot's, else the body facing (<see cref="UnitLayerPlan"/> then uses the body).</summary>
    public static byte? TurretFacing(UnitView view) => view.Kind == UnitKind.Vehicle ? view.TurretFacing : null;
}
