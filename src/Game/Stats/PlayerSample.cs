// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Stats;

/// <summary>M10-17: one player's numbers at one sample time.</summary>
/// <param name="ArmyValue">Cost of living vehicles and infantry.</param>
/// <param name="Earned">Credits harvested plus shipments, since the start.</param>
/// <param name="Kills">Enemy actors killed, since the start.</param>
/// <param name="Buildings">Structures standing.</param>
/// <param name="Upgrades">Upgrades researched.</param>
public readonly record struct PlayerSample(int ArmyValue, int Earned, int Kills, int Buildings, int Upgrades)
{
    /// <summary>The value of <paramref name="metric"/>.</summary>
    public int Get(GraphMetric metric) => metric switch
    {
        GraphMetric.ArmyValue => ArmyValue,
        GraphMetric.Income => Earned,
        GraphMetric.Kills => Kills,
        GraphMetric.Buildings => Buildings,
        GraphMetric.Upgrades => Upgrades,
        _ => 0,
    };
}
