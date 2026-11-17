// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.M3.Superweapon;
using static Rts.Sim.Tests.M3.Superweapon.SuperweaponFixture;

namespace Rts.Sim.Tests.M12_16;

/// <summary>M12-16 step 2 (D-264 (3)): splash damage on Tree, Snag and Wreck, Block and Pylon that never break, the Rough ground left behind
/// and the PropDestroyed event.</summary>
public sealed class PropDamageTests
{
    private static readonly CellCoord C = new(10, 10);

    private static LeptonCoord At(CellCoord c) => LeptonCoord.CenterOf(c);

    private static Simulation One(OverlayKind kind, LandType land = LandType.Clear, PropRules? rules = null)
    {
        MapData flat = PropFixture.Map(24, (C, kind));
        var lands = flat.Land.ToArray();
        lands[C.Y * 24 + C.X] = land;
        var map = new MapData("props", "rust_belt", 24, 24, new ushort[24 * 24], lands, new byte[24 * 24], flat.Overlay.ToArray(), new byte[24 * 24], flat.StartPositions);
        return PropFixture.Sim(map, rules);
    }

    private static void Splash(Simulation sim, CellCoord at, int damage, int warhead = PropFixture.SplashWarhead) =>
        sim.Detonate(0, damage, warhead, At(at), default, false);

    [Fact]
    public void Splash_DestroysATree_ClearsTheOverlay_AndLeavesRoughGround()
    {
        Simulation sim = One(OverlayKind.Tree);
        int version = sim.Map.Version;
        ulong hash = sim.ComputeStateHash();
        Splash(sim, C, PropRules.Default.TreeHealth);
        Assert.Equal(OverlayKind.None, sim.Map.OverlayAt(C));
        Assert.Equal(LandType.Rough, sim.Map.LandAt(C));
        Assert.Equal(0, sim.Props.HealthAt(C));
        Assert.True(sim.Map.Version > version);                 // every cached path and region label refreshes
        Assert.NotEqual(hash, sim.ComputeStateHash());
        Assert.Equal(75, sim.Map.SpeedPercent(C, Locomotion.Foot, PropFixture.Terrain()));   // the existing Rough rules
    }

    [Fact]
    public void Splash_EmitsOnePropDestroyedEvent_AtTheCellCentre()
    {
        Simulation sim = One(OverlayKind.Snag);
        Splash(sim, C, 1000);
        FxEvent e = Assert.Single(sim.FxEvents, f => f.Kind == FxKind.PropDestroyed);
        Assert.Equal(At(C), e.Pos);
        Assert.Equal(6, (int)FxKind.PropDestroyed);
    }

    [Fact]
    public void Splash_FallsOffWithDistance_HalfBesideTheImpact()
    {
        Simulation sim = One(OverlayKind.Tree);
        Splash(sim, new CellCoord(C.X + 1, C.Y), 30);    // splash 1: the tree is one cell off the impact: half damage, 15
        Assert.Equal(PropRules.Default.TreeHealth - 15, sim.Props.HealthAt(C));
        Assert.Equal(OverlayKind.Tree, sim.Map.OverlayAt(C));
        Splash(sim, new CellCoord(C.X + 2, C.Y), 1000);  // two cells off: out of a splash-1 blast
        Assert.Equal(PropRules.Default.TreeHealth - 15, sim.Props.HealthAt(C));
    }

    [Fact]
    public void Wreck_NeedsSeveralHits_ATreeOne()
    {
        Simulation wreck = One(OverlayKind.Wreck);
        int hits = 0;
        while (wreck.Map.OverlayAt(C) == OverlayKind.Wreck && hits < 100) { Splash(wreck, C, 40); hits++; }
        Assert.Equal(OverlayKind.None, wreck.Map.OverlayAt(C));
        Assert.Equal(4, hits);                                         // 160 health, 40 a hit
        Simulation tree = One(OverlayKind.Tree);
        Splash(tree, C, 40);
        Assert.Equal(OverlayKind.None, tree.Map.OverlayAt(C));
    }

    [Theory]
    [InlineData(OverlayKind.Block)]
    [InlineData(OverlayKind.Pylon)]
    [InlineData(OverlayKind.Wall)]
    public void BlockPylonAndWalls_NeverBreak(OverlayKind kind)
    {
        Simulation sim = One(kind);
        ulong hash = sim.ComputeStateHash();
        for (int i = 0; i < 20; i++) Splash(sim, C, 10_000);
        Assert.Equal(kind, sim.Map.OverlayAt(C));
        Assert.Equal(LandType.Clear, sim.Map.LandAt(C));
        Assert.DoesNotContain(sim.FxEvents, f => f.Kind == FxKind.PropDestroyed);
        Assert.Equal(0, sim.Props.HealthAt(C));
        Assert.Equal(0, sim.Props.Count);
        Assert.Equal(hash, sim.ComputeStateHash());
    }

    [Fact]
    public void SmallArmsAndSplashZeroShells_DoNothingToProps()
    {
        Simulation sim = One(OverlayKind.Tree);
        ulong hash = sim.ComputeStateHash();
        sim.Detonate(0, 10_000, PropFixture.SmallArmsWarhead, At(C), default, true);    // instant hit, splash 0
        sim.Detonate(0, 10_000, PropFixture.SmallArmsWarhead, At(C), default, false);   // a splash-0 shell
        Assert.Equal(OverlayKind.Tree, sim.Map.OverlayAt(C));
        Assert.Equal(PropRules.Default.TreeHealth, sim.Props.HealthAt(C));
        Assert.Equal(hash, sim.ComputeStateHash());
    }

    [Fact]
    public void WarheadVersusZeroForTheArmour_DoesNothing()
    {
        SimRules rules = PropFixture.Rules();
        rules = rules with { Warheads = new[] { new WarheadType("soft", new[] { 100, 100, 100, 0, 100, 100, 100, 100 }, 1, false) } };
        var sim = new Simulation(new SimSetup(1, PropFixture.Map(24, (C, OverlayKind.Tree)), 2, rules, 1) { AllowDebug = true });
        sim.Detonate(0, 10_000, 0, At(C), default, false);                              // Structure column = 0
        Assert.Equal(OverlayKind.Tree, sim.Map.OverlayAt(C));
    }

    [Fact]
    public void VersusOfTheConfiguredArmourClass_Scales_TheDamage()
    {
        var rules = new PropRules(40, 40, 160, ArmorClass.Light, 3);   // Light column of the fixture warhead is 100: use a 50 % warhead
        SimRules r = PropFixture.Rules(rules) with { Warheads = new[] { new WarheadType("half", new[] { 100, 50, 100, 100, 100, 100, 100, 100 }, 1, false) } };
        var sim = new Simulation(new SimSetup(1, PropFixture.Map(24, (C, OverlayKind.Wreck)), 2, r, 1) { AllowDebug = true });
        sim.Detonate(0, 40, 0, At(C), default, false);                                  // 40 x 50 % = 20
        Assert.Equal(160 - 20, sim.Props.HealthAt(C));
    }

    [Theory]
    [InlineData(LandType.Clear, LandType.Rough)]
    [InlineData(LandType.Road, LandType.Rough)]
    [InlineData(LandType.Ash, LandType.Rough)]
    [InlineData(LandType.Rough, LandType.Rough)]
    [InlineData(LandType.Rock, LandType.Rock)]       // ASSUMED (S31): impassable ground is never opened by a prop
    [InlineData(LandType.Cliff, LandType.Cliff)]
    public void TheLandUnderADestroyedProp_BecomesRough_ExceptImpassableGround(LandType before, LandType after)
    {
        Simulation sim = One(OverlayKind.Tree, before);
        Splash(sim, C, 1000);
        Assert.Equal(OverlayKind.None, sim.Map.OverlayAt(C));
        Assert.Equal(after, sim.Map.LandAt(C));
    }

    [Fact]
    public void ABigSplash_DamagesEveryPropInRange_InOneCall()
    {
        var props = new List<(CellCoord, OverlayKind)>();
        for (int dx = -1; dx <= 1; dx++) props.Add((new CellCoord(C.X + dx, C.Y), OverlayKind.Tree));
        props.Add((new CellCoord(C.X, C.Y + 1), OverlayKind.Block));
        Simulation sim = PropFixture.Sim(PropFixture.Map(24, props.ToArray()));
        Splash(sim, C, 1000);
        Assert.Equal(3, sim.FxEvents.Count(f => f.Kind == FxKind.PropDestroyed));
        Assert.Equal(OverlayKind.Block, sim.Map.OverlayAt(new CellCoord(C.X, C.Y + 1)));
        Assert.Equal(0, sim.Props.Count);
    }

    [Fact]
    public void ADeathWeapon_IsASplashDetonation_SoItLevelsProps()
    {
        // OnDeathWeapon fires the weapon through Detonate at the unit's cell (Simulation.Effects.cs): the same call as above.
        Simulation sim = One(OverlayKind.Tree);
        sim.Detonate(0, 60, PropFixture.SplashWarhead, At(new CellCoord(C.X - 1, C.Y)), default, false);   // beside the tree: half of 60 = 30 < 40
        Assert.Equal(10, sim.Props.HealthAt(C));
        sim.Detonate(0, 60, PropFixture.SplashWarhead, At(new CellCoord(C.X - 1, C.Y)), default, false);
        Assert.Equal(OverlayKind.None, sim.Map.OverlayAt(C));
    }

    // ---- superweapon blast and salvo impact -----------------------------------------------------------------------------------------

    private static Simulation SuperSim(bool salvo, params (CellCoord, OverlayKind)[] props)
    {
        SimRules r = SuperweaponFixture.Rules();
        WarheadType w = new("sw_blast", new[] { 100, 100, 100, 100, 100 }, 1, false);
        if (salvo) w = w with { SalvoMissiles = 1, SalvoTicks = 1, SalvoRadius = 0, SalvoDamage = 100, SalvoWarnTicks = 1, SalvoSplash = 1 };
        r = r with { Warheads = new[] { w }, Props = PropRules.Default with { SplashArmor = ArmorClass.Unarmored } };
        var sim = new Simulation(new SimSetup(1, PropFixture.Map(48, props), 2, r, 0x1216UL) { AllowDebug = true });
        sim.PlaceBuilding(0, Plant, new CellCoord(10, 10));
        EntityId sun = sim.PlaceBuilding(0, Sun, new CellCoord(14, 10));
        sim.PlaceBuilding(0, Hatch, new CellCoord(20, 10));
        sim.PlaceBuilding(1, Hut, new CellCoord(44, 44));
        sim.Effects.BuildingCharge[sun.Index] = Total;
        return sim;
    }

    [Fact]
    public void TheSuperweaponBlast_LevelsTreeSnagAndWreck_AndSparesBlockAndPylon()
    {
        var target = new CellCoord(25, 14);
        Simulation sim = SuperSim(false,
            (target, OverlayKind.Wreck),                                                     // 700 damage: a wreck goes too
            (new CellCoord(25 + 3, 14), OverlayKind.Tree),                                   // outside half the radius: 350, still dead
            (new CellCoord(25, 14 + 5), OverlayKind.Snag),                                   // at the radius
            (new CellCoord(25 - 1, 14), OverlayKind.Block), (new CellCoord(25 + 1, 14), OverlayKind.Pylon),
            (new CellCoord(25 + 9, 14), OverlayKind.Tree));                                  // beyond the blast
        SuperweaponFixture.Run(sim, 1, Fire(sim, 0, FindSun(sim), 25, 14));
        Assert.Equal(OverlayKind.None, sim.Map.OverlayAt(target));
        Assert.Equal(OverlayKind.None, sim.Map.OverlayAt(new CellCoord(28, 14)));
        Assert.Equal(OverlayKind.None, sim.Map.OverlayAt(new CellCoord(25, 19)));
        Assert.Equal(OverlayKind.Block, sim.Map.OverlayAt(new CellCoord(24, 14)));
        Assert.Equal(OverlayKind.Pylon, sim.Map.OverlayAt(new CellCoord(26, 14)));
        Assert.Equal(OverlayKind.Tree, sim.Map.OverlayAt(new CellCoord(34, 14)));
        Assert.Equal(3, sim.FxEvents.Count(f => f.Kind == FxKind.PropDestroyed));
    }

    [Fact]
    public void ASalvoMissile_LevelsPropsAroundItsImpact()
    {
        var target = new CellCoord(25, 14);
        Simulation sim = SuperSim(true, (target, OverlayKind.Tree), (new CellCoord(26, 14), OverlayKind.Tree), (new CellCoord(28, 14), OverlayKind.Tree));
        SuperweaponFixture.Run(sim, 1, Fire(sim, 0, FindSun(sim), 25, 14));
        SuperweaponFixture.Run(sim, 10);
        Assert.Equal(OverlayKind.None, sim.Map.OverlayAt(target));
        Assert.Equal(OverlayKind.None, sim.Map.OverlayAt(new CellCoord(26, 14)));    // splash 1: the neighbour takes half of 100 = 50, a tree is 40
        Assert.Equal(OverlayKind.Tree, sim.Map.OverlayAt(new CellCoord(28, 14)));
    }

    private static EntityId FindSun(Simulation sim)
    {
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.TypeIndex[i] == Sun) return sim.Buildings.Slots.IdOf(i);
        throw new InvalidOperationException("no superweapon building");
    }
}
