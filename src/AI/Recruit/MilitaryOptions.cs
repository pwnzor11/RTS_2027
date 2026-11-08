// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;

namespace Rts.AI.Recruit;

/// <summary>M3-11: the profile flags (S15 siege: <c>Tier</c> = profile tier, <c>Artillery</c> = the profile's counter_defences flag: long-range units hold back and shoot from outside) the <see cref="MilitaryManager"/> reads. <see cref="Recruit"/> is the original tier 1 behaviour.</summary>
internal readonly record struct MilitaryOptions(bool Respond, bool TwoPronged, bool MultiFront, int RetreatPct, int AttackRatioPct = 0, bool PredictedRetreat = false, int RetreatRatioPct = 0, int Tier = 1, bool Artillery = false)
{
    /// <summary>Defends, one prong, never retreats (the M3-10 behaviour).</summary>
    public static MilitaryOptions Recruit => new(true, false, false, 0);

    /// <summary>The options a profile asks for.</summary>
    public static MilitaryOptions From(DifficultyProfile p) =>
        new(p.RespondToAttacks, p.TwoPronged, p.MultiFront && p.TwoPronged, p.RetreatHealthPct, p.AttackRatioPct, p.PredictedRetreat, p.RetreatRatioPct, p.Tier, p.CounterDefences);
}
