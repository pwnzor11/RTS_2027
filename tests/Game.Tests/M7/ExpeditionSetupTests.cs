// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M7;

/// <summary>M7-14: the Overseas Expedition in the skirmish setup (ocean edge for a generated map, landing coast for a hand-made map).</summary>
public sealed class ExpeditionSetupTests
{
    private static SkirmishSlot Slot(SlotKind kind, Faction faction, int colour, int team) =>
        new(kind, faction, 2, colour, team);

    private static SkirmishSettings FileMap(params SkirmishSlot[] slots) =>
        new(new MapChoice("hand_made.map.toml", null), slots, 1);

    private static SkirmishSettings GeneratedMap(OceanEdges ocean, params SkirmishSlot[] slots) =>
        new(new MapChoice(null, new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 7, null, ocean)), slots, 7);

    private static MapMeta Meta(params CellCoord[] landing) =>
        new("", MapSymmetry.None, 2, null, Array.Empty<MapFeature>()) { Landing = landing };

    [Fact]
    public void FactionName_ShowsTheOverseasExpedition()
    {
        Assert.Equal("Overseas Expedition", SkirmishSetupScreen.FactionName(Faction.Expedition));
    }

    [Fact]
    public void GeneratedMap_WithExpedition_AndNoOcean_GetsOneNorthEdgeAndANote()
    {
        OceanEdges edges = ExpeditionSetup.ResolveOcean(OceanEdges.None, expeditionOnGeneratedMap: true, out string? note);
        Assert.Equal(OceanEdges.South, edges);
        Assert.Equal("The Expedition needs a coast: one ocean edge added.", note);
    }

    [Fact]
    public void GeneratedMap_WithExpedition_AndAnOceanAlready_KeepsItsChoiceWithoutANote()
    {
        OceanEdges edges = ExpeditionSetup.ResolveOcean(OceanEdges.East | OceanEdges.West, expeditionOnGeneratedMap: true, out string? note);
        Assert.Equal(OceanEdges.East | OceanEdges.West, edges);
        Assert.Null(note);
    }

    [Fact]
    public void GeneratedMap_WithoutExpedition_KeepsNoOcean()
    {
        OceanEdges edges = ExpeditionSetup.ResolveOcean(OceanEdges.None, expeditionOnGeneratedMap: false, out string? note);
        Assert.Equal(OceanEdges.None, edges);
        Assert.Null(note);
    }

    [Fact]
    public void HasExpedition_IgnoresClosedSlots()
    {
        Assert.True(ExpeditionSetup.HasExpedition(new[] { Slot(SlotKind.Human, Faction.Authority, 0, 0), Slot(SlotKind.Ai, Faction.Expedition, 1, 1) }));
        Assert.False(ExpeditionSetup.HasExpedition(new[] { Slot(SlotKind.Human, Faction.Authority, 0, 0), Slot(SlotKind.Closed, Faction.Expedition, 1, 1) }));
    }

    [Fact]
    public void HandMadeMap_WithoutLandingCoast_RefusesAnExpeditionSlotWithTheSlotNumber()
    {
        SkirmishSettings s = FileMap(Slot(SlotKind.Human, Faction.Authority, 0, 0), Slot(SlotKind.Ai, Faction.Expedition, 1, 1));
        Assert.Equal("Slot 2: the Overseas Expedition needs a map with a landing coast.", ExpeditionSetup.LandingProblem(s, Meta()));
    }

    [Fact]
    public void HandMadeMap_WithALandingCoast_IsAccepted()
    {
        SkirmishSettings s = FileMap(Slot(SlotKind.Human, Faction.Expedition, 0, 0), Slot(SlotKind.Ai, Faction.Authority, 1, 1));
        Assert.Null(ExpeditionSetup.LandingProblem(s, Meta(new CellCoord(3, 4))));
    }

    [Fact]
    public void HandMadeMap_WithoutExpedition_IsAcceptedWithoutALandingCoast()
    {
        SkirmishSettings s = FileMap(Slot(SlotKind.Human, Faction.Authority, 0, 0), Slot(SlotKind.Ai, Faction.Directorate, 1, 1));
        Assert.Null(ExpeditionSetup.LandingProblem(s, Meta()));
    }

    [Fact]
    public void GeneratedMap_IsNotCheckedForALandingCoast()
    {
        SkirmishSettings s = GeneratedMap(OceanEdges.North, Slot(SlotKind.Human, Faction.Expedition, 0, 0), Slot(SlotKind.Ai, Faction.Authority, 1, 1));
        Assert.Null(ExpeditionSetup.LandingProblem(s, null));
    }

    [Fact]
    public void Expedition_IsNowAValidSlotFaction()
    {
        SkirmishSettings s = GeneratedMap(OceanEdges.North, Slot(SlotKind.Human, Faction.Expedition, 0, 0), Slot(SlotKind.Ai, Faction.Authority, 1, 1));
        Assert.Empty(s.Validate());
    }
}
