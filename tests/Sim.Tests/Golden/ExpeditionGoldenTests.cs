// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.Golden;

/// <summary>
/// M7-14 golden replay for the Expedition: one scripted game on hand-made rules (so balance data changes do not move it) with a Command Ship
/// that deploys into a depot, a landing craft that carries two marines and a tank out to sea and puts them ashore, a shipment dock whose
/// first tender is sunk and whose later tenders arrive, a submarine that dives and surfaces, a beacon placed at the edge of the beachhead
/// and a Salvo Array strike. The game is saved and loaded at three points (mid-voyage, mid-salvo, mid-unload) and must carry on with the
/// same hashes as a twin that is never saved. The hash of every 16th tick is compared with <c>golden_expedition_hashes.txt</c>;
/// <c>RTS_UPDATE_GOLDEN=1</c> regenerates it.
/// </summary>
public class ExpeditionGoldenTests
{
    private const string UpdateVariable = "RTS_UPDATE_GOLDEN";
    private const int TotalTicks = 900, HashInterval = 16;
    private static readonly int[] SaveTicks = { 130, 290, 352 };
    private const string FailureHint =
        "Sim behaviour changed. If intended, rerun with RTS_UPDATE_GOLDEN=1 and commit golden_expedition_hashes.txt with a note in the commit message.";

    // vehicles (sorted by id): a_brute enemy target, b_craft landing craft, c_cship command ship, d_sub, e_tank, f_tender
    private const int Brute = 0, Craft = 1, CShip = 2, Sub = 3, Tank = 4, Tender = 5;
    // infantry: a_marine
    private const int Marine = 0;
    // buildings (sorted by id): a_beacon, b_depot, c_dock, d_hatch, e_hut, f_plant, g_sun
    private const int Beacon = 0, Depot = 1, Dock = 2, Hatch = 3, Hut = 4, Plant = 5, Sun = 6;
    private const int W = 64, H = 48, Shore = 14;   // water x &lt; 12, shoals 12..13, beach (Coast) x = 14, ground beyond

    private static readonly CellCoord CraftStart = new(13, 24), CraftOut = new(6, 30), CraftHome = new(13, 30), LandingCell = new(14, 30);
    private static readonly CellCoord SubStart = new(4, 40), SubFar = new(8, 44), ShipStart = new(13, 10);
    private static readonly CellCoord DockCell = new(14, 16), PlantCell = new(20, 4), HatchCell = new(22, 12), SunCell = new(20, 16), BeaconCell = new(24, 18);
    private static readonly CellCoord Target = new(30, 14);

    private static SimRules Rules()
    {
        var none = Array.Empty<string>();
        VehicleType Ship(string id, int speed, int hp) => ScriptedGame.Vehicle(id, speed, hp) with { Locomotion = Locomotion.Naval, TurnRate = 16 };
        var vehicles = new[]
        {
            ScriptedGame.Vehicle("a_brute", 16, 1000),
            Ship("b_craft", 40, 100) with { Passengers = 4, UnloadTicks = 12, VehicleSlots = 1 },
            Ship("c_cship", 30, 300) with { DeploysInto = Depot },
            Ship("d_sub", 40, 100) with { Submerges = true },
            ScriptedGame.Vehicle("e_tank", 40, 200),
            Ship("f_tender", 30, 100),
        };
        var buildings = new[]
        {
            ScriptedGame.Building("a_beacon", 1, 1, 50) with { BeachheadSource = true, BeachheadRadius = 6 },
            ScriptedGame.Building("b_depot", 3, 3, 800) with { BeachheadSource = true, BeachheadRadius = 10 },
            ScriptedGame.Building("c_dock", 2, 2, 300) with { ShipmentDock = true },
            ScriptedGame.Building("d_hatch", 2, 2, 500) with { Entrance = TunnelSize.S },
            ScriptedGame.Building("e_hut", 3, 3, 500),
            new BuildingType("f_plant", 2, 2, 5000, ArmorClass.Structure, 5, -1, 0, 300, none, new[] { "f_plant" }),
            new BuildingType("g_sun", 2, 2, 5000, ArmorClass.Structure, 5, -1, 0, -20, none, new[] { "g_sun" })
                { Superweapon = "sw", SuperweaponChargeTicks = 100, SuperweaponEntranceRange = 8 },
        };
        var salvo = new WarheadType("sw_blast", new[] { 100, 100, 100, 100, 100 }, 1, false)
            { SalvoMissiles = 4, SalvoTicks = 40, SalvoRadius = 2, SalvoDamage = 100, SalvoWarnTicks = 20, SalvoSplash = 0 };

        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        foreach (LandType land in Enum.GetValues<LandType>())
            for (int l = 0; l < RuleCounts.Locomotions; l++)
            {
                var loco = (Locomotion)l;
                bool water = land is LandType.Water or LandType.Shallow;
                speed[(int)land * RuleCounts.Locomotions + l] = loco == Locomotion.Air ? 100
                    : loco == Locomotion.Naval ? (water ? 100 : 0)
                    : water || land is LandType.Rock or LandType.Cliff ? 0 : 100;
            }
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        buildable[(int)LandType.Coast] = true;
        var landing = new bool[RuleCounts.LandTypes];
        landing[(int)LandType.Coast] = true;

        SimRules r = ScriptedGame.RulesWith(vehicles, new[] { ScriptedGame.Infantry("a_marine", 9, 50) });
        return r with
        {
            Buildings = buildings,
            Warheads = new[] { salvo },
            Terrain = new TerrainTable(speed, buildable, null, landing),
            Shipments = new ShipmentRules
            {
                Enabled = true, TenderType = "f_tender", IntervalTicks = 120, Value = 1000, DecayPermille = 100, FloorPermille = 400,
                CharterTicks = 0, TendersMax = 3,
            },
        };
    }

    private static MapData Map()
    {
        var land = new LandType[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                land[y * W + x] = x < 12 ? LandType.Water : x < Shore ? LandType.Shallow : x == Shore ? LandType.Coast : LandType.Clear;
        return new MapData("expedition_golden", "rust_belt", W, H, new ushort[W * H], land, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(2, 2), new CellCoord(W - 3, H - 3) });
    }

    private static Simulation NewSim()
    {
        var setup = new SimSetup(7, Map(), 2, Rules(), 0xE7D17UL)
        {
            AllowDebug = true,
            Factions = new[] { Faction.Expedition, Faction.Authority },
        };
        var sim = new Simulation(setup);
        sim.PlaceBuilding(1, Hut, new CellCoord(58, 42));
        sim.SpawnVehicle(0, CShip, ShipStart);
        sim.SpawnVehicle(0, Craft, CraftStart);
        sim.SpawnVehicle(0, Tank, new CellCoord(Shore, 24));
        sim.SpawnInfantry(0, Marine, new CellCoord(Shore, 23));
        sim.SpawnInfantry(0, Marine, new CellCoord(Shore, 25));
        sim.SpawnVehicle(0, Sub, SubStart);
        // a 5x5 square of enemy tanks for the Salvo Array to land on
        for (int y = Target.Y - 2; y <= Target.Y + 2; y++)
            for (int x = Target.X - 2; x <= Target.X + 2; x++)
                sim.SpawnVehicle(1, Brute, new CellCoord(x, y));
        return sim;
    }

    [Fact]
    public void ExpeditionScript_MatchesGoldenHashes_AndSurvivesSaveLoad()
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
    public void ExpeditionScript_ReallyExercisesTheFeatures()
    {
        // Guards the golden against a script that silently does nothing (an order refused, a footprint blocked).
        Simulation sim = NewSim();
        bool deployed = false, dived = false, surfaced = false, salvo = false, tenderSunk = false, beaconZoneGrew = false, aboard = false;
        int zoneBeforeBeacon = 0, enemyHp0 = EnemyHp(sim);
        Play(sim, tick =>
        {
            if (CountBuildings(sim, Depot) == 1 && !deployed) { deployed = true; Assert.Equal(0, CountUnits(sim, 0, CShip)); }
            int craft = FindUnit(sim, 0, Craft);
            if (craft >= 0 && sim.PassengersIn(sim.Units.Slots.IdOf(craft)) == 3) aboard = true;
            int sub = FindUnit(sim, 0, Sub);
            if (sub >= 0 && sim.Naval.Submerged[sub]) dived = true;
            if (sub >= 0 && dived && !sim.Naval.Submerged[sub]) surfaced = true;
            if (sim.Effects.AnySalvo) salvo = true;
            if (sim.StatsOf(0).UnitsLost >= 1) tenderSunk = true;
            if (tick == 99) zoneBeforeBeacon = ZoneCells(sim);
            if (tick == 101 && CountBuildings(sim, Beacon) == 1 && ZoneCells(sim) > zoneBeforeBeacon) beaconZoneGrew = true;
        });
        Assert.True(deployed, "the Command Ship deployed into a depot");
        Assert.True(aboard, "two marines and a tank boarded the landing craft");
        Assert.True(beaconZoneGrew, "the beacon was placed and widened the beachhead");
        Assert.True(dived && surfaced, "the submarine dived and surfaced");
        Assert.True(tenderSunk, "a tender was sunk");
        Assert.True(sim.StatsOf(0).ShipmentIncome > 0, "a later shipment was delivered");
        Assert.True(salvo, "the Salvo Array fired");
        Assert.Equal(4 * 100, enemyHp0 - EnemyHp(sim));   // four missiles of 100 damage, each on a separate tank
        // the landing: nobody is aboard any more, the marines and the tank stand ashore
        int landed = FindUnit(sim, 0, Craft);
        Assert.True(landed >= 0 && sim.PassengersIn(sim.Units.Slots.IdOf(landed)) == 0, "the craft is empty");
        int tank = FindUnit(sim, 0, Tank);
        Assert.True(tank >= 0 && sim.Units.Position[tank].ToCell().X >= Shore, "the tank is ashore");
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == 0)
                Assert.True(sim.Infantry.Position[i].ToCell().X >= Shore, "the marine is ashore");
    }

    private static int ZoneCells(Simulation sim)
    {
        int n = 0;
        foreach (ulong w in sim.Beachhead.Zone[0]) n += System.Numerics.BitOperations.PopCount(w);
        return n;
    }

    private static int EnemyHp(Simulation sim)
    {
        int hp = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == 1) hp += sim.Units.Health[i];
        return hp;
    }

    private static int CountBuildings(Simulation sim, int type)
    {
        int n = 0;
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == 0 && sim.Buildings.TypeIndex[i] == type) n++;
        return n;
    }

    private static int CountUnits(Simulation sim, byte player, int type)
    {
        int n = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player && sim.Units.TypeIndex[i] == type) n++;
        return n;
    }

    /// <summary>Slot index of the first live vehicle of that player and type, or -1.</summary>
    private static int FindUnit(Simulation sim, byte player, int type)
    {
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player && sim.Units.TypeIndex[i] == type) return i;
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
        int veh = (int)ActorKind.Vehicle, inf = (int)ActorKind.Infantry;
        Command C(CommandKind k, EntityId subject, EntityId target, CellCoord cell, int arg, int arg2 = 0) =>
            new(t, 0, (ushort)(1000 + t * 8 + cmds.Count), k, subject, target, cell, arg, arg2);
        EntityId Id(int slot) => sim.Units.Slots.IdOf(slot);

        if (t == 2)
        {
            int ship = FindUnit(sim, 0, CShip);
            if (ship >= 0) cmds.Add(C(CommandKind.Deploy, Id(ship), default, ShipStart, 0));
        }
        if (t == 12)
        {
            // the depot stands now: base buildings inside its beachhead zone (free placement, the sim is a debug sim)
            cmds.Add(C(CommandKind.Place, default, default, PlantCell, Plant));
            cmds.Add(C(CommandKind.Place, default, default, HatchCell, Hatch));
            cmds.Add(C(CommandKind.Place, default, default, DockCell, Dock));
            cmds.Add(C(CommandKind.Place, default, default, SunCell, Sun));
        }
        if (t == 14)
        {
            // board: two marines and the tank
            int craft = FindUnit(sim, 0, Craft), tank = FindUnit(sim, 0, Tank);
            if (craft >= 0)
            {
                for (int i = 1; i < sim.Infantry.Capacity; i++)
                    if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == 0)
                        cmds.Add(C(CommandKind.Enter, sim.Infantry.Slots.IdOf(i), Id(craft), default, inf | (veh << 8)));
                if (tank >= 0) cmds.Add(C(CommandKind.Enter, Id(tank), Id(craft), default, veh | (veh << 8)));
            }
        }
        if (t == 60)
        {
            int craft = FindUnit(sim, 0, Craft), sub = FindUnit(sim, 0, Sub);
            if (craft >= 0) cmds.Add(C(CommandKind.Move, Id(craft), default, CraftOut, veh));
            if (sub >= 0) cmds.Add(C(CommandKind.Deploy, Id(sub), default, default, veh));   // dive
        }
        if (t == 100)
        {
            cmds.Add(C(CommandKind.Place, default, default, BeaconCell, Beacon));
            int sub = FindUnit(sim, 0, Sub);
            if (sub >= 0) cmds.Add(C(CommandKind.Move, Id(sub), default, SubFar, veh));
        }
        if (t == 160 && sim.Shipments.Tenders.Count > 0)
        {
            // sink the first tender on its way in (Debug kill)
            var tender = sim.Shipments.Tenders[0];
            EntityId vid = sim.Units.Slots.IdOf((ushort)((uint)tender.Vehicle >> 16));
            cmds.Add(C(CommandKind.Debug, vid, default, default, (int)DebugOp.Kill, veh));
        }
        if (t == 200)
        {
            int craft = FindUnit(sim, 0, Craft);
            if (craft >= 0) cmds.Add(C(CommandKind.Move, Id(craft), default, CraftHome, veh));
        }
        if (t == 250)
        {
            int sub = FindUnit(sim, 0, Sub);
            if (sub >= 0) cmds.Add(C(CommandKind.Deploy, Id(sub), default, default, veh));   // surface
            EntityId sun = sim.Map.BuildingAt(SunCell);
            if (!sun.IsNone) cmds.Add(C(CommandKind.Special, sun, default, Target, 0));
        }
        if (t == 330)
        {
            int craft = FindUnit(sim, 0, Craft);
            if (craft >= 0) cmds.Add(C(CommandKind.Unload, Id(craft), default, LandingCell, veh));
        }
        return cmds.ToArray();
    }

    /// <summary>Runs the scenario, saving and loading at <see cref="SaveTicks"/>; returns one <c>tick hash_hex</c> line per recorded tick.</summary>
    private static List<string> RunScenario()
    {
        Simulation sim = NewSim();
        Simulation twin = NewSim();   // same script, never saved: the loaded game must stay on its hashes
        var lines = new List<string>();
        for (int step = 0; step < TotalTicks; step++)
        {
            sim.Step(CommandsFor(sim));
            twin.Step(CommandsFor(twin));
            if (Array.IndexOf(SaveTicks, sim.Tick) >= 0)
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
        return Path.Combine(dir, "Golden", "golden_expedition_hashes.txt");
    }
}
