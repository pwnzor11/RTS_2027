// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>M4: one player's counters from the "stats" state part (<see cref="Simulation.StatsOf"/>). Units = vehicles and
/// infantry; Kills = enemy actors whose lethal hit this player owned; credits are totals since tick 0.</summary>
public readonly record struct PlayerStats(int UnitsBuilt, int UnitsLost, int StructuresBuilt, int StructuresLost, int Kills,
    int CreditsHarvested, int CreditsSpent);
