// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// VET-6 (D-127, design 14 section 6a): the credit value of an army unit once its veterancy rank counts. Rank comes from the
/// <see cref="ViewActor.Rank"/> of the PlayerView only (D-051). Rank 0 is exactly the plain cost, so with veterancy off nothing changes.
/// </summary>
internal static class VeterancyValue
{
    /// <summary>Value added per rank, in permille of the cost (rank 3 is worth about 1.75 times a recruit).</summary>
    public const int PermillePerRank = 250;   // (tune) design 14 section 6

    /// <summary>The highest rank.</summary>
    public const int MaxRank = 3;

    /// <summary><c>cost x (1000 + 250 x rank) / 1000</c> in integer math; rank is clamped to 0..3.</summary>
    public static int Of(int cost, int rank)
    {
        rank = Math.Clamp(rank, 0, MaxRank);
        return (int)((long)cost * (1000 + PermillePerRank * rank) / 1000);
    }

    /// <summary>Value of a mobile unit by its rules cost and view rank; 0 for anything that is not a vehicle or infantry.</summary>
    public static int Of(SimRules rules, ViewActor a)
    {
        int cost = a.Kind == ActorKind.Vehicle ? rules.Vehicles[a.TypeIndex].Cost : a.Kind == ActorKind.Infantry ? rules.Infantry[a.TypeIndex].Cost : 0;
        return Of(cost, a.Rank);
    }
}
