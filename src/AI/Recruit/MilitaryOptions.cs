// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;

namespace Rts.AI.Recruit;

/// <summary>M3-11: the profile flags the <see cref="MilitaryManager"/> reads. <see cref="Recruit"/> is the original tier 1 behaviour.</summary>
internal readonly record struct MilitaryOptions(bool Respond, bool TwoPronged, bool MultiFront, int RetreatPct)
{
    /// <summary>Defends, one prong, never retreats (the M3-10 behaviour).</summary>
    public static MilitaryOptions Recruit => new(true, false, false, 0);

    /// <summary>The options a profile asks for.</summary>
    public static MilitaryOptions From(DifficultyProfile p) =>
        new(p.RespondToAttacks, p.TwoPronged, p.MultiFront && p.TwoPronged, p.RetreatHealthPct);
}
