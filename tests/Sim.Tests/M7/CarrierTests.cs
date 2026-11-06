// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Naval;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using static Rts.Sim.Tests.M4.RulesTierFixture;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-09: the carrier hangar (Simulation.Carrier.cs). Map: sea for x &lt; 16, grass beyond. Types: "carrier" (naval, 2 places,
/// sortie 150 ticks), "jet" (air, gun), "hook" (air, 6 seats), "tank" (tracked, enemy target).</summary>
public sealed class CarrierTests
{
    private const int Carrier = 0, Jet = 1, Hook = 2, Tank = 3, Gun = 0, W = 40, H = 30, Sortie = 150;
    private ushort _seq;
    private EntityId _third, _hook;

    private static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        foreach (LandType land in Enum.GetValues<LandType>())
            for (int l = 0; l < RuleCounts.Locomotions; l++)
            {
                var loco = (Locomotion)l;
                bool water = land is LandType.Water or LandType.Shallow;
                int v = loco == Locomotion.Air ? 100 : loco == Locomotion.Naval ? (water ? 100 : 0) : water ? 0 : 100;
                speed[(int)land * RuleCounts.Locomotions + l] = v;
            }
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        return new TerrainTable(speed, buildable);
    }

    private static SimSetup Setup(int tankHp = 5000)
    {
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) cells[y * W + x] = x < 16 ? LandType.Water : LandType.Clear;
        var map = new MapData("sea", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        VehicleType[] vehicles =
        {
            ScriptedGame.Vehicle("carrier", 12, 500) with { Locomotion = Locomotion.Naval, TurnRate = 16, AircraftSlots = 2, SortieTicks = Sortie },
            ScriptedGame.Vehicle("jet", 60, 100) with { Locomotion = Locomotion.Air, TurnRate = 32, PrimaryWeapon = Gun },
            ScriptedGame.Vehicle("hook", 40, 80) with { Locomotion = Locomotion.Air, Passengers = 6 },
            ScriptedGame.Vehicle("tank", 20, tankHp),
        };
        var weapons = new[] { new WeaponType("gun", 10, 6 * 256, 0, 10, 0, 0) { Targets = WeaponTargets.Both } };
        var warheads = new[] { new WarheadType("w", new[] { 100, 100, 100, 100, 100, 100, 100, 100 }, 0, false) };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>()) with { Weapons = weapons, Warheads = warheads, Terrain = Terrain() };
        return new SimSetup(5, map, 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private Command Cmd(Simulation s, CommandKind k, EntityId who, EntityId target = default, byte player = 0, int arg = (int)ActorKind.Vehicle) =>
        new(s.Tick, player, ++_seq, k, who, target, default, arg);

    private Command Dock(Simulation s, EntityId jet, EntityId carrier) =>
        Cmd(s, CommandKind.Enter, jet, carrier, 0, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8));

    private Command Strike(Simulation s, EntityId carrier, EntityId target) =>
        Cmd(s, CommandKind.Attack, carrier, target, 0, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8));

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private static byte State(Simulation s, EntityId e) => s.Naval.AirState[e.Index];

    /// <summary>A carrier at (10,10) with <paramref name="jets"/> jets docked and an enemy tank at (20,10).</summary>
    private (Simulation S, EntityId Carrier, EntityId[] Jets, EntityId Tank) Fleet(int jets = 2, int tankHp = 5000)
    {
        var s = new Simulation(Setup(tankHp));
        EntityId c = s.SpawnVehicle(0, Carrier, new CellCoord(10, 10));
        var js = new EntityId[jets];
        for (int k = 0; k < jets; k++) js[k] = s.SpawnVehicle(0, Jet, new CellCoord(10, 10));
        EntityId tank = s.SpawnVehicle(1, Tank, new CellCoord(20, 10));
        _third = s.SpawnVehicle(0, Jet, new CellCoord(10, 11)); _hook = s.SpawnVehicle(0, Hook, new CellCoord(10, 12));
        s.Step(js.Select(j => Dock(s, j, c)).ToArray());
        Run(s, 4);
        return (s, c, js, tank);
    }

    [Fact]
    public void Aircraft_DockIntoTheHangar_AndAreHidden()
    {
        var (s, c, jets, _) = Fleet(2);
        foreach (EntityId j in jets)
        {
            Assert.Equal(NavalState.AirDocked, State(s, j));
            Assert.Equal(ActorLayer.Passenger, s.Tunnels.VehicleLayer[j.Index]);
            Assert.Equal(c.Index, s.Naval.HangarOf[j.Index]);
        }
        Assert.True(s.ViewFor(0).OwnCarrierHangar(c, out int docked, out int slots));
        Assert.Equal((2, 2), (docked, slots));
        Assert.False(s.ViewFor(1).OwnCarrierHangar(c, out _, out _));
    }

    [Fact]
    public void Dock_FlyingBackFromAfar_ArrivesAtTheCarrier()
    {
        var s = new Simulation(Setup());
        EntityId c = s.SpawnVehicle(0, Carrier, new CellCoord(10, 10));
        EntityId j = s.SpawnVehicle(0, Jet, new CellCoord(30, 20));
        s.Step(new[] { Dock(s, j, c) });
        Assert.Equal(NavalState.AirReturning, State(s, j));
        for (int t = 0; t < 400 && State(s, j) != NavalState.AirDocked; t++) Run(s, 1);
        Assert.Equal(NavalState.AirDocked, State(s, j));
        Assert.Equal(s.Units.Position[c.Index], s.Units.Position[j.Index]);
    }

    [Fact]
    public void SlotLimit_RefusesTheThirdAircraft_AndTheSkyhook()
    {
        var (s, c, jets, _) = Fleet(2);
        EntityId third = _third, hook = _hook;
        s.Step(new[] { Dock(s, third, c), Dock(s, hook, c) });
        Run(s, 10);
        Assert.Equal(0, State(s, third));
        Assert.Equal(0, s.Naval.HangarOf[third.Index]);
        Assert.Equal(0, State(s, hook));
        Assert.Equal(2, jets.Count(j => State(s, j) == NavalState.AirDocked));
    }

    [Fact]
    public void AttackOrder_LaunchesTheDockedAircraft_WhichHitTheTarget()
    {
        var (s, c, jets, tank) = Fleet(2);
        s.Step(new[] { Strike(s, c, tank) });
        foreach (EntityId j in jets)
        {
            Assert.Equal(NavalState.AirOnSortie, State(s, j));
            Assert.Equal(ActorLayer.Surface, s.Tunnels.VehicleLayer[j.Index]);
        }
        Run(s, 80);
        Assert.True(s.Units.Health[tank.Index] < 5000);
        Assert.True(s.ViewFor(0).OwnCarrierHangar(c, out int docked, out _) && docked == 0);
        Assert.True(s.Units.Position[jets[0].Index].ToCell().X >= 16 - 6);   // they flew toward the target
    }

    [Fact]
    public void Sortie_EndsByTime_ThenTheAircraftReturnAndRepair()
    {
        var (s, c, jets, tank) = Fleet(1);
        s.Step(new[] { Strike(s, c, tank) });
        Run(s, Sortie + 5);
        Assert.Equal(NavalState.AirReturning, State(s, jets[0]));
        s.Units.Health[jets[0].Index] = 40;
        for (int t = 0; t < 200 && State(s, jets[0]) != NavalState.AirDocked; t++) Run(s, 1);
        Assert.Equal(NavalState.AirDocked, State(s, jets[0]));
        Run(s, Sortie + 5);
        Assert.Equal(100, s.Units.Health[jets[0].Index]);
        Assert.True(s.Units.Health[tank.Index] < 5000);
    }

    [Fact]
    public void Sortie_EndsEarlyWhenTheTargetDies()
    {
        var (s, c, jets, tank) = Fleet(2, tankHp: 25);
        s.Step(new[] { Strike(s, c, tank) });
        int back = -1;
        for (int t = 0; t < Sortie - 20; t++)
        {
            Run(s, 1);
            if (back < 0 && State(s, jets[0]) == NavalState.AirReturning) back = t;
        }
        Assert.False(s.Units.Slots.IsAlive(tank));
        Assert.True(back > 0, "the aircraft never turned back");
        for (int t = 0; t < 300; t++) Run(s, 1);
        Assert.All(jets, j => Assert.Equal(NavalState.AirDocked, State(s, j)));
    }

    [Fact]
    public void CarrierDeath_KillsDockedAircraft_AndFreesAirborneOnes()
    {
        var (s, c, jets, tank) = Fleet(2);
        // launch both, let one come home early by hand: re-dock jet 1 right after the launch order
        s.Step(new[] { Strike(s, c, tank) });
        Run(s, 20);
        s.Step(new[] { Dock(s, jets[1], c) });   // back onto the carrier: Returning
        for (int t = 0; t < 200 && State(s, jets[1]) != NavalState.AirDocked; t++) Run(s, 1);
        Assert.Equal(NavalState.AirDocked, State(s, jets[1]));
        Assert.Equal(NavalState.AirOnSortie, State(s, jets[0]));
        s.Units.Health[c.Index] = 0;
        Run(s, 2);
        Assert.False(s.Units.Slots.IsAlive(c));
        Assert.False(s.Units.Slots.IsAlive(jets[1]));   // docked: lost with the ship
        Assert.True(s.Units.Slots.IsAlive(jets[0]));    // airborne: an ordinary aircraft now
        Assert.Equal(0, s.Naval.HangarOf[jets[0].Index]);
        Assert.Equal(0, State(s, jets[0]));
        Run(s, 300);
        Assert.True(s.Units.Slots.IsAlive(jets[0]));
    }

    [Fact]
    public void SaveLoad_MidSortie_ReproducesTheHash()
    {
        var (s, c, _, tank) = Fleet(2);
        s.Step(new[] { Strike(s, c, tank) });
        Run(s, 60);
        ulong before = s.ComputeStateHash();
        var ms = new MemoryStream();
        s.Save(ms);
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), Setup());
        Assert.Equal(before, loaded.ComputeStateHash());
        for (int t = 0; t < 400; t++)
        {
            Run(s, 1); Run(loaded, 1);
            Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.Equal(s.Naval.AirState.ToArray(), loaded.Naval.AirState.ToArray());
    }

    [Fact]
    public void HangarFields_AreHashed_AndEmptyHangarsKeepTheOldLayout()
    {
        var s = new Simulation(Setup());
        EntityId c = s.SpawnVehicle(0, Carrier, new CellCoord(10, 10));
        ulong none = s.ComputeStateHash();
        EntityId j = s.SpawnVehicle(0, Jet, new CellCoord(10, 10));
        s.Step(new[] { Dock(s, j, c) });
        Assert.NotEqual(none, s.ComputeStateHash());
        var plain = new Simulation(Setup());
        var a = new MemoryStream(); var b = new MemoryStream();
        using (var w = new BinaryWriter(a, System.Text.Encoding.UTF8, true)) plain.Naval.Write(w);
        Assert.Equal(1, a.ToArray()[0]);   // version 1 while nobody holds a hangar place
        using (var w = new BinaryWriter(b, System.Text.Encoding.UTF8, true)) s.Naval.Write(w);
        Assert.Equal(2, b.ToArray()[0]);
    }

    [Fact]
    public void Replay_SameCommands_SameHashes()
    {
        ulong[] Play()
        {
            var (s, c, jets, tank) = Fleet(2);
            var hashes = new List<ulong>();
            s.Step(new[] { Strike(s, c, tank) });
            for (int t = 0; t < 500; t++)
            {
                if (t == 120) s.Step(new[] { Cmd(s, CommandKind.Move, c, default, 0, (int)ActorKind.Vehicle) with { Cell = new CellCoord(6, 14) } });
                else Run(s, 1);
                if (t % 20 == 0) hashes.Add(s.ComputeStateHash());
            }
            return hashes.ToArray();
        }
        ulong[] first = Play(), second = Play();
        Assert.Equal(first, second);
        Assert.True(first.Distinct().Count() > 5);
    }

    [Fact]
    public void Production_WithTheRallyOnACarrier_BuildsIntoTheHangar()
    {
        int plane = Veh("test_plane"), tank = Veh("test_tank");
        var vehicles = Base.Vehicles.ToArray();
        vehicles[tank] = vehicles[tank] with { AircraftSlots = 1, SortieTicks = 100 };
        var setup = new SimSetup(7, MapData.CreateFlat(40, 40), 2, Base with { Vehicles = vehicles }, Loaded.RulesHash) { AllowDebug = true };
        var sim = new Simulation(setup);
        Place(sim, 0, "test_hq", 4, 4); Place(sim, 0, "test_power", 12, 4);
        EntityId field = Place(sim, 0, "test_airfield", 16, 4);
        EntityId carrier = sim.SpawnVehicle(0, tank, new CellCoord(25, 25));
        sim.Players.Cash[0] = 20000;
        sim.Step(new[] { new Command(sim.Tick, 0, 900, CommandKind.Rally, field, default, new CellCoord(25, 26), 0),
                         Build(sim, ProductionCategory.Aircraft, plane), Build(sim, ProductionCategory.Aircraft, plane) });
        int docked = 0;
        for (int t = 0; t < 1500 && docked < 1; t++) { sim.Step(None); sim.ViewFor(0).OwnCarrierHangar(carrier, out docked, out _); }
        Assert.Equal(1, docked);
        Run(sim, 600);   // the second plane finds the hangar full and leaves the airfield instead
        sim.ViewFor(0).OwnCarrierHangar(carrier, out docked, out int slots);
        Assert.Equal((1, 1), (docked, slots));
    }
}
