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

namespace Rts.Game.Tests.S18;

/// <summary>S18 audit (SIMDET-01 / SIM-RULES-01, UI reachability): the game's own selection code can pick a soldier inside a garrison
/// and the ordinary Move order it then issues walks it out with layer Garrisoned. Shipped rules, no test doubles.</summary>
public sealed class GarrisonSelectionAuditTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    private static (Simulation Sim, RuleSet Rules, EntityId Man, EntityId House) GarrisonedRifleman()
    {
        string rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        RuleSet rules = RulesLoader.Load(rulesDir);
        var sim = new Simulation(new SimSetup(1, MapData.CreateFlat(40, 40), 2, rules.Sim, rules.RulesHash));
        int blockhouse = SimRules.IndexOf(rules.Sim.Buildings, "auth_blockhouse", b => b.Id);
        int rifleman = SimRules.IndexOf(rules.Sim.Infantry, "auth_rifleman", i => i.Id);
        EntityId house = sim.PlaceBuilding(0, blockhouse, new CellCoord(10, 10));
        BuildingType bt = rules.Sim.Buildings[blockhouse];
        EntityId man = sim.SpawnInfantry(0, rifleman, new CellCoord(10 + bt.FootprintW, 10));
        var key = new UnitKey(ActorKind.Infantry, man);
        sim.Step(new[] { OrderBuilder.Build(sim.Tick, 0, 0, CommandKind.Enter, key, default, new ActorRef(ActorKind.Building, house)) });
        for (int t = 0; t < 100 && sim.LayerOf(ActorKind.Infantry, man.Index) != ActorLayer.Garrisoned; t++) sim.Step(None);
        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, man.Index));
        return (sim, rules, man, house);
    }

    private static List<RenderUnit> RenderUnits(Simulation sim)
    {
        SimSnapshot snap = sim.Snapshot(0);
        var list = new List<RenderUnit>();
        UnitInterpolator.Build(snap, snap, 1f, list);   // exactly what GameScreen feeds the selection code
        return list;
    }

    // S18 audit: pins current behaviour of SIMDET-01 / SIM-RULES-01 (UI reachability); flip the assertion when fixed.
    [Fact]
    public void Audit_OwnSnapshot_ListsTheGarrisonedSoldier_AsAnOrdinaryUnit()
    {
        var (sim, _, man, _) = GarrisonedRifleman();
        UnitView view = Assert.Single(sim.Snapshot(0).Units, u => u.Kind == UnitKind.Infantry && u.Id == man);
        Assert.False(view.Underground);   // no flag tells the client it is inside a building
        Assert.False(view.Stealthed);
        Assert.DoesNotContain(sim.Snapshot(1).Units, u => u.Kind == UnitKind.Infantry && u.Id == man);
    }

    // S19 D-205: the selection helpers skip a carried soldier, and a Move order sent to it anyway (a stale selection) is dropped by the sim.
    [Fact]
    public void BoxSelectArmyAndPick_SkipTheGarrisonedSoldier_AndAMoveOrderDoesNotWalkItOut()
    {
        var (sim, rules, man, house) = GarrisonedRifleman();
        var key = new UnitKey(ActorKind.Infantry, man);
        List<RenderUnit> units = RenderUnits(sim);
        Vector2 at = Camera.WorldFromLepton(sim.Infantry.Position[man.Index]);   // the building centre
        var pad = new Vector2(4, 4);
        Assert.DoesNotContain(key, Selection.Inside(units, at - pad, at + pad, 0));                               // drag a box over the building
        Assert.DoesNotContain(key, Selection.Army(units, 0, u => Selection.IsCombatUnit(rules.Sim, u)));          // "select army"
        Assert.Null(Selection.Pick(units, at, 0));                                                                // a plain click on the building centre

        // The order the game then issues for a right-click on open ground:
        sim.Step(new[] { OrderBuilder.Build(sim.Tick, 0, 1, CommandKind.Move, key, new CellCoord(25, 10), default) });
        for (int t = 0; t < 600; t++) sim.Step(None);
        CellCoord cell = sim.Infantry.Position[man.Index].ToCell();
        Assert.True(cell.X < 20, $"the order must be dropped; the soldier is at {cell}");
        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, man.Index));
        Assert.Equal(1, sim.OccupantsIn(house));
    }

    [Fact]
    public void Desired_SelectionNeverPicksAGarrisonedSoldier()
    {
        var (sim, rules, man, _) = GarrisonedRifleman();
        var key = new UnitKey(ActorKind.Infantry, man);
        List<RenderUnit> units = RenderUnits(sim);
        Vector2 at = Camera.WorldFromLepton(sim.Infantry.Position[man.Index]);
        var pad = new Vector2(4, 4);
        Assert.DoesNotContain(key, Selection.Inside(units, at - pad, at + pad, 0));
        Assert.DoesNotContain(key, Selection.Army(units, 0, u => Selection.IsCombatUnit(rules.Sim, u)));
    }
}
