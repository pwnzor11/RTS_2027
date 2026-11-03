// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.Save;

/// <summary>
/// Save/load at many points of one long game (review A-6): a combat-and-building script runs for 2000 ticks; every 37
/// ticks the game is saved, loaded into a fresh sim and stepped alongside the unbroken run, and the full hash and the
/// sub-hashes must agree on every tick. State that is in neither the save nor the hash but changes behaviour shows up here.
/// </summary>
public class SaveLoadSweepTests
{
    private const int Ticks = 2000, SaveEvery = 37, Follow = 200;

    private static (Simulation Sim, List<Command> Script) NewGame()
    {
        var sim = NewSim(seed: 5, size: 48);
        var script = new List<Command>();
        string[] vehicles = { "test_tank", "test_tank", "test_jeep", "test_jeep", "test_artillery_truck" };
        var armies = new List<(byte Player, ActorKind Kind, EntityId Id)>();
        for (byte p = 0; p < 2; p++)
        {
            int x0 = p == 0 ? 6 : 40;
            for (int k = 0; k < vehicles.Length; k++)
                armies.Add((p, ActorKind.Vehicle, sim.SpawnVehicle(p, Veh(vehicles[k]), new CellCoord(x0, 18 + k * 2))));
            for (int k = 0; k < 3; k++)
                armies.Add((p, ActorKind.Infantry, sim.SpawnInfantry(p, Inf("test_rifleman"), new CellCoord(x0 + (p == 0 ? 1 : -1), 20 + k * 3), k)));
            sim.PlaceBuilding(p, Bld("test_hq"), new CellCoord(p == 0 ? 2 : 43, 4));
        }
        // Attack-move waves, so the fight keeps going for the whole run; every unit is re-ordered every 400 ticks.
        for (int wave = 0; wave * 400 < Ticks; wave++)
            foreach (var (p, kind, id) in armies)
            {
                int toward = (p == 0) == (wave % 2 == 0) ? 40 : 6;
                script.Add(AttackMove(sim, p, kind, id, toward, 16 + (wave * 5) % 14) with { ExecTick = wave * 400 + 1 });
            }
        // Buildings come and go during the fight.
        ushort seq = 40000;
        void Add(int tick, byte player, CommandKind kind, EntityId subject, CellCoord cell, int arg) =>
            script.Add(new Command(tick, player, seq++, kind, subject, default, cell, arg));
        Add(10, 0, CommandKind.Place, default, new CellCoord(10, 30), Bld("test_turret"));
        Add(10, 1, CommandKind.Place, default, new CellCoord(36, 30), Bld("test_turret"));
        Add(600, 0, CommandKind.Place, default, new CellCoord(14, 8), Bld("test_turret"));
        Add(900, 1, CommandKind.Place, default, new CellCoord(30, 8), Bld("test_hq"));
        Add(1300, 1, CommandKind.Sell, new EntityId(2, 1), default, (int)ActorKind.Building);
        Add(1500, 0, CommandKind.Place, default, new CellCoord(8, 36), Bld("test_hq"));
        Add(1800, 0, CommandKind.Debug, default, default, 1);
        return (sim, script);
    }

    [Fact]
    public void SaveAndLoadAtEvery37thTick_FollowsTheUnbrokenGameForTwoHundredTicks()
    {
        var (sim, script) = NewGame();
        int total = Ticks + Follow;
        var fullHashes = new ulong[total + 1];
        var subHashes = new IReadOnlyList<(string Part, ulong Hash)>[total + 1];
        var saves = new List<(int Tick, byte[] Bytes)>();
        int peakBuildings = 0, casualtyFloor = sim.Units.Slots.Count + sim.Infantry.Slots.Count;

        subHashes[0] = sim.ComputeHashes(out ulong f0); fullHashes[0] = f0;
        for (int t = 0; t < total; t++)
        {
            if (t < Ticks && t % SaveEvery == 0)
            {
                using var ms = new MemoryStream();
                sim.Save(ms);
                saves.Add((t, ms.ToArray()));
            }
            sim.Step(script);
            subHashes[sim.Tick] = sim.ComputeHashes(out ulong full);
            fullHashes[sim.Tick] = full;
            peakBuildings = Math.Max(peakBuildings, sim.Buildings.Slots.Count);
            casualtyFloor = Math.Min(casualtyFloor, sim.Units.Slots.Count + sim.Infantry.Slots.Count);
        }
        Assert.Equal(Ticks / SaveEvery + 1, saves.Count);
        Assert.True(peakBuildings >= 5, "buildings should be placed during the game");
        Assert.True(casualtyFloor < 16, "the armies should actually fight");

        foreach ((int saveTick, byte[] bytes) in saves)
        {
            var loaded = Simulation.Load(new MemoryStream(bytes), sim.Setup);
            Assert.Equal(saveTick, loaded.Tick);
            Assert.Equal(fullHashes[saveTick], loaded.ComputeStateHash());
            Assert.Equal(subHashes[saveTick], loaded.ComputeSubHashes());
            for (int k = 0; k < Follow; k++)
            {
                loaded.Step(script);
                int tick = loaded.Tick;
                if (fullHashes[tick] != loaded.ComputeStateHash() || !subHashes[tick].SequenceEqual(loaded.ComputeSubHashes()))
                    Assert.Fail($"load at tick {saveTick} diverged at tick {tick} in part {Simulation.FirstDifferentPart(subHashes[tick], loaded.ComputeSubHashes())}");
            }
        }
    }
}
