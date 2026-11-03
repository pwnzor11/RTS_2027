// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim;

public sealed partial class Simulation
{
    /// <summary>
    /// HUD snapshot for a player. Cash, stored credits, power and defeat come from the players part; storage cap,
    /// queues and the buildable list are stubs until the production package fills them.
    /// </summary>
    public PlayerHud HudFor(byte player)
    {
        if (player >= _players.Count) throw new ArgumentOutOfRangeException(nameof(player));
        return new PlayerHud(_players.Cash[player], _players.Stored[player], 0, _players.PowerOut[player],
            _players.PowerUse[player], Array.Empty<QueueView>(),
            Array.Empty<(ProductionCategory Category, int TypeIndex)>(), _players.Defeated[player]);
    }

    /// <summary>True once the match is decided. Stub (always false); the win-condition package fills it.</summary>
    public bool GameOver => false;

    /// <summary>The winning team once <see cref="GameOver"/>, else -1. Stub; the win-condition package fills it.</summary>
    public int WinningTeam => -1;
}
