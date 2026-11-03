// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// Money and power API shared by economy (M2-07, owner of this file) and production (M2-08, caller).
public sealed partial class Simulation
{
    /// <summary>Total spendable money of a player (Cash + Stored).</summary>
    public int Funds(byte player) => _players.Cash[player] + _players.Stored[player];

    /// <summary>
    /// Spend up to <paramref name="amount"/> credits: draws from Cash first, then Stored. Returns the amount actually
    /// spent (less than asked when broke — production drains cost per tick and stalls on a partial spend).
    /// CONTRACT STUB: M2-07 may refine (e.g. order of draw) but keeps the signature and the "returns spent" meaning.
    /// </summary>
    internal int Spend(byte player, int amount)
    {
        if (amount <= 0) return 0;
        int fromCash = Math.Min(amount, _players.Cash[player]);
        _players.Cash[player] -= fromCash;
        int fromStored = Math.Min(amount - fromCash, _players.Stored[player]);
        _players.Stored[player] -= fromStored;
        return fromCash + fromStored;
    }

    /// <summary>Give credits back (cancel refunds, sell). Goes to Cash, which is uncapped.</summary>
    internal void Refund(byte player, int amount)
    {
        if (amount > 0) _players.Cash[player] += amount;
    }

    /// <summary>
    /// Power produced / used ratio band for production slowdown: 0 = full power, 1 = &lt;100 %, 2 = &lt;75 %, 3 = &lt;50 %.
    /// Reads Players.PowerOut/PowerUse, which M2-07 recomputes whenever buildings change.
    /// </summary>
    public int PowerBand(byte player)
    {
        int use = _players.PowerUse[player], output = _players.PowerOut[player];
        if (use <= output) return 0;
        long pct = output <= 0 ? 0 : (long)output * 100 / use;
        return pct >= 75 ? 1 : pct >= 50 ? 2 : 3;
    }
}
