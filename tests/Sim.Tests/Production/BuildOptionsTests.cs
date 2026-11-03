// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Sim.Tests.Production.ProductionFixture;

namespace Rts.Sim.Tests.Production;

/// <summary>M2-11: IsUnlocked / QueueBlockReason (the CanBuild split) and PlayerHud.Options (D-066).</summary>
public class BuildOptionsTests
{
    private const ProductionCategory Infantry = ProductionCategory.Infantry;
    private const ProductionCategory Vehicle = ProductionCategory.Vehicle;
    private const ProductionCategory Structure = ProductionCategory.Structure;
    private const ProductionCategory Defence = ProductionCategory.Defence;

    [Fact]
    public void IsUnlocked_FollowsPrerequisites_NotQueueState()
    {
        var sim = NewSim(new[] { ("test_hq", 4, 4) });
        Assert.True(sim.IsUnlocked(0, Structure, Bld("test_barracks")));
        Assert.False(sim.IsUnlocked(0, Infantry, Inf("test_rifleman")));   // no barracks
        Assert.False(sim.IsUnlocked(0, Structure, Bld("test_refinery")));  // needs a power plant
        Assert.False(sim.IsUnlocked(0, Defence, Bld("test_barracks")));    // wrong category for the type
        Assert.False(sim.IsUnlocked(0, Structure, 99));
        Assert.False(sim.IsUnlocked(0, ProductionCategory.Aircraft, 0));
        Assert.False(sim.IsUnlocked(9, Structure, Bld("test_barracks")));
    }

    [Fact]
    public void IsUnlocked_StaysTrue_WhenTheQueueIsFullOrAStructureWaits()
    {
        var sim = Base();
        for (int i = 0; i < 5; i++) Step(sim, Build(sim, Infantry, Inf("test_rifleman")));
        Assert.True(sim.IsUnlocked(0, Infantry, Inf("test_rifleman")));
        Assert.Equal(QueueBlockReason.QueueFull, sim.QueueBlockReason(0, Infantry, Inf("test_rifleman")));
        Assert.False(sim.CanBuild(0, Infantry, Inf("test_rifleman")));
    }

    [Fact]
    public void QueueBlockReason_NoFactory_ForUnitCategories()
    {
        var sim = NewSim(new[] { ("test_hq", 4, 4), ("test_barracks", 8, 4) });
        Assert.Equal(QueueBlockReason.None, sim.QueueBlockReason(0, Infantry, Inf("test_rifleman")));
        Assert.Equal(QueueBlockReason.NoFactory, sim.QueueBlockReason(0, Vehicle, Veh("test_tank")));
        Assert.Equal(QueueBlockReason.None, sim.QueueBlockReason(0, Structure, Bld("test_power")));
    }

    [Fact]
    public void QueueBlockReason_StructureWaiting_UntilPlaced()
    {
        var sim = Base();
        int power = Bld("test_power");
        Step(sim, Build(sim, Structure, power));
        RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == power);
        Assert.Equal(QueueBlockReason.StructureWaiting, sim.QueueBlockReason(0, Structure, Bld("test_silo")));
        Assert.Equal(QueueBlockReason.None, sim.QueueBlockReason(0, Defence, Bld("test_wall")));   // separate lane
        Assert.False(sim.CanBuild(0, Structure, Bld("test_silo")));
    }

    [Fact]
    public void QueueBlockReason_Defeated_AndBadTypes()
    {
        var sim = Base();
        Assert.Equal(QueueBlockReason.NotBuildable, sim.QueueBlockReason(0, Structure, 99));
        Assert.Equal(QueueBlockReason.NotBuildable, sim.QueueBlockReason(0, ProductionCategory.Aircraft, 0));
        sim.Players.Defeated[0] = true;
        Assert.Equal(QueueBlockReason.Defeated, sim.QueueBlockReason(0, Structure, Bld("test_power")));
        Assert.False(sim.CanBuild(0, Structure, Bld("test_power")));
    }

    [Fact]
    public void HudOptions_ListEveryUnlockedType_WithReasons_AndBuildableIsTheUnblockedSubset()
    {
        var sim = NewSim(new[] { ("test_hq", 4, 4), ("test_barracks", 8, 4) });
        sim.Players.Cash[0] = 5000;
        PlayerHud hud = sim.HudFor(0);
        Assert.All(hud.Options, o => Assert.True(o.Unlocked));
        BuildOption rifle = hud.Options.Single(o => o.Category == Infantry && o.TypeIndex == Inf("test_rifleman"));
        Assert.Equal(QueueBlockReason.None, rifle.Blocked);
        Assert.Equal(Loaded.Sim.Infantry[Inf("test_rifleman")].Cost, rifle.Cost);
        Assert.DoesNotContain(hud.Options, o => o.Category == Vehicle && o.TypeIndex == Veh("test_tank"));   // needs a factory building
        Assert.All(hud.Options.Where(o => o.Category == Vehicle), o => Assert.Equal(QueueBlockReason.NoFactory, o.Blocked));
        var unblocked = hud.Options.Where(o => o.Blocked == QueueBlockReason.None).Select(o => (o.Category, o.TypeIndex)).ToList();
        Assert.Equal(unblocked, hud.Buildable.ToList());

        // A waiting structure greys the whole structure tab but not the others.
        Step(sim, Build(sim, Structure, Bld("test_power")));
        RunUntil(sim, () => sim.Production.ReadyType(0, Structure) == Bld("test_power"));
        hud = sim.HudFor(0);
        Assert.All(hud.Options.Where(o => o.Category == Structure), o => Assert.Equal(QueueBlockReason.StructureWaiting, o.Blocked));
        Assert.Equal(QueueBlockReason.None, hud.Options.Single(o => o.Category == Infantry && o.TypeIndex == Inf("test_rifleman")).Blocked);
    }

    [Fact]
    public void CanPlaceReady_UsesTheSameChecksAsPlace()
    {
        var sim = Base();
        int power = Bld("test_power");
        Assert.False(sim.CanPlaceReady(0, 99, new CellCoord(8, 8)));
        Assert.True(sim.CanPlaceReady(0, power, new CellCoord(4, 12)));     // free ground next to the factory
        Assert.False(sim.CanPlaceReady(0, power, new CellCoord(4, 4)));     // on the HQ
        Assert.False(sim.CanPlaceReady(0, power, new CellCoord(28, 28)));   // outside the build radius
        Assert.False(sim.CanPlaceReady(0, power, new CellCoord(-1, 0)));    // off the map
    }
}
