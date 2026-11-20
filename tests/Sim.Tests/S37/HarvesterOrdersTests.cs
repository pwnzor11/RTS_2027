// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Economy;
using static Rts.Sim.Tests.Combat.CombatFixture;

namespace Rts.Sim.Tests.S37;

/// <summary>M13-32: harvester orders the owner missed in the campaign (specific refinery, empty trip, auto-resume, clicked cell first).</summary>
public sealed class HarvesterOrdersTests
{
    private static readonly CellCoord FarRefinery = new(22, 20);   // 3x3, dock cell (23,23)

    private static ushort seq = 100;

    private static void Send(Simulation sim, CommandKind kind, EntityId subject, CellCoord cell = default, EntityId target = default, int arg = 0) =>
        sim.Step(new[] { new Command(sim.Tick, 0, seq++, kind, subject, target, cell, arg) });

    private static EntityId TwoRefineries(out Simulation sim, out EntityId nearRef, out EntityId farRef)
    {
        var (s, h) = EconomyFixture.Basic();
        sim = s;
        sim.PlaceBuilding(0, Bld("test_refinery"), FarRefinery);
        nearRef = sim.Map.BuildingAt(EconomyFixture.Refinery);
        farRef = sim.Map.BuildingAt(FarRefinery);
        Assert.False(farRef.IsNone);
        return h;
    }

    [Fact]
    public void MovedHarvester_ResumesAfterThreeSeconds()
    {
        var (sim, h) = EconomyFixture.Basic();
        EconomyFixture.Run(sim, 5);
        Send(sim, CommandKind.Move, h, new CellCoord(10, 20), arg: (int)ActorKind.Vehicle);
        EconomyFixture.RunUntil(sim, () => EconomyFixture.MissionOf(sim, h) == MissionKind.Guard && sim.Units.NextMission[h.Index] == MissionKind.None);
        EconomyFixture.Run(sim, 40);   // 2 s: still parked
        Assert.Equal(MissionKind.Guard, EconomyFixture.MissionOf(sim, h));
        EconomyFixture.Run(sim, 30);   // past 3 s
        Assert.Equal(MissionKind.Harvest, EconomyFixture.MissionOf(sim, h));
        EconomyFixture.RunUntil(sim, () => sim.Economy.Cargo[h.Index] > 0 || sim.Players.Stored[0] > 0);
    }

    [Fact]
    public void StoppedHarvester_ResumesAfterThreeSeconds()
    {
        var (sim, h) = EconomyFixture.Basic();
        EconomyFixture.Run(sim, 5);
        Send(sim, CommandKind.Stop, h, arg: (int)ActorKind.Vehicle);
        EconomyFixture.Run(sim, 20);
        Assert.Equal(MissionKind.Guard, EconomyFixture.MissionOf(sim, h));
        EconomyFixture.Run(sim, 60);
        Assert.Equal(MissionKind.Harvest, EconomyFixture.MissionOf(sim, h));
    }

    [Fact]
    public void EnemyHarvester_AlsoResumes()
    {
        var sim = EconomyFixture.NewSim(EconomyFixture.PaintMap(OverlayKind.Ashfall, 12));
        sim.PlaceBuilding(1, Bld("test_refinery"), EconomyFixture.Refinery);
        EntityId h = sim.SpawnVehicle(1, EconomyFixture.Harvester, new CellCoord(8, 8));
        EconomyFixture.Run(sim, 5);
        sim.Step(new[] { new Command(sim.Tick, 1, 1, CommandKind.Stop, h, default, default, (int)ActorKind.Vehicle) });
        EconomyFixture.Run(sim, 80);
        Assert.Equal(MissionKind.Harvest, EconomyFixture.MissionOf(sim, h));
    }

    [Fact]
    public void FreshHarvester_StartsHarvestingAtOnce()
    {
        var (sim, h) = EconomyFixture.Basic();
        EconomyFixture.Run(sim, 3);
        Assert.Equal(MissionKind.Harvest, EconomyFixture.MissionOf(sim, h));
    }

    [Fact]
    public void LoadedHarvester_ClickedRefinery_DocksAtThatOneNotTheNearest()
    {
        EntityId h = TwoRefineries(out Simulation sim, out EntityId nearRef, out EntityId farRef);
        EconomyFixture.Run(sim, 3);
        sim.Economy.Cargo[h.Index] = 10; sim.Economy.CargoValue[h.Index] = 10 * 25;
        Send(sim, CommandKind.Return, h, target: farRef, arg: (int)ActorKind.Vehicle);
        EconomyFixture.Run(sim, 5);
        Assert.Equal(MissionKind.Return, EconomyFixture.MissionOf(sim, h));
        Assert.Equal(farRef, sim.Economy.DockTarget[h.Index]);
        Assert.NotEqual(nearRef, sim.Economy.DockTarget[h.Index]);
        EconomyFixture.RunUntil(sim, () => sim.Players.Stored[0] > 0, 4000);
        Assert.Equal(farRef, sim.Economy.HomeRefinery[h.Index]);
    }

    [Fact]
    public void EmptyHarvester_SentToRefinery_StartsHarvestingWithoutDocking()
    {
        EntityId h = TwoRefineries(out Simulation sim, out _, out EntityId farRef);
        EconomyFixture.Run(sim, 3);
        // park it empty, then send it to the far refinery
        Send(sim, CommandKind.Stop, h, arg: (int)ActorKind.Vehicle);
        EconomyFixture.Run(sim, 5);
        Assert.Equal(0, sim.Economy.Cargo[h.Index]);
        Send(sim, CommandKind.Return, h, target: farRef, arg: (int)ActorKind.Vehicle);
        EconomyFixture.Run(sim, 3);
        Assert.Equal(MissionKind.Harvest, EconomyFixture.MissionOf(sim, h));
        Assert.Equal(farRef, sim.Economy.HomeRefinery[h.Index]);
        // it never makes a Return trip until it carries something
        for (int t = 0; t < 400 && sim.Economy.Cargo[h.Index] == 0; t++)
        {
            sim.Step(None);
            Assert.NotEqual(MissionKind.Return, EconomyFixture.MissionOf(sim, h));
        }
        // when full it delivers to its home (the far refinery), not the nearer one
        EconomyFixture.RunUntil(sim, () => EconomyFixture.MissionOf(sim, h) == MissionKind.Return, 4000);
        EconomyFixture.Run(sim, 3);
        Assert.Equal(farRef, sim.Economy.DockTarget[h.Index]);
    }

    [Fact]
    public void HomeRefinery_Destroyed_FallsBackToNearest()
    {
        EntityId h = TwoRefineries(out Simulation sim, out EntityId nearRef, out EntityId farRef);
        EconomyFixture.Run(sim, 3);
        sim.Economy.Cargo[h.Index] = 10; sim.Economy.CargoValue[h.Index] = 250;
        Send(sim, CommandKind.Return, h, target: farRef, arg: (int)ActorKind.Vehicle);
        EconomyFixture.Run(sim, 5);
        sim.Step(new[] { new Command(sim.Tick, 0, 900, CommandKind.Debug, farRef, default, default, (int)DebugOp.Kill, (int)ActorKind.Building) });
        EconomyFixture.RunUntil(sim, () => sim.Players.Stored[0] > 0, 4000);
        Assert.Equal(nearRef, sim.Economy.DockTarget[h.Index].IsNone ? nearRef : sim.Economy.DockTarget[h.Index]);
        Assert.NotEqual(farRef, sim.Economy.HomeRefinery[h.Index]);
    }

    [Fact]
    public void ClickedCell_IsHarvestedFirst_EvenWhenARicherCellIsNearby()
    {
        // a 3x3 field of density 12 plus one poor cell (density 2) a few cells away from it, clicked on purpose
        var click = new CellCoord(14, 14);
        var map = EconomyFixture.PaintMap(OverlayKind.Ashfall, 12, (overlay, dens, w) => { overlay[click.Y * w + click.X] = OverlayKind.Ashfall; dens[click.Y * w + click.X] = 2; });
        var sim = EconomyFixture.NewSim(map);
        sim.PlaceBuilding(0, Bld("test_refinery"), EconomyFixture.Refinery);
        EntityId h = sim.SpawnVehicle(0, EconomyFixture.Harvester, new CellCoord(8, 8));
        EconomyFixture.Run(sim, 2);
        Send(sim, CommandKind.Harvest, h, click, arg: (int)ActorKind.Vehicle);
        EconomyFixture.RunUntil(sim, () => sim.Economy.HarvestState[h.Index] == (byte)HarvestStep.Load);
        Assert.Equal(click, sim.Economy.Goal[h.Index]);
    }

    [Fact]
    public void ClickedCell_ForgetsOldUnreachableMarks()
    {
        var (sim, h) = EconomyFixture.Basic();
        EconomyFixture.Run(sim, 3);
        sim.Economy.SkipCell[h.Index] = new CellCoord(15, 9); sim.Economy.SkipUntil[h.Index] = sim.Tick + 1200;
        Send(sim, CommandKind.Harvest, h, new CellCoord(15, 9), arg: (int)ActorKind.Vehicle);
        Assert.Equal(0, sim.Economy.SkipUntil[h.Index]);
        Assert.Equal(default, sim.Economy.SkipCell[h.Index]);
        EconomyFixture.RunUntil(sim, () => sim.Economy.HarvestState[h.Index] == (byte)HarvestStep.Load);
        Assert.Equal(new CellCoord(15, 9), sim.Economy.Goal[h.Index]);
    }

    [Fact]
    public void DirectorateScraper_BurrowLocomotion_HarvestsAndDelivers()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        SimRules real = RulesLoader.Load(dir!).Sim;
        var sim = EconomyFixture.NewSim(EconomyFixture.PaintMap(OverlayKind.Ashfall, 12), real);
        sim.PlaceBuilding(0, SimRules.IndexOf(real.Buildings, "dir_smelter_vent", b => b.Id), EconomyFixture.Refinery);
        EntityId h = sim.SpawnVehicle(0, SimRules.IndexOf(real.Vehicles, "dir_scraper", v => v.Id), new CellCoord(8, 8));
        EconomyFixture.RunUntil(sim, () => sim.Players.Stored[0] > 0, 6000);
        // after a Move (a burrow trip) it surfaces, idles, and goes back to work by itself
        Send(sim, CommandKind.Move, h, new CellCoord(10, 24), arg: (int)ActorKind.Vehicle);
        EconomyFixture.RunUntil(sim, () => EconomyFixture.MissionOf(sim, h) is MissionKind.Burrow, 200);
        EconomyFixture.RunUntil(sim, () => EconomyFixture.MissionOf(sim, h) == MissionKind.Guard && sim.Units.NextMission[h.Index] == MissionKind.None, 6000);
        EconomyFixture.Run(sim, 70);
        Assert.Equal(MissionKind.Harvest, EconomyFixture.MissionOf(sim, h));
    }
}
