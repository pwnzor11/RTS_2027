// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Menu;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Game.Tests.S19;

/// <summary>D-205: the Anchorage "must touch the shore" reason (S18 UXG-07) and the Expedition start cue (UXG-09).</summary>
[Collection("GameKeysActive")]
public sealed class ShoreAndExpeditionCueTests
{
    [Fact]
    public void A_floating_dock_that_touches_no_land_says_so()
    {
        Assert.Equal("Must touch the shore", PlacementCheck.Reason(PlacementResult.Blocked, false, CellProblem.None, needsShore: true));
        Assert.Equal("Blocked", PlacementCheck.Reason(PlacementResult.Blocked, false, CellProblem.None));
        Assert.Equal("Blocked by a unit", PlacementCheck.Reason(PlacementResult.Blocked, false, CellProblem.Unit, needsShore: true));   // a real cause wins
        Assert.Null(PlacementCheck.Reason(PlacementResult.Ok, true, CellProblem.None, needsShore: true));
    }

    private static SimSnapshotSource ExpeditionGame(SaveTestSupport t, Faction human)
    {
        var slots = new List<SkirmishSlot> { new(SlotKind.Human, human, 1, 0, 0), new(SlotKind.Ai, Faction.Authority, 1, 1, 1) };
        var settings = new SkirmishSettings(new MapChoice(null, new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 7, null, OceanEdges.North)), slots, 7);
        return GameScreen.BuildSkirmishSource(t.RulesDir, settings, Path.Combine(t.Folder, "r.rpl"));
    }

    [Fact]
    public void The_cue_applies_to_an_expedition_player_with_no_building_and_finds_the_ship()
    {
        using var t = new SaveTestSupport();
        SimSnapshotSource source = ExpeditionGame(t, Faction.Expedition);
        source.StepTicks(1);
        PlayerHud hud = source.Sim.HudFor(SimSnapshotSource.LocalPlayer);
        Assert.True(ExpeditionCue.Applies(hud, source.Current, SimSnapshotSource.LocalPlayer));
        UnitView? ship = ExpeditionCue.FindShip(source.Current, source.Rules, SimSnapshotSource.LocalPlayer);
        Assert.NotNull(ship);
        Assert.Equal(SimSnapshotSource.LocalPlayer, ship.Value.Player);
    }

    [Fact]
    public void The_cue_does_not_apply_to_other_factions()
    {
        using var t = new SaveTestSupport();
        SimSnapshotSource source = ExpeditionGame(t, Faction.Authority);
        source.StepTicks(1);
        Assert.False(ExpeditionCue.Applies(source.Sim.HudFor(SimSnapshotSource.LocalPlayer), source.Current, SimSnapshotSource.LocalPlayer));
        Assert.Null(ExpeditionCue.FindShip(source.Current, source.Rules, SimSnapshotSource.LocalPlayer));
    }

    [Fact]
    public void The_hint_names_the_bound_deploy_key()
    {
        KeyBindings saved = GameKeys.Active;
        try
        {
            GameKeys.Active = KeyBindings.Default;
            Assert.Equal("Your Command Ship is at the centre of the view. Select it and press D to land.", ExpeditionCue.Text());
            GameKeys.Active = KeyBindings.Default.With(GameAction.Deploy, Microsoft.Xna.Framework.Input.Keys.B);
            Assert.Contains("press B ", ExpeditionCue.Text());
        }
        finally { GameKeys.Active = saved; }
    }
}
