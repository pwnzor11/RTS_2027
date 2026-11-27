// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Scripting;

/// <summary>The medal of a drill run, worst to best (the numbers are stored in the profile).</summary>
public enum DrillMedal
{
    /// <summary>Won, but no threshold met.</summary>
    None = 0,
    /// <summary>Bronze.</summary>
    Bronze = 1,
    /// <summary>Silver.</summary>
    Silver = 2,
    /// <summary>Gold.</summary>
    Gold = 3,
}

/// <summary>What a finished drill run measured.</summary>
/// <param name="Seconds">Whole seconds of play.</param>
/// <param name="Lost">Own units plus own structures destroyed.</param>
/// <param name="Kept">Own units (vehicles and infantry) alive at the end.</param>
public readonly record struct DrillRun(int Seconds, int Lost, int Kept);

/// <summary>S47 U-12: measures a drill run from the sim and turns it into a medal. Pure; the thresholds are data (<see cref="DrillDef"/>).</summary>
public static class DrillScoring
{
    /// <summary>Reads the run from the sim for <paramref name="player"/>: time, losses from the stats, own mobile units still alive.</summary>
    public static DrillRun Measure(Simulation sim, byte player)
    {
        PlayerStats st = sim.StatsOf(player);
        int kept = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player && sim.Units.Health[i] > 0) kept++;
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player && sim.Infantry.Health[i] > 0) kept++;
        return new DrillRun(Math.Max(1, sim.Tick / SimConstants.TicksPerSecond), st.UnitsLost + st.StructuresLost, kept);
    }

    /// <summary>The medal a winning run earns under <paramref name="def"/>.</summary>
    public static DrillMedal Medal(DrillDef def, DrillRun run)
    {
        int value = def.IsTime ? run.Seconds : run.Lost;
        if (value <= def.Gold && run.Kept >= def.KeepGold) return DrillMedal.Gold;
        if (value <= def.Silver && run.Kept >= def.KeepSilver) return DrillMedal.Silver;
        if (value <= def.Bronze && run.Kept >= def.KeepBronze) return DrillMedal.Bronze;
        return DrillMedal.None;
    }
}
