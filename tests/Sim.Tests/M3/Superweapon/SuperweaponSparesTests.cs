// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.M3.Superweapon.SuperweaponFixture;

namespace Rts.Sim.Tests.M3.Superweapon;

/// <summary>A43 / D-311: superweapon blast and Salvo spare garrisoned soldiers and dived submarines (as splash does), radiation
/// spares garrisons, and a captured superweapon building starts uncharged.</summary>
public sealed class SuperweaponSparesTests
{
    private const int Bunker = 4, Rifle = 0, Engineer = 1, SoldierHp = 5000;

    private static SimRules MakeRules(bool salvo)
    {
        SimRules r = SuperweaponFixture.Rules();
        var none = Array.Empty<string>();
        WarheadType w = new("sw_blast", new[] { 100, 100, 100, 100, 100 }, 1, false);
        if (salvo) w = w with { SalvoMissiles = 4, SalvoTicks = 40, SalvoRadius = 0, SalvoDamage = 100, SalvoWarnTicks = 20, SalvoSplash = 1 };
        var buildings = r.Buildings.Concat(new[]
        {
            new BuildingType("bunker", 2, 2, 50000, ArmorClass.Structure, 5, -1, 0, 0, none, new[] { "bunker" }) { Garrison = 5 },
        }).ToArray();
        var infantry = new[]
        {
            new InfantryType("rifle", 9, SoldierHp, ArmorClass.Unarmored, 10, -1, 0, none),
            new InfantryType("engineer", 9, 50, ArmorClass.Unarmored, 10, -1, 0, none) { Engineer = true },
        };
        return r with { Warheads = new[] { w }, Buildings = buildings, Infantry = infantry };
    }

    // player 0 owns the superweapon; the enemy bunker at (25,14) holds one soldier.
    private sealed record Extras(EntityId Dived, EntityId Surf, EntityId Outside, EntityId Theirs, EntityId Eng);

    private static (Simulation Sim, EntityId Sun, EntityId Soldier, EntityId Bunker, Extras X) Make(bool salvo = false)
    {
        var setup = new SimSetup(1, MapData.CreateFlat(48, 48), 2, MakeRules(salvo), 0x43A1UL) { AllowDebug = true };
        var sim = new Simulation(setup);
        sim.PlaceBuilding(0, Plant, new CellCoord(10, 10));
        EntityId sun = sim.PlaceBuilding(0, Sun, new CellCoord(14, 10));
        sim.PlaceBuilding(0, Hatch, new CellCoord(20, 10));
        sim.PlaceBuilding(1, Hut, new CellCoord(44, 44));
        EntityId bunker = sim.PlaceBuilding(1, Bunker, new CellCoord(25, 14));
        Assert.False(bunker.IsNone);
        EntityId soldier = sim.SpawnInfantry(1, Rifle, new CellCoord(24, 16));
        var x = new Extras(sim.SpawnVehicle(1, Brute, new CellCoord(35, 12)), sim.SpawnVehicle(1, Brute, new CellCoord(35, 14)),
            sim.SpawnInfantry(1, Rifle, new CellCoord(30, 30)), sim.PlaceBuilding(1, Sun, new CellCoord(5, 30)),
            sim.SpawnInfantry(0, Engineer, new CellCoord(5, 33)));
        var enter = new Command(sim.Tick, 1, 900, CommandKind.Enter, soldier, bunker, default, (int)ActorKind.Infantry | ((int)ActorKind.Building << 8));
        sim.Step(new[] { enter });
        for (int n = 0; n < 200 && sim.LayerOf(ActorKind.Infantry, soldier.Index) != ActorLayer.Garrisoned; n++) sim.Step(None);
        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, soldier.Index));
        return (sim, sun, soldier, bunker, x);
    }

    [Fact]
    public void Blast_SparesAGarrisonedSoldier()
    {
        var (sim, sun, soldier, _, _) = Make();
        sim.Effects.BuildingCharge[sun.Index] = Total;
        sim.Step(None);
        Run(sim, 1, Fire(sim, 0, sun, 26, 15));
        Assert.True(sim.Effects.BuildingCharge[sun.Index] <= 1, "the blast went off");
        Assert.Equal(SoldierHp, sim.Infantry.Health[soldier.Index]);
    }

    [Fact]
    public void Salvo_SparesAGarrisonedSoldier()
    {
        var (sim, sun, soldier, _, _) = Make(salvo: true);
        sim.Effects.BuildingCharge[sun.Index] = Total;
        sim.Step(None);
        Run(sim, 1, Fire(sim, 0, sun, 26, 15));
        Run(sim, 100);
        Assert.False(sim.Effects.AnySalvo);
        Assert.Equal(SoldierHp, sim.Infantry.Health[soldier.Index]);
    }

    [Fact]
    public void Blast_SparesADivedSubmarine_ButHurtsASurfaceOne()
    {
        var (sim, sun, _, _, x) = Make();
        EntityId dived = x.Dived, surf = x.Surf;
        sim.Naval.Submerged[dived.Index] = true;
        sim.Effects.BuildingCharge[sun.Index] = Total;
        sim.Step(None);
        Run(sim, 1, Fire(sim, 0, sun, 35, 13));
        Assert.Equal(2000, sim.Units.Health[dived.Index]);
        Assert.True(sim.Units.Health[surf.Index] < 2000);
    }

    [Fact]
    public void Radiation_SparesAGarrisonedSoldier_ButHurtsOneOutside()
    {
        var (sim, _, soldier, _, x) = Make();
        EntityId outside = x.Outside;
        foreach (CellCoord c in new[] { new CellCoord(25, 14), new CellCoord(26, 15), new CellCoord(30, 30) })
            sim.Ground.Radiation[sim.Ground.IndexOf(c)] = 250;
        Run(sim, 60);
        Assert.True(sim.Infantry.Health[outside.Index] < SoldierHp, "control: radiation hurts a soldier in the open");
        Assert.Equal(SoldierHp, sim.Infantry.Health[soldier.Index]);
    }

    [Fact]
    public void CapturedSuperweapon_StartsUncharged()
    {
        var (sim, _, _, _, x) = Make();
        EntityId theirs = x.Theirs;
        Assert.False(theirs.IsNone);
        sim.Effects.BuildingCharge[theirs.Index] = Total;
        EntityId eng = x.Eng;
        sim.Buildings.Health[theirs.Index] = 100;
        Assert.True(sim.EngineerEnter(eng.Index, theirs));
        Assert.Equal(0, (int)sim.Buildings.Player[theirs.Index]);
        Assert.Equal(0, sim.Effects.BuildingCharge[theirs.Index]);
    }
}
