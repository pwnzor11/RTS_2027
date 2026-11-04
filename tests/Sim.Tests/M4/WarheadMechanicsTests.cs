// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.M3.Burrow.BurrowWorld;

namespace Rts.Sim.Tests.M4;

/// <summary>M4-05: hits_burrowed, bonus_vs_entrance_percent, garrison_damage_percent, Quick-Set Foam (design 08 §4, §8).
/// Rules are the M3 burrow fixture plus extra warheads and a garrisonable "zfort", built in code.</summary>
public sealed class WarheadMechanicsTests
{
    private static readonly int[] Versus = { 100, 100, 100, 100, 100 };
    private const int Plain = 1, HitsBurrowed = 2, Entrance = 3, GarrisonHalf = 4, FoamWh = 5, FoamBig = 6;
    private const int Fort = 5;   // appended after "shaft" (sorted by id)

    private static SimRules Rules()
    {
        SimRules b = M3.Burrow.BurrowWorld.Rules();
        var wh = new List<WarheadType>(b.Warheads)
        {
            new("plain", Versus, 1, false),
            new("hits_burrowed", Versus, 1, false) { HitsBurrowed = true },
            new("entrance", Versus, 1, false) { BonusVsEntrancePercent = 200 },
            new("garrison_half", Versus, 1, false) { GarrisonDamagePercent = 50 },
            new("foam", Versus, 2, false) { FoamTicks = 150 },
            new("foam_big", Versus, 40, false) { FoamTicks = 150 },
        };
        var bld = new List<BuildingType>(b.Buildings)
        {
            new("zfort", 1, 1, 500, ArmorClass.Structure, 5, -1, 0, 0, Array.Empty<string>(), Array.Empty<string>()) { Garrison = 3 },
        };
        return b with { Warheads = wh, Buildings = bld };
    }

    private static Simulation NewSim() =>
        new(new SimSetup(1, Rts.Sim.Tests.Movement.MoveWorld.Grid(48, 32), 2, Rules(), 1) { AllowDebug = true });

    private static LeptonCoord Centre(int x, int y) => LeptonCoord.CenterOf(new CellCoord(x, y));

    private static void Blast(Simulation sim, int warhead, int x, int y, int damage = 10) =>
        sim.Detonate(1, damage, warhead, Centre(x, y), default, false);

    // ---- hits_burrowed ----------------------------------------------------------------------------

    [Fact]
    public void HitsBurrowed_SplashHurtsBurrowedUnits_PlainSplashDoesNot()
    {
        var sim = NewSim();
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        EntityId s = sim.SpawnVehicle(0, Enemy, new CellCoord(12, 10));
        sim.Tunnels.VehicleLayer[u.Index] = ActorLayer.Burrowed;
        int hp = sim.Units.Health[u.Index];
        Blast(sim, Plain, 10, 10); Step(sim);
        Assert.Equal(hp, sim.Units.Health[u.Index]);
        Blast(sim, HitsBurrowed, 10, 10); Step(sim);
        Assert.Equal(hp - 10, sim.Units.Health[u.Index]);
        Blast(sim, Plain, 12, 10); Step(sim);
        Assert.Equal(500 - 10, sim.Units.Health[s.Index]);   // surface units are hit by both
    }

    [Fact]
    public void HitsBurrowed_AlsoHurtsSurfacingUnits()
    {
        var sim = NewSim();
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        sim.Tunnels.VehicleLayer[u.Index] = ActorLayer.Surfacing;
        int hp = sim.Units.Health[u.Index];
        Blast(sim, Plain, 10, 10); Step(sim);
        Assert.Equal(hp, sim.Units.Health[u.Index]);
        Blast(sim, HitsBurrowed, 10, 10); Step(sim);
        Assert.Equal(hp - 10, sim.Units.Health[u.Index]);
    }

    // ---- bonus_vs_entrance_percent -----------------------------------------------------------------

    [Fact]
    public void BonusVsEntrance_DoublesDamageOnEntrances_NotOnOtherBuildings()
    {
        var sim = NewSim();
        EntityId hatch = sim.PlaceBuilding(0, Hatch, new CellCoord(10, 10));
        EntityId block = sim.PlaceBuilding(0, Block, new CellCoord(20, 10));
        EntityId hatch2 = sim.PlaceBuilding(0, Hatch, new CellCoord(30, 20));
        Blast(sim, Entrance, 10, 10); Blast(sim, Entrance, 20, 10); Blast(sim, Plain, 30, 20); Step(sim);
        Assert.Equal(200 - 20, sim.Buildings.Health[hatch.Index]);
        Assert.Equal(100 - 10, sim.Buildings.Health[block.Index]);
        Assert.Equal(200 - 10, sim.Buildings.Health[hatch2.Index]);   // 100 % default
    }

    // ---- garrison_damage_percent -------------------------------------------------------------------

    private static EntityId Garrison(Simulation sim, EntityId building, int x, int y)
    {
        EntityId s = sim.SpawnInfantry(0, 0, new CellCoord(x, y), 0);
        sim.Tunnels.InfantryLayer[s.Index] = ActorLayer.Garrisoned;
        sim.Tunnels.InfantryCarrier[s.Index] = building;
        return s;
    }

    [Fact]
    public void GarrisonDamage_PassesAShareOfEachHitToEveryOccupant()
    {
        var sim = NewSim();
        EntityId fort = sim.PlaceBuilding(0, Fort, new CellCoord(10, 10));
        EntityId a = Garrison(sim, fort, 10, 10), c = Garrison(sim, fort, 10, 10);
        EntityId outside = sim.SpawnInfantry(0, 0, new CellCoord(30, 30), 0);
        EntityId other = sim.PlaceBuilding(0, Fort, new CellCoord(20, 20));
        EntityId bystander = Garrison(sim, other, 20, 20);
        int hpA = sim.Infantry.Health[a.Index];
        Blast(sim, GarrisonHalf, 10, 10, 20); Step(sim);
        Assert.Equal(500 - 20, sim.Buildings.Health[fort.Index]);
        Assert.Equal(hpA - 10, sim.Infantry.Health[a.Index]);
        Assert.Equal(hpA - 10, sim.Infantry.Health[c.Index]);
        Assert.Equal(hpA, sim.Infantry.Health[outside.Index]);
        Assert.Equal(hpA, sim.Infantry.Health[bystander.Index]);
    }

    [Fact]
    public void GarrisonDamage_ZeroPercentOrNoOccupantsOrNonGarrisonBuildings_HurtOnlyTheBuilding()
    {
        var sim = NewSim();
        EntityId fort = sim.PlaceBuilding(0, Fort, new CellCoord(10, 10));
        EntityId empty = sim.PlaceBuilding(0, Fort, new CellCoord(20, 20));
        EntityId block = sim.PlaceBuilding(0, Block, new CellCoord(30, 10));
        EntityId a = Garrison(sim, fort, 10, 10);
        int hpA = sim.Infantry.Health[a.Index];
        Blast(sim, Plain, 10, 10, 20);                  // 0 %: occupants are shielded (and skipped by splash)
        Blast(sim, GarrisonHalf, 20, 20, 20);           // nobody inside
        Blast(sim, GarrisonHalf, 30, 10, 20);           // building with garrison = 0
        Step(sim);
        Assert.Equal(hpA, sim.Infantry.Health[a.Index]);
        Assert.Equal(500 - 20, sim.Buildings.Health[fort.Index]);
        Assert.Equal(500 - 20, sim.Buildings.Health[empty.Index]);
        Assert.Equal(100 - 20, sim.Buildings.Health[block.Index]);
    }

    [Fact]
    public void GarrisonDamage_CanKillOccupants()
    {
        var sim = NewSim();
        EntityId fort = sim.PlaceBuilding(0, Fort, new CellCoord(10, 10));
        EntityId a = Garrison(sim, fort, 10, 10);
        Blast(sim, GarrisonHalf, 10, 10, 200); Step(sim);
        Assert.True(sim.Infantry.Health[a.Index] <= 0 || !sim.Infantry.Slots.IsAlive(a));
    }

    // ---- Quick-Set Foam -----------------------------------------------------------------------------

    [Fact]
    public void Foam_IsLaidInRadius_AndDecaysToZeroAfterFoamTicks()
    {
        var sim = NewSim();
        Blast(sim, FoamWh, 10, 10);
        Assert.Equal(150, sim.Ground.FoamAt(new CellCoord(10, 10)));
        Assert.Equal(150, sim.Ground.FoamAt(new CellCoord(12, 8)));
        Assert.Equal(0, sim.Ground.FoamAt(new CellCoord(13, 10)));
        Run(sim, 149);
        Assert.Equal(1, sim.Ground.FoamAt(new CellCoord(10, 10)));
        Run(sim, 1);
        Assert.Equal(0, sim.Ground.FoamAt(new CellCoord(10, 10)));
        Assert.All(sim.Ground.Foam, v => Assert.Equal(0, v));
    }

    [Fact]
    public void Foam_RefreshesButNeverShortens_AndClipsAtTheMapEdge()
    {
        var sim = NewSim();
        sim.Ground.Foam[sim.Ground.IndexOf(new CellCoord(1, 1))] = 400;
        Blast(sim, FoamWh, 0, 0);   // partly off the map
        Assert.Equal(400, sim.Ground.FoamAt(new CellCoord(1, 1)));
        Assert.Equal(150, sim.Ground.FoamAt(new CellCoord(0, 0)));
        Run(sim, 10);
        Blast(sim, FoamWh, 0, 0);
        Assert.Equal(150, sim.Ground.FoamAt(new CellCoord(0, 0)));
    }

    [Fact]
    public void Foam_BlocksSurfacing_UntilItDecays()
    {
        var sim = NewSim();
        EntityId u = sim.SpawnVehicle(0, Burrower, new CellCoord(10, 10));
        Step(sim, MoveTo(sim, u, 40, 10));
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Burrowed);
        Run(sim, 20);
        Blast(sim, FoamBig, 20, 16);   // covers the whole map
        int foamLeft = sim.Ground.FoamAt(new CellCoord(5, 5));
        Assert.Equal(150, foamLeft);
        Step(sim, Cmd(sim, CommandKind.Stop, u, default, (int)ActorKind.Vehicle));
        Run(sim, foamLeft - 30);
        Assert.True(Layer(sim, u) is ActorLayer.Burrowed or ActorLayer.Surfacing, "still underground while foamed");
        Assert.True(sim.Ground.FoamAt(new CellCoord(5, 5)) > 0);
        RunUntil(sim, () => Layer(sim, u) == ActorLayer.Surface);
        Assert.Equal(0, sim.Ground.FoamAt(CellOf(sim, u)));
        Assert.True(sim.Tick > 150, "surfaced only after the foam ran out");
    }

    [Fact]
    public void Foam_BlocksBoreBreakThrough_UntilItDecays()
    {
        var sim = NewSim();
        EntityId bore = sim.SpawnVehicle(0, Bore, new CellCoord(10, 10));
        Step(sim, DeployAt(sim, bore, 30, 12));
        RunUntil(sim, () => Layer(sim, bore) == ActorLayer.Burrowed);
        Blast(sim, FoamBig, 20, 16);
        int laid = sim.Tick;
        Run(sim, 100);
        Assert.Null(EntranceTelegraph(sim));
        Assert.True(sim.Units.Slots.IsAlive(bore), "no Hatch while the ground is foamed");
        RunUntil(sim, () => EntranceTelegraph(sim) != null);
        Assert.True(sim.Tick >= laid + 150, "the hatch came after the foam ran out");
    }

    [Fact]
    public void Foam_SlowsGroundUnitsToTheConfiguredSpeed()
    {
        Simulation Drive(bool foam)
        {
            var sim = NewSim();
            EntityId t = sim.SpawnVehicle(0, Enemy, new CellCoord(5, 5));
            if (foam) Blast(sim, FoamBig, 20, 16);
            Step(sim, MoveTo(sim, t, 40, 5));
            Run(sim, 30);
            return sim;
        }
        // The only vehicle in slot 1 is the tank.
        int free = Drive(false).Units.Position[1].X - Centre(5, 5).X;
        int slow = Drive(true).Units.Position[1].X - Centre(5, 5).X;
        Assert.True(free > 0 && slow > 0);
        Assert.True(slow * 100 >= free * 40 && slow * 100 <= free * 60, $"free {free}, foamed {slow}");   // half speed (turn-in-place ticks are identical in both runs)
    }

    [Fact]
    public void Foam_SurvivesSaveLoad_AndChangesTheHash()
    {
        var setup = new SimSetup(1, Rts.Sim.Tests.Movement.MoveWorld.Grid(48, 32), 2, Rules(), 1) { AllowDebug = true };
        var sim = new Simulation(setup);
        var plain = new Simulation(setup);
        Blast(sim, FoamWh, 10, 10);
        Run(sim, 5); Run(plain, 5);
        Assert.NotEqual(plain.ComputeSubHashes().Single(h => h.Part == "ground"), sim.ComputeSubHashes().Single(h => h.Part == "ground"));
        var ms = new MemoryStream(); sim.Save(ms); ms.Position = 0;
        var loaded = Simulation.Load(ms, setup);
        Assert.Equal(145, loaded.Ground.FoamAt(new CellCoord(10, 10)));
        Assert.Equal(sim.ComputeSubHashes(), loaded.ComputeSubHashes());
        Run(sim, 200); Run(loaded, 200);
        Assert.Equal(sim.ComputeSubHashes(), loaded.ComputeSubHashes());
        Assert.All(loaded.Ground.Foam, v => Assert.Equal(0, v));
    }
}
