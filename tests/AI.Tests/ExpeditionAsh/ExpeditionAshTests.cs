// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests.ExpeditionAsh;

/// <summary>S17: an Expedition player on Ash Plains layout 2 (sea behind the mains, about 9 cells from the start) gets its start kit and builds.</summary>
public class ExpeditionAshTests(ITestOutputHelper output)
{
    private static Simulation NewSim(ulong seed, out MapMeta meta)
    {
        var gen = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.AshPlains, MapSize.Medium, 2, seed, null, OceanEdges.North | OceanEdges.South, 2));
        meta = gen.Meta;
        var sim = new Simulation(new SimSetup(seed, gen.Map, 2, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash) { Factions = [Faction.Expedition, Faction.Expedition], LandingMarkers = gen.Meta.Landing });
        SkirmishStart.Apply(sim);
        return sim;
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Start_kit_spawns_on_ash_plains_layout_2(ulong seed)
    {
        Simulation sim = NewSim(seed, out _);
        for (byte p = 0; p < 2; p++)
        {
            int vehicles = 0, ships = 0, infantry = 0;
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == p) { vehicles++; if (TestWorld.Rules.Sim.Vehicles[sim.Units.TypeIndex[i]].Locomotion == Locomotion.Naval) ships++; }
            for (int i = 1; i < sim.Infantry.Capacity; i++)
                if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == p) infantry++;
            output.WriteLine($"seed {seed} p{p}: vehicles {vehicles} ships {ships} infantry {infantry}");
            Assert.True(ships >= 1, $"seed {seed} player {p}: no Command Ship spawned");
            Assert.Equal(3, vehicles);
            Assert.Equal(3, infantry);
        }
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void Expedition_ai_places_structures_on_ash_plains_layout_2(ulong seed)
    {
        Simulation sim = NewSim(seed, out _);
        DifficultyProfile profile = TestWorld.Tiers[1];
        var driver = new AiDriver(seed);
        driver.Add(0, new RecruitAi(profile), profile);
        driver.Add(1, new RecruitAi(profile), profile);
        var pending = new List<Command>();
        for (int t = 0; t < 3000; t++)
        {
            driver.Collect(sim, pending);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
        }
        for (byte p = 0; p < 2; p++)
        {
            int buildings = 0;
            for (int i = 1; i < sim.Buildings.Capacity; i++)
                if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == p) buildings++;
            output.WriteLine($"seed {seed} p{p}: buildings {buildings}");
            Assert.True(buildings >= 3, $"seed {seed} player {p}: only {buildings} structures by tick 3000");
        }
    }
}
