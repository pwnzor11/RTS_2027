// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.Golden;

/// <summary>
/// M6-12 golden replay for Left Behind: one scripted game on hand-made rules (so balance data changes do not move it) that packs a rig
/// building, unpacks two packed rigs, salvages a wreck, plunders an enemy silo with a raider and fires the Second Drift storm; the
/// game is saved and loaded in the middle and must carry on with the same hashes. The hash of every 16th tick is compared with
/// <c>golden_left_behind_hashes.txt</c>; <c>RTS_UPDATE_GOLDEN=1</c> regenerates it.
/// </summary>
public class LeftBehindGoldenTests
{
    private const string UpdateVariable = "RTS_UPDATE_GOLDEN";
    private const int TotalTicks = 900, HashInterval = 16, SaveTick = 320;
    private const string FailureHint =
        "Sim behaviour changed. If intended, rerun with RTS_UPDATE_GOLDEN=1 and commit golden_left_behind_hashes.txt with a note in the commit message.";

    // vehicles (sorted by id): a_truck raider, b_harv salvager, c_rig packed rig, d_victim
    private const int Truck = 0, Harv = 1, Rig = 2, Victim = 3;
    // buildings (sorted by id): a_hut, b_plant, c_refinery, d_rigbase, e_silo, f_sun, g_hatch
    private const int Hut = 0, Plant = 1, Refinery = 2, RigBase = 3, Silo = 4, Sun = 5, Hatch = 6;
    private static readonly CellCoord WreckCell = new(20, 20);

    private static SimRules Rules()
    {
        var none = Array.Empty<string>();
        var vehicles = new[]
        {
            ScriptedGame.Vehicle("a_truck", 37, 100) with { Raider = true },
            ScriptedGame.Vehicle("b_harv", 11, 200) with { HarvestCapacity = 20, SalvageRate = 20, Cost = 800 },
            ScriptedGame.Vehicle("c_rig", 11, 200) with { UnpacksInto = RigBase, PackTicks = 20 },
            ScriptedGame.Vehicle("d_victim", 11, 200) with { Cost = 400 },
        };
        var buildings = new[]
        {
            ScriptedGame.Building("a_hut", 3, 3, 500),
            new BuildingType("b_plant", 2, 2, 5000, ArmorClass.Structure, 5, -1, 0, 300, none, new[] { "b_plant" }),
            ScriptedGame.Building("c_refinery", 3, 3, 500) with { Storage = 2000, Refinery = true },
            ScriptedGame.Building("d_rigbase", 2, 2, 400) with { PacksInto = Rig, PackTicks = 20, Produces = ProductionCategory.Structure },
            ScriptedGame.Building("e_silo", 2, 2, 300) with { Storage = 1500 },
            new BuildingType("f_sun", 2, 2, 5000, ArmorClass.Structure, 5, -1, 0, -20, none, new[] { "f_sun" })
                { Superweapon = "sw", SuperweaponChargeTicks = 100, SuperweaponEntranceRange = 8 },
            ScriptedGame.Building("g_hatch", 2, 2, 500) with { Entrance = TunnelSize.S },
        };
        var storm = new WarheadType("sw_blast", new[] { 100, 100, 100, 100, 100 }, 1, false)
            { StormRadius = 3, StormTicks = 200, StormContaminateStep = 20, StormContaminateCap = 64, StormSpeedPermille = 500 };
        SimRules r = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>());
        return r with
        {
            Buildings = buildings,
            Warheads = new[] { storm },
            Economy = r.Economy with { WreckTicks = 1200, WreckMaxValue = 1000, PlunderCap = 1000, SpoilsPermille = 250 },
        };
    }

    private static Simulation NewSim()
    {
        var setup = new SimSetup(7, MapData.CreateFlat(64, 64), 2, Rules(), 0x60DE1UL)
        {
            AllowDebug = true,
            Factions = new[] { Faction.LeftBehind, Faction.Authority },
        };
        var sim = new Simulation(setup);
        sim.PlaceBuilding(0, Plant, new CellCoord(4, 4));
        sim.PlaceBuilding(0, Refinery, new CellCoord(10, 14));
        sim.PlaceBuilding(0, RigBase, new CellCoord(4, 10));
        sim.PlaceBuilding(1, Hut, new CellCoord(56, 56));
        sim.PlaceBuilding(1, Silo, new CellCoord(40, 8));
        EntityId sun = sim.PlaceBuilding(0, Sun, new CellCoord(8, 4));
        sim.Effects.BuildingCharge[sun.Index] = 100;
        sim.PlaceBuilding(0, Hatch, new CellCoord(16, 4));
        sim.Players.Stored[1] = 1000;
        sim.SpawnVehicle(0, Rig, new CellCoord(30, 30));
        sim.SpawnVehicle(0, Harv, new CellCoord(14, 20));
        sim.SpawnVehicle(0, Truck, new CellCoord(30, 8));
        sim.SpawnVehicle(1, Victim, WreckCell);
        return sim;
    }

    [Fact]
    public void LeftBehindScript_MatchesGoldenHashes_AndSurvivesSaveLoad()
    {
        List<string> actual = RunScenario();
        string path = GoldenPath();

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(path, string.Join("\n", actual) + "\n");
            return;
        }

        Assert.True(File.Exists(path), $"{FailureHint} Golden file not found: {path}");
        string[] expected = File.ReadAllLines(path);
        int common = Math.Min(expected.Length, actual.Count);
        for (int i = 0; i < common; i++)
        {
            if (expected[i] != actual[i])
            {
                Assert.Fail($"{FailureHint}\nFirst difference at line {i + 1}: expected '{expected[i]}', actual '{actual[i]}'.");
            }
        }
        Assert.True(expected.Length == actual.Count,
            $"{FailureHint}\nLine count differs: expected {expected.Length}, actual {actual.Count}.");
    }

    [Fact]
    public void LeftBehindScript_ReallyExercisesTheFeatures()
    {
        // Guards the golden against a script that silently does nothing (an order refused, a footprint blocked).
        Simulation sim = NewSim();
        int cash0 = sim.Players.Cash[0];
        bool packedToRig = false, unpackedBoth = false, storm = false;
        int buildingsStart = sim.Buildings.Slots.Count;
        Play(sim, tick =>
        {
            if (tick > 60 && FindRigNear(sim, 4, 10) >= 0) packedToRig = true;
            if (sim.Effects.StormTicks[0] > 0) storm = true;
            if (tick == TotalTicks - 1) unpackedBoth = sim.Buildings.Slots.Count >= buildingsStart + 1;
        });
        Assert.True(packedToRig, "the rig building packed into a vehicle");
        Assert.True(unpackedBoth, "a packed rig unpacked into a building");
        Assert.True(storm, "the storm was placed");
        Assert.True(sim.Players.Stored[1] < 1000, "the raider plundered the silo");
        Assert.True(sim.StatsOf(0).CreditsHarvested > 0, "credits came in from salvage or plunder");
        Assert.True(sim.Players.Cash[0] + sim.Players.Stored[0] > cash0 - 1, "the owner is not poorer than at the start");
    }

    /// <summary>Index of a packed rig vehicle standing near (x,y), or -1.</summary>
    private static int FindRigNear(Simulation sim, int x, int y)
    {
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == 0 && sim.Rigs.IsPackedRig(i)
                && Math.Abs(sim.Units.Position[i].ToCell().X - x) <= 3 && Math.Abs(sim.Units.Position[i].ToCell().Y - y) <= 3) return i;
        return -1;
    }

    /// <summary>Runs the script on <paramref name="sim"/>, calling <paramref name="afterStep"/> with the new tick after every Step.</summary>
    private static void Play(Simulation sim, Action<int> afterStep)
    {
        for (int step = 0; step < TotalTicks; step++)
        {
            sim.Step(CommandsFor(sim));
            afterStep(sim.Tick);
        }
    }

    /// <summary>The scripted orders for the tick the sim is about to run.</summary>
    private static Command[] CommandsFor(Simulation sim)
    {
        var cmds = new List<Command>();
        int t = sim.Tick;
        Command C(byte p, CommandKind k, EntityId subject, EntityId target, CellCoord cell, int arg, int arg2 = 0) =>
            new(t, p, (ushort)(1000 + t * 4 + cmds.Count), k, subject, target, cell, arg, arg2);
        if (t == 4)
        {
            // kill the enemy vehicle: it leaves a wreck for the salvager
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == 1)
                    cmds.Add(C(1, CommandKind.Debug, sim.Units.Slots.IdOf(i), default, default, (int)DebugOp.Kill, (int)ActorKind.Vehicle));
        }
        if (t == 12)
        {
            for (int i = 1; i < sim.Units.Capacity; i++)
            {
                if (!sim.Units.Slots.IsIndexAlive(i) || sim.Units.Player[i] != 0) continue;
                EntityId id = sim.Units.Slots.IdOf(i);
                int type = sim.Units.TypeIndex[i];
                if (type == Harv) cmds.Add(C(0, CommandKind.Harvest, id, default, WreckCell, (int)ActorKind.Vehicle));
                if (type == Truck)
                    cmds.Add(C(0, CommandKind.Enter, id, sim.Map.BuildingAt(new CellCoord(40, 8)), default, (int)ActorKind.Vehicle | ((int)ActorKind.Building << 8)));
            }
        }
        if (t == 20)
        {
            cmds.Add(C(0, CommandKind.Pack, sim.Map.BuildingAt(new CellCoord(4, 10)), default, default, 0));   // rig building -> vehicle
            for (int i = 1; i < sim.Units.Capacity; i++)
                if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == 0 && sim.Units.TypeIndex[i] == Rig && sim.Rigs.IsPackedRig(i))
                    cmds.Add(C(0, CommandKind.Pack, sim.Units.Slots.IdOf(i), default, new CellCoord(36, 30), 0));   // packed rig -> building
        }
        if (t == 80)
        {
            int rig = FindRigNear(sim, 4, 10);
            if (rig >= 0) cmds.Add(C(0, CommandKind.Pack, sim.Units.Slots.IdOf(rig), default, new CellCoord(20, 40), 0));   // second unpack
        }
        if (t == 200)
        {
            EntityId sun = sim.Map.BuildingAt(new CellCoord(8, 4));
            cmds.Add(C(0, CommandKind.Special, sun, default, new CellCoord(21, 8), 0));
        }
        return cmds.ToArray();
    }

    /// <summary>Runs the scenario, saving and loading in the middle; returns one <c>tick hash_hex</c> line per recorded tick.</summary>
    private static List<string> RunScenario()
    {
        Simulation sim = NewSim();
        Simulation twin = NewSim();   // same script, never saved: the loaded game must stay on its hashes
        var lines = new List<string>();
        for (int step = 0; step < TotalTicks; step++)
        {
            sim.Step(CommandsFor(sim));
            twin.Step(CommandsFor(twin));
            if (sim.Tick == SaveTick)
            {
                var ms = new MemoryStream();
                sim.Save(ms);
                ms.Position = 0;
                sim = Simulation.Load(ms, sim.Setup);
                Assert.Equal(twin.ComputeStateHash(), sim.ComputeStateHash());
            }
            if (sim.Tick % HashInterval == 0)
            {
                Assert.True(twin.ComputeStateHash() == sim.ComputeStateHash(), $"save/load diverged at tick {sim.Tick}");
                lines.Add($"{sim.Tick} {sim.ComputeStateHash():x16}");
            }
        }
        return lines;
    }

    private static string GoldenPath()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Sim.Tests.csproj")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        if (dir is null)
        {
            throw new InvalidOperationException($"Sim.Tests.csproj not found above {AppContext.BaseDirectory}.");
        }
        return Path.Combine(dir, "Golden", "golden_left_behind_hashes.txt");
    }
}
