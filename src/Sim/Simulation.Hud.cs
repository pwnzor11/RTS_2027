// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim;

public sealed partial class Simulation
{
    /// <summary>
    /// HUD snapshot for a player: cash, stored credits, power and defeat from the players part; one queue view per
    /// category (all five, empty when idle) with the front item's progress and stalled flag and the ready-to-place type;
    /// and every (category, type) the player could Build right now (see <see cref="CanBuild"/>).
    /// </summary>
    public PlayerHud HudFor(byte player)
    {
        if (player >= _players.Count) throw new ArgumentOutOfRangeException(nameof(player));
        return new PlayerHud(_players.Cash[player], _players.Stored[player], StorageCap(player), _players.PowerOut[player],
            _players.PowerUse[player], BuildQueueViews(player), BuildBuildableList(player), _players.Defeated[player]);
    }

    /// <summary>
    /// True once the match is decided (D-062): at least one player has been defeated and at most one team still has an
    /// undefeated non-neutral player.
    /// </summary>
    public bool GameOver => ComputeGameOver(out _);

    /// <summary>The winning team once <see cref="GameOver"/>, else -1 (also -1 if every player was defeated).</summary>
    public int WinningTeam => ComputeGameOver(out int team) ? team : -1;
}
