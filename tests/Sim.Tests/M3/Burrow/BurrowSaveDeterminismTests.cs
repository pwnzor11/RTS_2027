// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Tunnels;
using static Rts.Sim.Tests.M3.Burrow.BurrowWorld;

namespace Rts.Sim.Tests.M3.Burrow;

/// <summary>M3-03: a save taken at any stage of burrowing / digging / morphing resumes identically, and equal inputs give equal
/// hashes tick for tick.</summary>
public sealed class BurrowSaveDeterminismTests
{
    private static Simulation RoundTrip(Simulation sim, SimSetup setup)
    {
        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        return Simulation.Load(ms, setup);
    }

    private static void AssertSameFuture(Simulation a, Simulation b, int ticks)
    {
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        for (int t = 0; t < ticks; t++)
        {
            a.Step(None); b.Step(None);
            Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        }
        Assert.Equal(a.ComputeSubHashes(), b.ComputeSubHashes());
    }

    [Theory]
    [InlineData(ActorLayer.Diving)]
    [InlineData(ActorLayer.Burrowed)]
    [InlineData(ActorLayer.Surfacing)]
    public void SaveLoad_AtEveryBurrowStage_ResumesIdentically(ActorLayer stage)
    {
        SimSetup setup = Setup();
        var sim = new Simulation(setup);
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        sim.SpawnVehicle(0, Burrower, new CellCoord(10, 14));
        Step(sim, MoveTo(sim, u, 40, 12));
        RunUntil(sim, () => Layer(sim, u) == stage);
        if (stage == ActorLayer.Burrowed) Run(sim, 25);          // well inside the route
        if (stage == ActorLayer.Surfacing) Run(sim, 15);         // part-way through the telegraph
        Simulation loaded = RoundTrip(sim, setup);
        Assert.Equal(stage, Layer(loaded, u));
        Assert.Equal(sim.Tunnels.Telegraphs, loaded.Tunnels.Telegraphs);
        AssertSameFuture(sim, loaded, 450);
        Assert.Equal(ActorLayer.Surface, Layer(loaded, u));
        Assert.Equal(CellOf(sim, u), CellOf(loaded, u));
    }

    [Fact]
    public void SaveLoad_DuringTheHatchTelegraph_AndMidMorph()
    {
        SimSetup setup = Setup();
        var sim = new Simulation(setup);
        EntityId bore = sim.SpawnVehicle(0, Bore, new CellCoord(10, 10));
        EntityId h = sim.PlaceBuilding(0, Hatch, new CellCoord(6, 25));
        Step(sim, DeployAt(sim, bore, 30, 10), Cmd(sim, CommandKind.Upgrade, h, default, BoreHead));
        RunUntil(sim, () => EntranceTelegraph(sim) != null);
        Run(sim, 40);
        Assert.NotEqual(0, sim.Tunnels.BuildingMorphType[h.Index] + sim.Tunnels.BuildingMorphTicks[h.Index] + (EntranceTelegraph(sim) is null ? 0 : 1));
        Simulation loaded = RoundTrip(sim, setup);
        Assert.Equal(sim.Tunnels.Telegraphs, loaded.Tunnels.Telegraphs);
        AssertSameFuture(sim, loaded, 150);
        Assert.Empty(loaded.Tunnels.Telegraphs);
        Assert.Equal(BoreHead, loaded.Buildings.TypeIndex[h.Index]);

        // and a save in the middle of the morph itself
        var sim2 = new Simulation(setup);
        EntityId h2 = sim2.PlaceBuilding(0, Hatch, new CellCoord(6, 25));
        Step(sim2, Cmd(sim2, CommandKind.Upgrade, h2, default, BoreHead));
        Run(sim2, 20);
        Assert.NotEqual(0, sim2.Tunnels.BuildingMorphType[h2.Index]);
        AssertSameFuture(sim2, RoundTrip(sim2, setup), 80);
    }

    /// <summary>A busy scripted game: burrowers crossing paths, a Bore, a Hatch upgrade, a land-train and an enemy.</summary>
    private static Simulation Scripted(ulong seed, int ticks, List<ulong>? hashes = null)
    {
        var sim = new Simulation(Setup(seed: seed));
        var units = new List<EntityId>();
        for (int k = 0; k < 6; k++) units.Add(sim.SpawnVehicle(0, k % 2 == 0 ? Burrower : Gunner, new CellCoord(4 + k * 2, 4)));
        EntityId bore = sim.SpawnVehicle(0, Bore, new CellCoord(20, 28));
        EntityId train = sim.SpawnVehicle(0, Train, new CellCoord(40, 20));
        EntityId hatch = sim.PlaceBuilding(0, Hatch, new CellCoord(5, 28));
        sim.PlaceBuilding(1, House, new CellCoord(30, 14));
        sim.SpawnVehicle(1, Enemy, new CellCoord(44, 4));
        var rng = new SimRandom(seed + 5);
        for (int t = 0; t < ticks; t++)
        {
            var cmds = new List<Command>();
            if (t == 3) cmds.Add(DeployAt(sim, bore, 24, 8));
            if (t == 50) cmds.Add(Cmd(sim, CommandKind.Upgrade, hatch, default, BoreHead));
            if (t == 120) cmds.Add(Cmd(sim, CommandKind.Deploy, train, default, (int)ActorKind.Vehicle));
            if (t % 37 == 0)
            {
                EntityId who = units[rng.Next(units.Count)];
                cmds.Add(MoveTo(sim, who, rng.Next(2, 46), rng.Next(2, 30)));
            }
            if (t % 91 == 90) cmds.Add(Cmd(sim, CommandKind.Stop, units[rng.Next(units.Count)], default, (int)ActorKind.Vehicle));
            sim.Step(cmds);
            hashes?.Add(sim.ComputeStateHash());
        }
        return sim;
    }

    [Fact]
    public void Determinism_SameInputsSameHashesEveryTick()
    {
        var h1 = new List<ulong>(); var h2 = new List<ulong>();
        Simulation a = Scripted(3, 700, h1), b = Scripted(3, 700, h2);
        Assert.Equal(h1, h2);
        Assert.Equal(a.ComputeSubHashes(), b.ComputeSubHashes());
        // the script really exercised the feature: units surfaced, a Hatch was dug and the upgrade finished
        Assert.False(a.Units.Slots.IsAlive(new EntityId(7, 1)) && a.Tunnels.VehicleLayer[7] != ActorLayer.Surface);
        Assert.Equal(BoreHead, a.Buildings.TypeIndex[a.Map.BuildingAt(new CellCoord(5, 28)).Index]);
        Simulation c = Scripted(4, 700);
        Assert.NotEqual(a.ComputeStateHash(), c.ComputeStateHash());
    }

    [Fact]
    public void Determinism_ASavedAndReloadedScriptedGameMatchesTheStraightRun()
    {
        var straight = new List<ulong>();
        Scripted(3, 400, straight);
        // replay the same script, but swap the sim for a reloaded copy at tick 200
        SimSetup setup = Setup(seed: 3);
        var sim = new Simulation(setup);
        var units = new List<EntityId>();
        for (int k = 0; k < 6; k++) units.Add(sim.SpawnVehicle(0, k % 2 == 0 ? Burrower : Gunner, new CellCoord(4 + k * 2, 4)));
        EntityId bore = sim.SpawnVehicle(0, Bore, new CellCoord(20, 28));
        EntityId train = sim.SpawnVehicle(0, Train, new CellCoord(40, 20));
        EntityId hatch = sim.PlaceBuilding(0, Hatch, new CellCoord(5, 28));
        sim.PlaceBuilding(1, House, new CellCoord(30, 14));
        sim.SpawnVehicle(1, Enemy, new CellCoord(44, 4));
        var rng = new SimRandom(3 + 5);
        var replay = new List<ulong>();
        for (int t = 0; t < 400; t++)
        {
            if (t == 200) sim = RoundTrip(sim, setup);
            var cmds = new List<Command>();
            if (t == 3) cmds.Add(DeployAt(sim, bore, 24, 8));
            if (t == 50) cmds.Add(Cmd(sim, CommandKind.Upgrade, hatch, default, BoreHead));
            if (t == 120) cmds.Add(Cmd(sim, CommandKind.Deploy, train, default, (int)ActorKind.Vehicle));
            if (t % 37 == 0)
            {
                EntityId who = units[rng.Next(units.Count)];
                cmds.Add(MoveTo(sim, who, rng.Next(2, 46), rng.Next(2, 30)));
            }
            if (t % 91 == 90) cmds.Add(Cmd(sim, CommandKind.Stop, units[rng.Next(units.Count)], default, (int)ActorKind.Vehicle));
            sim.Step(cmds);
            replay.Add(sim.ComputeStateHash());
        }
        Assert.Equal(straight, replay);
    }
}
