// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S19;

/// <summary>S19 D-205 (M13-04): the game never keeps a carried (garrisoned) unit selected or in a recalled control group. Shipped rules.</summary>
public sealed class CarriedSelectionTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    private static (Simulation Sim, RuleSet Rules, int Rifleman, int Blockhouse, EntityId House) Scene()
    {
        string rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        RuleSet rules = RulesLoader.Load(rulesDir);
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(40, 40), 2, rules.Sim, rules.RulesHash));
        int blockhouse = SimRules.IndexOf(rules.Sim.Buildings, "auth_blockhouse", b => b.Id);
        int rifleman = SimRules.IndexOf(rules.Sim.Infantry, "auth_rifleman", i => i.Id);
        EntityId house = sim.PlaceBuilding(0, blockhouse, new CellCoord(10, 10));
        return (sim, rules, rifleman, blockhouse, house);
    }

    private static List<RenderUnit> Units(Simulation sim)
    {
        SimSnapshot snap = sim.Snapshot(0);
        var list = new List<RenderUnit>();
        UnitInterpolator.Build(snap, snap, 1f, list);
        return list;
    }

    private static void EnterAndWait(Simulation sim, EntityId man, EntityId house)
    {
        sim.Step(new[] { OrderBuilder.Build(sim.Tick, 0, 0, CommandKind.Enter, new UnitKey(ActorKind.Infantry, man), default, new ActorRef(ActorKind.Building, house)) });
        for (int t = 0; t < 100 && sim.LayerOf(ActorKind.Infantry, man.Index) != ActorLayer.Garrisoned; t++) sim.Step(None);
        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, man.Index));
    }

    [Fact]
    public void ASoldierOrderedToEnter_IsSelectedOutside_ThenLeavesTheSelectionOnceInside()
    {
        var (sim, _, rifleman, blockhouse, house) = Scene();
        BuildingType bt = sim.Setup.Rules.Buildings[blockhouse];
        EntityId man = sim.SpawnInfantry(0, rifleman, new CellCoord(10 + bt.FootprintW, 10));
        EntityId other = sim.SpawnInfantry(0, rifleman, new CellCoord(20, 20));
        var key = new UnitKey(ActorKind.Infantry, man);
        var otherKey = new UnitKey(ActorKind.Infantry, other);
        var selection = new Selection();
        selection.Replace(new[] { key, otherKey });

        selection.DropCarried(Units(sim));
        Assert.Equal(2, selection.Count);   // both on the surface: kept

        EnterAndWait(sim, man, house);
        selection.DropCarried(Units(sim));

        Assert.False(selection.Contains(key));
        Assert.True(selection.Contains(otherKey));
    }

    [Fact]
    public void ControlGroupRecall_SkipsCarriedMembers_ButTheGroupKeepsThem()
    {
        var (sim, _, rifleman, blockhouse, house) = Scene();
        BuildingType bt = sim.Setup.Rules.Buildings[blockhouse];
        EntityId inside = sim.SpawnInfantry(0, rifleman, new CellCoord(10 + bt.FootprintW, 10));
        EntityId outside = sim.SpawnInfantry(0, rifleman, new CellCoord(20, 20));
        var insideKey = new UnitKey(ActorKind.Infantry, inside);
        var outsideKey = new UnitKey(ActorKind.Infantry, outside);
        var groups = new ControlGroups();
        groups.Set(1, new[] { insideKey, outsideKey });

        EnterAndWait(sim, inside, house);
        List<UnitKey> recalled = Selection.WithoutCarried(groups.Members(1), Units(sim));

        Assert.Equal(new[] { outsideKey }, recalled);
        Assert.Contains(insideKey, groups.Members(1));   // still a member: it can be recalled after it is unloaded
    }

    [Fact]
    public void BoxSelectArmyPickAndSameType_SkipTheCarriedSoldier_ButPickTheBuildingAround()
    {
        var (sim, rules, rifleman, blockhouse, house) = Scene();
        BuildingType bt = sim.Setup.Rules.Buildings[blockhouse];
        EntityId man = sim.SpawnInfantry(0, rifleman, new CellCoord(10 + bt.FootprintW, 10));
        EntityId free = sim.SpawnInfantry(0, rifleman, new CellCoord(10 + bt.FootprintW, 14));
        EnterAndWait(sim, man, house);
        var key = new UnitKey(ActorKind.Infantry, man);
        var freeKey = new UnitKey(ActorKind.Infantry, free);
        List<RenderUnit> units = Units(sim);
        Vector2 at = Camera.WorldFromLepton(sim.Infantry.Position[man.Index]);
        Vector2 pad = new(4, 4), big = new(2000, 2000);

        Assert.Empty(Selection.Inside(units, at - pad, at + pad, 0));
        Assert.Equal(new[] { freeKey }, Selection.Inside(units, at - big, at + big, 0).ToArray());
        Assert.Equal(new[] { freeKey }, Selection.Army(units, 0, u => Selection.IsCombatUnit(rules.Sim, u)).ToArray());
        Assert.Null(Selection.Pick(units, at, 0));
        Assert.Equal(new[] { freeKey }, Selection.SameTypeInside(units, at - big, at + big, 0, freeKey).ToArray());
        Assert.DoesNotContain(key, Selection.SameTypeInside(units, at - big, at + big, 0, key));
    }
}
