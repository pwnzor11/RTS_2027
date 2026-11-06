// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens.Editor;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Tests.M7;

/// <summary>M7-14: sidebar text for ships without a shipyard and selected carriers, the global upgrade wording, and the editor's save of convoy and landing markers.</summary>
public sealed class ExpeditionSidebarTests
{
    private static SimRules Rules { get; } = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found")).Sim;

    private static int NavalVehicle() => Enumerable.Range(0, Rules.Vehicles.Count).First(i => Rules.Vehicles[i].Locomotion == Locomotion.Naval);

    private static int LandVehicle() => Enumerable.Range(0, Rules.Vehicles.Count).First(i => Rules.Vehicles[i].Locomotion == Locomotion.Tracked);

    private static int Factory() => Enumerable.Range(0, Rules.Buildings.Count).First(i => Rules.Buildings[i].Produces == ProductionCategory.Vehicle);

    private static ViewActor Building(int type, CellCoord cell, bool ghost = false) =>
        new(EntityId.None, ActorKind.Building, 0, Relation.Self, type, cell, default, 100, 100, MissionKind.None, true, EntityId.None, ghost,
            Rules.Buildings[type].FootprintW, Rules.Buildings[type].FootprintH);

    [Fact]
    public void ShipyardText_IsTheOneLine()
    {
        Assert.Equal("Needs a shipyard on water", BlockReasonText.NeedsShipyardOnWater);
        Assert.Equal("Needs a factory", BlockReasonText.Of(QueueBlockReason.NoFactory));
    }

    [Fact]
    public void Ship_WithNoFactoryOnWater_NeedsAShipyard()
    {
        var own = new List<ViewActor> { Building(Factory(), new CellCoord(10, 10)) };
        Assert.True(ShipyardReach.NeedsShipyard(Rules, NavalVehicle(), own, _ => false));
    }

    [Fact]
    public void Ship_WithAFactoryBesideWater_DoesNotNeedAShipyard()
    {
        var own = new List<ViewActor> { Building(Factory(), new CellCoord(10, 10)) };
        Assert.False(ShipyardReach.NeedsShipyard(Rules, NavalVehicle(), own, c => c.X == 12 && c.Y == 10));
    }

    [Fact]
    public void Ship_WithNoFactoryAtAll_KeepsTheFactoryReason()
    {
        Assert.False(ShipyardReach.NeedsShipyard(Rules, NavalVehicle(), new List<ViewActor>(), _ => false));
    }

    [Fact]
    public void LandVehicle_NeverNeedsAShipyard()
    {
        var own = new List<ViewActor> { Building(Factory(), new CellCoord(10, 10)) };
        Assert.False(ShipyardReach.NeedsShipyard(Rules, LandVehicle(), own, _ => false));
    }

    [Fact]
    public void GhostFactory_IsIgnored()
    {
        var own = new List<ViewActor> { Building(Factory(), new CellCoord(10, 10), ghost: true) };
        Assert.False(ShipyardReach.NeedsShipyard(Rules, NavalVehicle(), own, _ => true));
    }

    [Theory]
    [InlineData(2, 4, "Hangar 2/4")]
    [InlineData(0, 3, "Hangar 0/3")]
    [InlineData(-1, 0, "Hangar 0/0")]
    public void Hangar_ReadsDockedOverSlots(int docked, int slots, string expected) =>
        Assert.Equal(expected, CarrierReadout.Text(docked, slots));

    [Fact]
    public void GlobalShipmentUpgrade_ReadsAsAllPlayers()
    {
        UpgradeType up = Rules.Upgrades.First(u => u.Id == "exp_up_fast_charging");
        Assert.Equal(new[] { "All players: shipment interval x0.85" }, UpgradeEffectText.Lines(Rules, up));
    }

    [Fact]
    public void Editor_SaveAndLoad_KeepsConvoyEdgeAndLanding()
    {
        string dir = Path.Combine(Path.GetTempPath(), "m7-14-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var convoy = new[] { new CellCoord(0, 5), new CellCoord(0, 6) };
            var landing = new[] { new CellCoord(3, 5) };
            var meta = new MapMeta("rust_belt", MapSymmetry.None, 2, null, Array.Empty<MapFeature>())
            {
                ConvoyEdge = convoy,
                Landing = landing,
            };
            MapData map = EditorDocument.Create("Marker Trip", "rust_belt", 32, 32).ToMapData();

            string path = Path.Combine(dir, "marker_trip.map.toml");
            MapFile.Save(map, EditorDocument.From(map, meta).ToMeta(), path);
            var (_, loaded) = MapFile.LoadWithMeta(path);

            Assert.Equal(convoy, loaded.ConvoyEdge);
            Assert.Equal(landing, loaded.Landing);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
