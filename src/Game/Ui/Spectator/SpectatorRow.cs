// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Spectator;

/// <summary>One player's line of the spectator stats table (design 08 §2), read from the sim between steps.</summary>
/// <param name="Player">Player slot.</param>
/// <param name="Team">Team id.</param>
/// <param name="Faction">The player's faction.</param>
/// <param name="Tier">Tech tier 1..3 (<c>Simulation.TierOf</c>).</param>
/// <param name="Cash">Spendable credits.</param>
/// <param name="Stored">Credits in silos.</param>
/// <param name="IncomePerMinute">Credits harvested per minute over the last 60 s of snapshots (front end only).</param>
/// <param name="ArmyValue">Sum of the cost of alive vehicles and infantry.</param>
/// <param name="Structures">Alive structures.</param>
/// <param name="PowerOut">Power produced.</param>
/// <param name="PowerUse">Power used.</param>
/// <param name="Stats">The counters of the "stats" part (<c>Simulation.StatsOf</c>), unchanged.</param>
/// <param name="Upgrades">Upgrades researched.</param>
/// <param name="Defeated">True once the player is defeated (drawn greyed out).</param>
public sealed record SpectatorRow(int Player, int Team, Faction Faction, int Tier, int Cash, int Stored, int IncomePerMinute,
    int ArmyValue, int Structures, int PowerOut, int PowerUse, PlayerStats Stats, int Upgrades, bool Defeated);
