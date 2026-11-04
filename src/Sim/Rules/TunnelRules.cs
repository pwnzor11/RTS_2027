// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>
/// Tunnel constants (game.toml <c>[tunnels]</c>, design 05 §1, D-074). All (tune). Ticks are sim ticks (20 Hz).
/// TransitTicks = flat time underground per network trip; NetworkCapPoints = most points in transit per player network;
/// PointsS / PointsM = points an S / M unit costs; DiveTicks = time to dive (free burrowing); TelegraphTicks = surfacing
/// telegraph of a unit; EntranceTelegraphTicks = break-through telegraph of a new Hatch; EmergingHealthPermille = HP a
/// breaking-through Hatch starts with; SurfacingRing = cells around any structure where nothing may surface.
/// </summary>
public sealed record TunnelRules(int TransitTicks, int NetworkCapPoints, int PointsS, int PointsM, int DiveTicks,
    int TelegraphTicks, int EntranceTelegraphTicks, int EmergingHealthPermille, int SurfacingRing)
{
    /// <summary>Design 05 starting values.</summary>
    public static TunnelRules Default { get; } = new(40, 10, 1, 3, 20, 60, 100, 250, 1);

    /// <summary>Points a unit of <paramref name="size"/> costs in transit: S and M per the table; None and L cannot use the network (0).</summary>
    public int PointsOf(TunnelSize size) => size switch { TunnelSize.S => PointsS, TunnelSize.M => PointsM, _ => 0 };
}
