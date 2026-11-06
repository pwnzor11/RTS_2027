// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Render;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M7;

/// <summary>M7-14: shipment timer text, beachhead mask lookup, landing unload order choice, dive toggle, dived alpha, ocean header round trip.</summary>
public sealed class M7UiTests
{
    private static readonly SimRules Rules = Build();
    private static readonly MapData Map = BuildMap();

    private static SimRules Build()
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        SimRules baseRules = RulesLoader.Load(dir).Sim;
        VehicleType v = baseRules.Vehicles[0];
        return baseRules with
        {
            Vehicles = new[]
            {
                v with { Passengers = 0, UnloadTicks = 0, Submerges = false },                              // 0 plain
                v with { Passengers = 6, UnloadTicks = 10, Locomotion = Locomotion.Naval },                 // 1 landing craft
                v with { Passengers = 4, UnloadTicks = 0, Locomotion = Locomotion.Air },                    // 2 Skyhook-like
                v with { Passengers = 4, UnloadTicks = 0, Locomotion = Locomotion.Wheeled },                // 3 ordinary APC
                v with { Passengers = 0, UnloadTicks = 0, Submerges = true, Locomotion = Locomotion.Naval },// 4 submarine
            },
        };
    }

    // 4x1 strip: water, water, coast, clear.
    private static MapData BuildMap() => new("t", "temperate", 4, 1, new ushort[4],
        new[] { LandType.Water, LandType.Water, LandType.Coast, LandType.Clear }, new byte[4], new OverlayKind[4], new byte[4],
        new[] { new CellCoord(0, 0) });

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(1, "0:01")]
    [InlineData(SimConstants.TicksPerSecond, "0:01")]
    [InlineData(SimConstants.TicksPerSecond * 65, "1:05")]
    [InlineData(SimConstants.TicksPerSecond * 600 + 1, "10:01")]
    public void Shipment_clock_rounds_up_to_whole_seconds(int ticks, string expected) => Assert.Equal(expected, ShipmentReadout.Clock(ticks));

    [Fact]
    public void Shipment_row_text_and_visibility()
    {
        Assert.Equal("Shipment in 1:05 +300", ShipmentReadout.Text(SimConstants.TicksPerSecond * 65, 300));
        Assert.False(ShipmentReadout.Shown(-1, 300));
        Assert.False(ShipmentReadout.Shown(100, 0));
        Assert.True(ShipmentReadout.Shown(0, 300));
        Assert.True(ShipmentReadout.Decayed(1));
        Assert.False(ShipmentReadout.Decayed(0));
        Assert.Equal("Shipment in {time} +{payload}", UiText.Get(UiText.ShipmentTimer));
    }

    [Fact]
    public void Beachhead_mask_lookup_reads_the_right_bit()
    {
        int cell = 1 * 100 + 30;   // width 100: (30, 1) = cell 130 = word 2, bit 2
        var mask = new ulong[3];
        mask[cell >> 6] |= 1UL << (cell & 63);
        Assert.True(BeachheadOverlay.Contains(mask, 100, 30, 1));
        Assert.False(BeachheadOverlay.Contains(mask, 100, 31, 1));
        Assert.False(BeachheadOverlay.Contains(mask, 100, -1, 1));
        Assert.False(BeachheadOverlay.Contains(mask, 100, 100, 1));
        Assert.False(BeachheadOverlay.Contains(ReadOnlySpan<ulong>.Empty, 100, 0, 0));
    }

    [Theory]
    [InlineData(1, 3, 3, true)]    // landing craft, cargo, clear cell
    [InlineData(1, 3, 2, true)]    // coast
    [InlineData(1, 3, 0, false)]   // water: that is a Move
    [InlineData(1, 0, 3, false)]   // empty craft
    [InlineData(2, 2, 3, true)]    // air transport
    [InlineData(3, 4, 3, false)]   // ordinary APC unloads with D, not by right-click
    [InlineData(0, 0, 3, false)]
    public void Right_click_unloads_only_a_loaded_landing_transport_on_land(int type, int passengers, int x, bool expected) =>
        Assert.Equal(expected, LandingOrders.ShouldUnload(Rules, Map, ActorKind.Vehicle, type, passengers, new CellCoord(x, 0)));

    [Fact]
    public void Infantry_and_off_map_cells_never_unload()
    {
        Assert.False(LandingOrders.ShouldUnload(Rules, Map, ActorKind.Infantry, 1, 3, new CellCoord(3, 0)));
        Assert.False(LandingOrders.ShouldUnload(Rules, Map, ActorKind.Vehicle, 1, 3, new CellCoord(9, 0)));
    }

    [Fact]
    public void A_submarine_deploys_in_place_as_its_dive_toggle()
    {
        Assert.Equal(DeployMode.InPlace, M3Orders.DeployModeOf(Rules, ActorKind.Vehicle, 4));
        Assert.True(M3Orders.IsDiver(Rules, ActorKind.Vehicle, 4));
        Assert.False(M3Orders.IsDiver(Rules, ActorKind.Vehicle, 1));
        Assert.Equal("Dive (D)", DiveOffer.Caption(false));
        Assert.Equal("Surface (D)", DiveOffer.Caption(true));
    }

    [Fact]
    public void A_dived_ship_is_drawn_semi_transparent()
    {
        var view = new UnitView(new EntityId(0, 0), UnitKind.Vehicle, 0, 4, default, 0, 100);
        Assert.Equal(1f, UnitVisuals.Alpha(view, false, false));
        Assert.Equal(UnitVisuals.SubmergedAlpha, UnitVisuals.Alpha(view, true, true));
        Assert.InRange(UnitVisuals.SubmergedAlpha, 0.1f, 0.9f);
    }

    private static GameSaveHeader RoundTrip(GameSaveHeader h, int version = GameSaveHeader.CurrentVersion)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) h.Write(w);
        ms.Position = 0;
        using var r = new BinaryReader(ms);
        return GameSaveHeader.Read(r, version);
    }

    private static GameSaveHeader WithMap(MapChoice map) => new()
    {
        Mode = SaveMode.Skirmish, MapId = "x", MapName = "x", RulesHash = 1, Build = "b",
        Skirmish = new SkirmishSettings(map, SkirmishSettings.Default.Slots, 5),
    };

    [Theory]
    [InlineData(OceanEdges.None)]
    [InlineData(OceanEdges.North)]
    [InlineData(OceanEdges.East | OceanEdges.West)]
    public void The_header_round_trips_the_ocean_edges_of_a_generated_map(OceanEdges edges)
    {
        var gen = new MapGenParams(Biome.GlassCoast, MapSize.Medium, 2, 77, "named", edges);
        GameSaveHeader back = RoundTrip(WithMap(new MapChoice(null, gen)));
        Assert.Equal(gen, back.Skirmish!.Map.Generated);
        Assert.Equal(edges, back.Skirmish.Map.Generated!.OceanEdges);
    }

    [Fact]
    public void A_version_four_header_loads_with_no_ocean()
    {
        var gen = new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 9, null, OceanEdges.South);
        GameSaveHeader v4 = WithMap(new MapChoice(null, gen)) with { Version = 4 };
        GameSaveHeader back = RoundTrip(v4, 4);
        Assert.Equal(OceanEdges.None, back.Skirmish!.Map.Generated!.OceanEdges);
        Assert.Equal(gen with { OceanEdges = OceanEdges.None }, back.Skirmish.Map.Generated);
    }

    [Fact]
    public void A_map_file_choice_writes_no_ocean_byte()
    {
        GameSaveHeader h = WithMap(new MapChoice("a.map.toml", null));
        Assert.Equal(h.Skirmish, RoundTrip(h).Skirmish);
    }

    [Fact]
    public void The_setup_screen_cycles_none_one_edge_and_two_opposite_edges()
    {
        Assert.Equal(OceanEdges.North, OceanChoices.Step(OceanEdges.None, 1));
        Assert.Equal(OceanEdges.East | OceanEdges.West, OceanChoices.Step(OceanEdges.None, -1));
        Assert.Equal(OceanEdges.None, OceanChoices.Step(OceanEdges.East | OceanEdges.West, 1));
        Assert.Equal("Ocean: None", OceanChoices.Label(OceanEdges.None));
        Assert.Equal("Ocean: North and South", OceanChoices.Label(OceanEdges.North | OceanEdges.South));
        Assert.All(OceanChoices.All, e => Assert.True(System.Numerics.BitOperations.PopCount((uint)e) <= 2));
    }
}
