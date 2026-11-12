// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Stats;

/// <summary>M10-17: what one end-of-match line chart plots.</summary>
public enum GraphMetric
{
    /// <summary>Total cost of the player's living vehicles and infantry.</summary>
    ArmyValue,
    /// <summary>Credits earned so far (harvested plus home shipments).</summary>
    Income,
    /// <summary>Enemy actors killed so far.</summary>
    Kills,
    /// <summary>Structures standing.</summary>
    Buildings,
    /// <summary>Upgrades researched.</summary>
    Upgrades,
}
