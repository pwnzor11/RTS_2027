// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Naval;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.M7;

/// <summary>M7-16 / D-156: aircraft ammunition and rearm. Map: sea for x &lt; 16, grass beyond. Types: "carrier" (naval, 2 places, sortie 400),
/// "jet" (air, ammo 3, rearm 90), "free" (air, unlimited), "tank" (enemy target); building "pad" (2x2, produces aircraft).</summary>
public sealed class AircraftAmmoTests
{
    private const int Carrier = 0, Jet = 1, Free = 2, Tank = 3, Gun = 0, W = 40, H = 30, Sortie = 400, Rounds = 3, Rearm = 90, ShotDamage = 10;
    private ushort _seq;

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

    private static SimSetup Setup(int ammo = Rounds)
    {
        var cells = new LandType[W * H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) cells[y * W + x] = x < 16 ? LandType.Water : LandType.Clear;
        var map = new MapData("sea", "rust_belt", W, H, new ushort[W * H], cells, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
        VehicleType[] vehicles =
        {
            ScriptedGame.Vehicle("carrier", 12, 500) with { Locomotion = Locomotion.Naval, TurnRate = 16, AircraftSlots = 2, SortieTicks = Sortie },
            ScriptedGame.Vehicle("jet", 60, 100) with { Locomotion = Locomotion.Air, TurnRate = 32, PrimaryWeapon = Gun, Ammo = ammo, RearmTicks = ammo > 0 ? Rearm : 0 },
            ScriptedGame.Vehicle("free", 60, 100) with { Locomotion = Locomotion.Air, TurnRate = 32, PrimaryWeapon = Gun },
            ScriptedGame.Vehicle("tank", 20, 5000),
        };
        var weapons = new[] { new WeaponType("gun", ShotDamage, 6 * 256, 0, 10, 0, 0) { Targets = WeaponTargets.Both } };
        var warheads = new[] { new WarheadType("w", new[] { 100, 100, 100, 100, 100, 100, 100, 100 }, 0, false) };
        BuildingType[] buildings =
        {
            ScriptedGame.Building("barracks", 2, 2, 300), ScriptedGame.Building("hq", 3, 3, 500),
            ScriptedGame.Building("pad", 2, 2, 300) with { Produces = ProductionCategory.Aircraft }, ScriptedGame.Building("turret", 1, 1, 100),
        };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>()) with
        { Weapons = weapons, Warheads = warheads, Terrain = Terrain(), Buildings = buildings };
        return new SimSetup(5, map, 2, rules, 0xABCDUL) { AllowDebug = true };
    }

    private Command Cmd(Simulation s, CommandKind k, EntityId who, EntityId target = default, int arg = (int)ActorKind.Vehicle) =>
        new(s.Tick, 0, ++_seq, k, who, target, default, arg);

    private Command Dock(Simulation s, EntityId jet, EntityId carrier) =>
        Cmd(s, CommandKind.Enter, jet, carrier, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8));

    private Command Strike(Simulation s, EntityId who, EntityId target) =>
        Cmd(s, CommandKind.Attack, who, target, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8));

    private static void Run(Simulation s, int n) { for (int i = 0; i < n; i++) s.Step(Array.Empty<Command>()); }

    private static int Ammo(Simulation s, EntityId e) => s.Naval.AmmoLeft[e.Index];

    /// <summary>Carrier at (10,10) with one docked jet, enemy tank at (20,10).</summary>
    private (Simulation S, EntityId Carrier, EntityId Jet, EntityId Tank) Fleet(int ammo = Rounds, int jetType = Jet)
    {
        var s = new Simulation(Setup(ammo));
        EntityId c = s.SpawnVehicle(0, Carrier, new CellCoord(10, 10));
        EntityId j = s.SpawnVehicle(0, jetType, new CellCoord(10, 10));
        EntityId tank = s.SpawnVehicle(1, Tank, new CellCoord(20, 10));
        s.Step(new[] { Dock(s, j, c) });
        Run(s, 4);
        return (s, c, j, tank);
    }

    [Fact]
    public void NewAircraft_StartFull()
    {
        var (s, _, j, _) = Fleet();
        Assert.Equal(Rounds, Ammo(s, j));
        Assert.Equal(NavalState.AirDocked, s.Naval.AirState[j.Index]);
    }

    [Fact]
    public void Sortie_FiresExactlyTheRounds_ThenReturnsAndDocks()
    {
        var (s, c, j, tank) = Fleet();
        int hp0 = s.Units.Health[tank.Index];
        s.Step(new[] { Strike(s, c, tank) });
        Assert.Equal(NavalState.AirOnSortie, s.Naval.AirState[j.Index]);
        int t = 0;
        while (s.Naval.AirState[j.Index] == NavalState.AirOnSortie && t++ < Sortie) Run(s, 1);
        Assert.True(t < Sortie, "the sortie should end by ammunition long before its timer");
        Assert.Equal(0, Ammo(s, j));
        Run(s, 1);   // let the last shell land
        for (int k = 0; k < 120 && s.Naval.AirState[j.Index] != NavalState.AirDocked; k++) Run(s, 1);
        Assert.Equal(NavalState.AirDocked, s.Naval.AirState[j.Index]);
        Assert.Equal(hp0 - Rounds * ShotDamage, s.Units.Health[tank.Index]);
    }

    [Fact]
    public void Docked_Aircraft_Rearm_AfterRearmTicks()
    {
        var (s, c, j, tank) = Fleet();
        s.Step(new[] { Strike(s, c, tank) });
        while (s.Naval.AirState[j.Index] != NavalState.AirDocked) Run(s, 1);
        int start = Ammo(s, j);
        Assert.True(start < Rounds);
        // Rearming from the current count completes within RearmTicks and not much sooner than the missing share of it.
        Run(s, Rearm / 2);
        Assert.True(Ammo(s, j) > start || start == Rounds - 1 && Ammo(s, j) >= start);
        Run(s, Rearm);
        Assert.Equal(Rounds, Ammo(s, j));
        // Ready again: a new launch order takes it up.
        s.Step(new[] { Strike(s, c, tank) });
        Assert.Equal(NavalState.AirOnSortie, s.Naval.AirState[j.Index]);
    }

    [Fact]
    public void EmptyToFull_TakesRearmTicks()
    {
        var (s, _, j, _) = Fleet();
        s.Naval.AmmoLeft[j.Index] = 0;
        Run(s, Rearm - 2);
        Assert.True(Ammo(s, j) < Rounds);
        Run(s, 3);
        Assert.Equal(Rounds, Ammo(s, j));
    }

    [Fact]
    public void AircraftWithoutAmmo_FireWithoutLimit()
    {
        var (s, c, j, tank) = Fleet(ammo: 0, jetType: Free);
        Assert.Equal(0, Ammo(s, j));
        int hp0 = s.Units.Health[tank.Index];
        s.Step(new[] { Strike(s, c, tank) });
        Run(s, 150);
        Assert.Equal(0, Ammo(s, j));
        Assert.True(hp0 - s.Units.Health[tank.Index] > (Rounds + 2) * ShotDamage);
    }

    [Fact]
    public void EmptyAircraft_CannotBeOrderedToAttack()
    {
        var s = new Simulation(Setup());
        EntityId j = s.SpawnVehicle(0, Jet, new CellCoord(25, 10));
        EntityId tank = s.SpawnVehicle(1, Tank, new CellCoord(28, 10));
        s.Naval.AmmoLeft[j.Index] = 0;
        s.Step(new[] { Strike(s, j, tank) });
        Run(s, 60);
        Assert.Equal(5000, s.Units.Health[tank.Index]);
        Assert.NotEqual(MissionKind.Attack, s.Units.Mission[j.Index]);
    }

    [Fact]
    public void FreeAircraft_GoesBackToACarrierWithAFreeSlot_WhenDry()
    {
        var s = new Simulation(Setup());
        EntityId c = s.SpawnVehicle(0, Carrier, new CellCoord(10, 10));
        EntityId j = s.SpawnVehicle(0, Jet, new CellCoord(24, 10));
        EntityId tank = s.SpawnVehicle(1, Tank, new CellCoord(28, 10));
        s.Step(new[] { Strike(s, j, tank) });
        for (int k = 0; k < 300 && s.Naval.AirState[j.Index] != NavalState.AirDocked; k++) Run(s, 1);
        Assert.Equal(c.Index, s.Naval.HangarOf[j.Index]);
        Assert.Equal(NavalState.AirDocked, s.Naval.AirState[j.Index]);
        Run(s, Rearm + 5);
        Assert.Equal(Rounds, Ammo(s, j));
        Assert.Equal(5000 - Rounds * ShotDamage, s.Units.Health[tank.Index]);
    }

    [Fact]
    public void FreeAircraft_WithoutCarrier_HoversOverAircraftBuilding_AndRearms()
    {
        var s = new Simulation(Setup());
        EntityId pad = s.PlaceBuilding(0, 2, new CellCoord(30, 20));
        Assert.False(pad.IsNone);
        EntityId j = s.SpawnVehicle(0, Jet, new CellCoord(24, 10));
        EntityId tank = s.SpawnVehicle(1, Tank, new CellCoord(28, 10));
        s.Step(new[] { Strike(s, j, tank) });
        for (int k = 0; k < 300 && Ammo(s, j) > 0; k++) Run(s, 1);
        Assert.Equal(0, Ammo(s, j));
        for (int k = 0; k < 300; k++) Run(s, 1);
        CellCoord cell = s.Units.Position[j.Index].ToCell();
        Assert.InRange(cell.X, 30, 31); Assert.InRange(cell.Y, 20, 21);
        Assert.Equal(Rounds, Ammo(s, j));
        Assert.Equal(0, s.Naval.HangarOf[j.Index]);
    }

    [Fact]
    public void DryAircraft_WithNoHome_StaysOutOfAmmo()
    {
        var s = new Simulation(Setup());
        EntityId j = s.SpawnVehicle(0, Jet, new CellCoord(24, 10));
        EntityId tank = s.SpawnVehicle(1, Tank, new CellCoord(28, 10));
        s.Step(new[] { Strike(s, j, tank) });
        Run(s, 600);
        Assert.Equal(0, Ammo(s, j));
        Assert.Equal(5000 - Rounds * ShotDamage, s.Units.Health[tank.Index]);
        Assert.True(s.Units.Health[j.Index] > 0);
    }

    [Fact]
    public void SaveLoad_MidSortie_KeepsAmmoAndTheHash()
    {
        var (s, c, j, tank) = Fleet();
        s.Step(new[] { Strike(s, c, tank) });
        while (Ammo(s, j) == Rounds) Run(s, 1);
        Run(s, 2);
        int mid = Ammo(s, j);
        Assert.InRange(mid, 0, Rounds - 1);
        ulong before = s.ComputeStateHash();
        var ms = new MemoryStream();
        s.Save(ms);
        Simulation loaded = Simulation.Load(new MemoryStream(ms.ToArray()), Setup());
        Assert.Equal(mid, Ammo(loaded, j));
        Assert.Equal(before, loaded.ComputeStateHash());
        for (int t = 0; t < 400; t++)
        {
            Run(s, 1); Run(loaded, 1);
            Assert.Equal(s.ComputeStateHash(), loaded.ComputeStateHash());
        }
        Assert.Equal(s.Naval.AmmoLeft.ToArray(), loaded.Naval.AmmoLeft.ToArray());
        Assert.Equal(s.Naval.RearmProgress.ToArray(), loaded.Naval.RearmProgress.ToArray());
    }

    [Fact]
    public void AmmoArrays_AreWrittenOnlyWhenSomeUnitHasAmmo()
    {
        var plain = new Simulation(Setup(ammo: 0));
        plain.SpawnVehicle(0, Free, new CellCoord(24, 10));
        var withAmmo = new Simulation(Setup());
        withAmmo.SpawnVehicle(0, Jet, new CellCoord(24, 10));
        Assert.Equal(1, FirstByte(plain.Naval));
        Assert.Equal(3, FirstByte(withAmmo.Naval));
        Assert.NotEqual(plain.ComputeStateHash(), withAmmo.ComputeStateHash());
    }

    private static byte FirstByte(NavalState n)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) n.Write(w);
        return ms.ToArray()[0];
    }

    [Fact]
    public void Replay_SameCommands_SameHashes()
    {
        ulong Play()
        {
            var (s, c, _, tank) = Fleet();
            s.Step(new[] { Strike(s, c, tank) });
            Run(s, 500);
            return s.ComputeStateHash();
        }
        _seq = 0; ulong a = Play(); _seq = 0; ulong b = Play();
        Assert.Equal(a, b);
    }
}
