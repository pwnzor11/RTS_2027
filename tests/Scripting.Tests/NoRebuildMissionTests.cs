// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>M4-17 (D-173): the skirmish no-rebuild defeat rule is off in campaign missions.</summary>
public sealed class NoRebuildMissionTests
{
    [Theory]
    [InlineData("auth_01")]
    [InlineData("dir_01")]
    public void A_mission_player_with_no_production_founder_or_harvester_is_not_defeated(string id)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, id);
        Simulation sim = m.Sim;
        Assert.False(sim.Setup.NoRebuildDefeat);
        byte human = m.HumanSlot;
        SimRules rules = sim.Setup.Rules;
        // Keep exactly one building of the human player that is neither a producer nor a tech root; kill every unit and the rest.
        int keep = -1;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
        {
            if (!sim.Buildings.Slots.IsIndexAlive(i) || sim.Buildings.Player[i] != human) continue;
            BuildingType bt = rules.Buildings[sim.Buildings.TypeIndex[i]];
            if (keep < 0 && bt.Produces == null && bt.Prerequisites.Count > 0 && !bt.Wall && !bt.Gate) keep = i;
        }
        Assert.True(keep > 0, "the mission start has a plain building to keep");
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (i != keep && sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == human) sim.Buildings.Health[i] = 0;
        for (int v = 1; v < sim.Units.Capacity; v++)
            if (sim.Units.Slots.IsIndexAlive(v) && sim.Units.Player[v] == human) sim.Units.Health[v] = 0;
        for (int v = 1; v < sim.Infantry.Capacity; v++)
            if (sim.Infantry.Slots.IsIndexAlive(v) && sim.Infantry.Player[v] == human) sim.Infantry.Health[v] = 0;
        for (int t = 0; t < SimConstants.NoRebuildDefeatTicks + 300; t++) sim.Step(Array.Empty<Command>());
        Assert.False(sim.Players.Defeated[human]);
    }
}
