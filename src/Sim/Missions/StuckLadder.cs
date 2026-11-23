// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Missions;

/// <summary>What the stuck ladder asks a wedged unit to do (S41 M13-44).</summary>
internal enum StuckAction : byte
{
    /// <summary>Nothing new.</summary>
    None = 0,
    /// <summary>Throw the route away and ask for a fresh one.</summary>
    Replan = 1,
    /// <summary>Step to a free neighbour cell away from the blocker.</summary>
    Sidestep = 2,
    /// <summary>Accept a nearby cell as arrival and ask idle friends in the way to scatter.</summary>
    Relax = 3,
    /// <summary>Give up the move order.</summary>
    Abandon = 4,
}

/// <summary>
/// Pure decision of the recovery ladder (S41 M13-44; the idea of a displacement-based stuck clock is from the MIT-licensed Standard of
/// Iron, no code taken). The caller measures ticks since the unit's anchor (the last cell at Chebyshev distance 2 or more from where it
/// is now) and remembers the rung already used; each rung fires once per anchor.
/// </summary>
internal static class StuckLadder
{
    /// <summary>Ticks without net displacement before a fresh route is requested.</summary>
    public const int ReplanTicks = 60;     // (tune) s41
    /// <summary>Ticks before a sidestep.</summary>
    public const int SidestepTicks = 120;  // (tune) s41
    /// <summary>Ticks before the goal is relaxed.</summary>
    public const int RelaxTicks = 200;     // (tune) s41
    /// <summary>Ticks before the order is abandoned.</summary>
    public const int AbandonTicks = 320;   // (tune) s41
    /// <summary>Chebyshev distance from the anchor that counts as progress and resets the clock.</summary>
    public const int ProgressDistance = 2; // (tune) s41
    /// <summary>Relax accepts a stop within this many cells of the goal as arrival.</summary>
    public const int RelaxRadius = 3;      // (tune) s41

    /// <summary>The next action for <paramref name="elapsed"/> ticks since the anchor; <paramref name="rung"/> is the highest rung used so far (0 to 4) and is raised to the action taken.</summary>
    public static StuckAction Decide(int elapsed, ref int rung)
    {
        StuckAction a = elapsed >= AbandonTicks ? StuckAction.Abandon
            : elapsed >= RelaxTicks ? StuckAction.Relax
            : elapsed >= SidestepTicks ? StuckAction.Sidestep
            : elapsed >= ReplanTicks ? StuckAction.Replan : StuckAction.None;
        if ((int)a <= rung) return StuckAction.None;
        rung = (int)a;
        return a;
    }
}
