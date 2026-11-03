// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Tests.Boundary;

/// <summary>Test helper: spawn after tick 0 through the Debug command (setup calls are rejected once the sim has stepped).
/// Needs a sim built with AllowDebug. Costs one tick.</summary>
public static class LateSpawn
{
    private static ushort _seq = 60000;

    public static EntityId Vehicle(Simulation sim, byte player, int type, CellCoord cell)
    {
        int slot = FirstFree(sim.Units.Slots);
        sim.Step(new[] { new Command(sim.Tick, player, _seq++, CommandKind.Debug, default, default, cell, (int)DebugOp.SpawnVehicle, type) });
        return sim.Units.Slots.IdOf(slot);
    }

    public static EntityId Infantry(Simulation sim, byte player, int type, CellCoord cell)
    {
        int slot = FirstFree(sim.Infantry.Slots);
        sim.Step(new[] { new Command(sim.Tick, player, _seq++, CommandKind.Debug, default, default, cell, (int)DebugOp.SpawnInfantry, type) });
        return sim.Infantry.Slots.IdOf(slot);
    }

    /// <summary>Place a building through a Place command; returns its id (None if rejected). Costs one tick.</summary>
    public static EntityId Building(Simulation sim, byte player, int type, CellCoord topLeft)
    {
        sim.Step(new[] { new Command(sim.Tick, player, _seq++, CommandKind.Place, default, default, topLeft, type) });
        return sim.Map.BuildingAt(topLeft);
    }

    private static int FirstFree(Rts.Sim.World.SlotArray s)
    {
        for (int i = 1; i < s.Capacity; i++) if (!s.IsIndexAlive(i)) return i;
        throw new InvalidOperationException("full");
    }
}
