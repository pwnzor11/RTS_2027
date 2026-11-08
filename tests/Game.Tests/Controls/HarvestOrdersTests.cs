// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Controls;

/// <summary>M11-10a: the harvester right-click classifier and how <see cref="OrderController"/> routes it.</summary>
public sealed class HarvestOrdersTests : IDisposable
{
    private readonly string _replay = Path.Combine(Path.GetTempPath(), "rts_harvest_" + Guid.NewGuid().ToString("N") + ".rpl");

    public void Dispose()
    {
        foreach (string f in new[] { _replay, _replay + ".prev", _replay + ".map.toml" })
            if (File.Exists(f)) File.Delete(f);
    }

    private static int VehicleIndex(SimRules rules, string id) => SimRules.IndexOf(rules.Vehicles, id, v => v.Id);

    // ---- classifier ----------------------------------------------------------------------------

    [Fact]
    public void Every_faction_harvester_classifies_and_others_do_not()
    {
        (SkirmishGame game, _) = StarterHarvestTests.Setup();
        SimRules rules = game.Sim.Setup.Rules;
        int harvesters = 0;
        for (int t = 0; t < rules.Vehicles.Count; t++)
        {
            bool cap = rules.Vehicles[t].HarvestCapacity > 0;
            if (cap) harvesters++;
            Assert.Equal(cap ? HarvestClick.Harvest : HarvestClick.None, HarvestOrders.Classify(rules, ActorKind.Vehicle, t, OverlayKind.Ashfall, null, false));
        }
        Assert.True(harvesters >= 4, "one harvester per faction");
        Assert.Equal(HarvestClick.None, HarvestOrders.Classify(rules, ActorKind.Infantry, 0, OverlayKind.Ashfall, null, false));
    }

    [Fact]
    public void Resource_cells_harvest_and_other_cells_do_not()
    {
        (SkirmishGame game, _) = StarterHarvestTests.Setup();
        SimRules rules = game.Sim.Setup.Rules;
        int scraper = VehicleIndex(rules, "auth_scraper");
        Assert.Equal(HarvestClick.Harvest, HarvestOrders.Classify(rules, ActorKind.Vehicle, scraper, OverlayKind.Ashfall, null, false));
        Assert.Equal(HarvestClick.Harvest, HarvestOrders.Classify(rules, ActorKind.Vehicle, scraper, OverlayKind.Pitchblende, null, false));
        Assert.Equal(HarvestClick.None, HarvestOrders.Classify(rules, ActorKind.Vehicle, scraper, OverlayKind.None, null, false));
        Assert.Equal(HarvestClick.None, HarvestOrders.Classify(rules, ActorKind.Vehicle, scraper, OverlayKind.Wall, null, false));
    }

    [Fact]
    public void Own_refinery_returns_but_other_buildings_and_foreign_refineries_do_not()
    {
        (SkirmishGame game, _) = StarterHarvestTests.Setup();
        SimRules rules = game.Sim.Setup.Rules;
        int scraper = VehicleIndex(rules, "auth_scraper");
        int refinery = SimRules.IndexOf(rules.Buildings, "auth_refinery", b => b.Id);
        int conyard = SimRules.IndexOf(rules.Buildings, "auth_conyard", b => b.Id);
        BuildingView Of(int type, bool ghost = false) => new(new EntityId(1, 0), 0, type, new CellCoord(3, 3), 3, 3, 100, ghost);
        Assert.Equal(HarvestClick.Return, HarvestOrders.Classify(rules, ActorKind.Vehicle, scraper, OverlayKind.None, Of(refinery), true));
        Assert.Equal(HarvestClick.None, HarvestOrders.Classify(rules, ActorKind.Vehicle, scraper, OverlayKind.None, Of(refinery), false));
        Assert.Equal(HarvestClick.None, HarvestOrders.Classify(rules, ActorKind.Vehicle, scraper, OverlayKind.None, Of(conyard), true));
        Assert.Equal(HarvestClick.None, HarvestOrders.Classify(rules, ActorKind.Vehicle, scraper, OverlayKind.None, Of(refinery, ghost: true), true));
    }

    // ---- controller routing --------------------------------------------------------------------

    private (SimSnapshotSource Source, OrderController Orders, Selection Selection, Camera Camera, EntityId Scraper, CellCoord Field) Rig()
    {
        (SkirmishGame game, EntityId scraper) = StarterHarvestTests.Setup();
        var source = new SimSnapshotSource(game, StarterHarvestTests.RulesDir, _replay);
        var selection = new Selection();
        selection.Replace(new[] { new UnitKey(ActorKind.Vehicle, scraper) });
        var orders = new OrderController(source, selection);
        // a field of ashfall a few cells from the scraper, on open ground
        Simulation sim = game.Sim;
        CellCoord at = sim.Units.Position[scraper.Index].ToCell();
        CellCoord field = default;
        for (int r = 5; r < 12 && field == default; r++)
            for (int dx = -r; dx <= r && field == default; dx++)
            {
                var c = new CellCoord(at.X + dx, at.Y + r);
                if (sim.Setup.Map.Contains(c) && sim.Map.BuildingAt(c).IsNone && sim.Map.IsPassableTerrain(c, Locomotion.Tracked, sim.Setup.Rules.Terrain))
                    field = c;
            }
        Assert.NotEqual(default, field);
        sim.Map.SetOverlay(field, OverlayKind.Ashfall, 200);
        return (source, orders, selection, new Camera(), scraper, field);
    }

    private static Point Screen(Camera camera, CellCoord cell) => camera.ScreenFromWorld(Camera.CentreOf(cell)).ToPoint();

    private static Command Last(SimSnapshotSource s) => s.Host.Pending[^1];

    [Fact]
    public void Right_click_on_a_field_sends_Harvest_with_that_cell()
    {
        var r = Rig();
        r.Orders.RightClick(Screen(r.Camera, r.Field), r.Camera, Array.Empty<RenderUnit>());
        Command c = Last(r.Source);
        Assert.Equal((CommandKind.Harvest, r.Scraper, r.Field), (c.Kind, c.Subject, c.Cell));
    }

    [Fact]
    public void Right_click_on_own_refinery_sends_Return_and_on_open_ground_sends_Move()
    {
        var r = Rig();
        Simulation sim = r.Source.Sim;
        SimRules rules = sim.Setup.Rules;
        int refinery = SimRules.IndexOf(rules.Buildings, "auth_refinery", b => b.Id);
        BuildingView view = r.Source.Current.Buildings.First(b => b.TypeIndex == refinery && b.Player == 0);
        r.Orders.RightClick(Screen(r.Camera, view.Cell), r.Camera, Array.Empty<RenderUnit>());
        Assert.Equal(CommandKind.Return, Last(r.Source).Kind);

        CellCoord open = new(r.Field.X + 1, r.Field.Y);
        sim.Map.SetOverlay(open, OverlayKind.None, 0);
        r.Orders.RightClick(Screen(r.Camera, open), r.Camera, Array.Empty<RenderUnit>());
        Assert.Equal(CommandKind.Move, Last(r.Source).Kind);
    }

    [Fact]
    public void In_a_mixed_selection_only_harvesters_harvest()
    {
        var r = Rig();
        Simulation sim = r.Source.Sim;
        int jeep = VehicleIndex(sim.Setup.Rules, "auth_jeep");
        CellCoord at = sim.Units.Position[r.Scraper.Index].ToCell();
        EntityId other = EntityId.None;
        for (int dx = -4; dx <= 4 && other.IsNone; dx++)
            for (int dy = -4; dy <= 4 && other.IsNone; dy++)
            {
                var c = new CellCoord(at.X + dx, at.Y + dy);
                if (sim.Setup.Map.Contains(c) && sim.Map.BuildingAt(c).IsNone && sim.Map.VehicleAt(c).IsNone
                    && sim.Map.IsPassableTerrain(c, Locomotion.Wheeled, sim.Setup.Rules.Terrain))
                    other = sim.SpawnVehicle(0, jeep, c);
            }
        r.Selection.Replace(new[] { new UnitKey(ActorKind.Vehicle, r.Scraper), new UnitKey(ActorKind.Vehicle, other) });
        r.Source.Host.RefreshSnapshots();
        int before = r.Source.Host.Pending.Count;
        r.Orders.RightClick(Screen(r.Camera, r.Field), r.Camera, Array.Empty<RenderUnit>());
        var sent = r.Source.Host.Pending.Skip(before).ToList();
        Assert.Contains(sent, c => c.Subject == r.Scraper && c.Kind == CommandKind.Harvest);
        Assert.Contains(sent, c => c.Subject == other && c.Kind == CommandKind.Move);
    }

    [Fact]
    public void A_scraper_the_player_moved_away_harvests_again_after_a_click_on_a_field()
    {
        var r = Rig();
        Simulation sim = r.Source.Sim;
        // Move it: afterwards it is not harvesting any more and the sim does not start it again by itself.
        r.Source.Order(CommandKind.Move, new UnitKey(ActorKind.Vehicle, r.Scraper), new CellCoord(r.Field.X, r.Field.Y));
        r.Source.StepTicks(400);
        Assert.NotEqual(MissionKind.Harvest, sim.Units.Mission[r.Scraper.Index]);
        int before = sim.Funds(0);

        r.Orders.RightClick(Screen(r.Camera, r.Field), r.Camera, Array.Empty<RenderUnit>());
        r.Source.StepTicks(2);
        Assert.Equal(MissionKind.Harvest, sim.Units.Mission[r.Scraper.Index]);
        r.Source.StepTicks(3000);
        Assert.True(sim.Funds(0) > before, "the scraper delivered after the click");
    }
}
