// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.AI;

/// <summary>
/// s43 (D-311): the three owner-approved AI behaviour fixes, each behind one switch so balance can be measured with and without it.
/// Set a field to <c>false</c> and rebuild to get the pre-fix behaviour of that item alone.
/// </summary>
internal static class AiFixes
{
    /// <summary>A43-AI-01: a side whose income has stalled (<c>EconomyRescue.Broke</c>) may still rebuild a lost refinery or war factory.</summary>
    public static readonly bool BrokeRebuildsEconomy = true;

    /// <summary>A43-AI-02: the mop-up rides on the profile's <c>Mopup</c> option for tier 2+ even when respond_to_attacks is off (tier 1 stays off).</summary>
    public static readonly bool MopupWithoutRespond = true;

    /// <summary>A43-ROSTER-AD-06: the Directorate AI builds its Rocket Nest when it sees enemy air.</summary>
    public static readonly bool DirectorateAntiAir = true;
}
