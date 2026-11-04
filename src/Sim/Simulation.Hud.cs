// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

public sealed partial class Simulation
{
    /// <summary>
    /// HUD snapshot for a player: cash, stored credits, power and defeat from the players part; one queue view per
    /// category (all five, empty when idle) with the front item's progress and stalled flag and the ready-to-place type;
    /// every (category, type) the player could Build right now (<see cref="PlayerHud.Buildable"/>, see <see cref="CanBuild"/>), and
    /// every unlocked type with its block reason (<see cref="PlayerHud.Options"/>, D-066).
    /// </summary>
    public PlayerHud HudFor(byte player)
    {
        if (player >= _players.Count) throw new ArgumentOutOfRangeException(nameof(player));
        return new PlayerHud(_players.Cash[player], _players.Stored[player], StorageCap(player), _players.PowerOut[player],
            _players.PowerUse[player], BuildQueueViews(player), BuildBuildableList(player), _players.Defeated[player],
            BuildOptionList(player))
        {
            Faction = _players.Faction[player],
            Upgrades = _players.Upgrades[player],
            BuildableUpgrades = BuildableUpgradeList(player),
        };
    }

    /// <summary>Upgrade indices <see cref="CanResearch"/> accepts now (M3).</summary>
    private IReadOnlyList<int> BuildableUpgradeList(byte player)
    {
        IReadOnlyList<UpgradeType> upgrades = Setup.Rules.Upgrades;
        List<int>? list = null;
        for (int u = 0; u < upgrades.Count; u++)
            if (CanResearch(player, u)) (list ??= new List<int>()).Add(u);
        return list ?? (IReadOnlyList<int>)Array.Empty<int>();
    }

    /// <summary>
    /// True once the match is decided (D-062): at least one player has been defeated and at most one team still has an
    /// undefeated non-neutral player.
    /// </summary>
    public bool GameOver => ComputeGameOver(out _);

    /// <summary>
    /// True if a finished <paramref name="buildingType"/> could be placed with its footprint's top-left at <paramref name="topLeft"/>
    /// by <paramref name="player"/> right now: <see cref="CanPlace"/> says Ok and the footprint is within the build radius (D-062) of an
    /// own building. The ghost cursor and the Place command use the same checks. False for a bad type or player.
    /// </summary>
    public bool CanPlaceReady(byte player, int buildingType, CellCoord topLeft)
    {
        if (player >= _players.Count || _players.IsNeutral(player)) return false;
        if (buildingType < 0 || buildingType >= BuildingTypeCount) return false;
        return CanPlace(player, buildingType, topLeft) == PlacementResult.Ok
            && WithinBuildRadius(player, Setup.Rules.Buildings[buildingType], topLeft);
    }

    /// <summary>The winning team once <see cref="GameOver"/>, else -1 (also -1 if every player was defeated).</summary>
    public int WinningTeam => ComputeGameOver(out int team) ? team : -1;
}
