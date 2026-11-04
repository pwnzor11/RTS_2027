// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.AI;

/// <summary>M4-12: outcome of one 2-4 player free-for-all AI game (<see cref="AiMatchRunner.PlayN"/>). Every array is indexed by SIDE
/// (the order the sides were passed in), not by slot. <see cref="Winner"/> = the last side standing, or -1 for a draw at the tick
/// limit (or a mutual wipe). <see cref="DefeatTick"/> = the tick a side was defeated, or -1 if it was still alive at the end;
/// <see cref="Value"/> = remaining health of everything the side owns (the tiebreak); <see cref="Start"/> = the map start index
/// the side played from.</summary>
public sealed record AiMultiResult(int Winner, int EndTick, int[] DefeatTick, int[] Value, int[] Start, AiSideStats[] Stats,
    ulong FinalHash)
{
    /// <summary>True if side <paramref name="a"/> outlasted side <paramref name="b"/>: defeated later (alive counts as never), or both
    /// alive at the end and more remaining health. Equal = neither.</summary>
    public bool Outlasted(int a, int b)
    {
        long ta = DefeatTick[a] < 0 ? long.MaxValue : DefeatTick[a], tb = DefeatTick[b] < 0 ? long.MaxValue : DefeatTick[b];
        if (ta != tb) return ta > tb;
        return DefeatTick[a] < 0 && Value[a] > Value[b];
    }
}
