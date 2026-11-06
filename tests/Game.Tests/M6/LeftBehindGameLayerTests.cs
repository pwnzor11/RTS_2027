// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Host;
using Rts.Game.Render;
using Rts.Game.Saves;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Game.Ui.Sidebar;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Tests.M6;

/// <summary>M6-09: the Left Behind game layer (faction pick, Pack button, wreck and storm layers, salvage right click, headless skirmish).</summary>
public sealed class LeftBehindGameLayerTests
{
    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found");

    private static SkirmishSettings Of(SlotKind first, Faction f0, SlotKind second, Faction f1) =>
        new(new MapChoice(SkirmishSettings.CuratedFile, null),
            new[] { new SkirmishSlot(first, f0, 2, 0, 0), new SkirmishSlot(second, f1, 2, 1, 1) }, 1);

    // ---- faction pick and validation ----------------------------------------------------------

    [Fact]
    public void The_faction_cycle_offers_the_playable_factions_and_never_neutral()
    {
        var seen = new List<Faction>();
        Faction f = Faction.Authority;
        for (int i = 0; i < RuleCounts.PlayableFactions; i++) { seen.Add(f); f = SkirmishSetupScreen.NextFaction(f, 1); }
        Assert.Equal(new[] { Faction.Authority, Faction.Directorate, Faction.LeftBehind, Faction.Expedition }, seen);   // M7-14: the Expedition is offered last
        Assert.Equal(Faction.Authority, f);
        Assert.Equal("Left Behind", SkirmishSetupScreen.FactionName(Faction.LeftBehind));
        Assert.Equal("Overseas Expedition", SkirmishSetupScreen.FactionName(Faction.Expedition));
    }

    [Fact]
    public void Left_Behind_is_valid_for_a_human_and_an_ai_slot_and_neutral_is_not()
    {
        Assert.Empty(Of(SlotKind.Human, Faction.LeftBehind, SlotKind.Ai, Faction.Authority).Validate());
        Assert.Empty(Of(SlotKind.Ai, Faction.LeftBehind, SlotKind.Ai, Faction.LeftBehind).Validate());
        Assert.NotEmpty(Of(SlotKind.Human, Faction.Neutral, SlotKind.Ai, Faction.Authority).Validate());
    }

    [Fact]
    public void A_left_behind_setup_round_trips_through_the_save_header()
    {
        SkirmishSettings s = Of(SlotKind.Human, Faction.LeftBehind, SlotKind.Ai, Faction.LeftBehind);
        var h = new GameSaveHeader { Mode = SaveMode.Skirmish, MapId = "x", MapName = "x", RulesHash = 1, Skirmish = s, Build = "b" };
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) h.Write(w);
        ms.Position = 0;
        using var r = new BinaryReader(ms);
        SkirmishSettings back = GameSaveHeader.Read(r, GameSaveHeader.CurrentVersion).Skirmish!;
        Assert.Equal(s, back);
        Assert.Equal(Faction.LeftBehind, back.PlayerFaction);
        Assert.Equal(Faction.LeftBehind, back.AiFaction);
        Assert.Equal(new[] { Faction.LeftBehind, Faction.LeftBehind }, back.Factions);
    }

    // ---- start, hint, pack ----------------------------------------------------------------------

    [Fact]
    public void A_human_Left_Behind_player_starts_with_a_packed_rig_and_an_empty_sidebar_hint()
    {
        using SkirmishGame g = SkirmishGame.Create(RulesDir, Of(SlotKind.Human, Faction.LeftBehind, SlotKind.Ai, Faction.Authority));
        Simulation sim = g.Sim;
        PlayerView view = sim.ViewFor(0);
        EntityId rig = PackedRigOf(sim, 0);
        Assert.False(rig.IsNone);
        Assert.True(view.IsPackedRig(rig));
        Assert.True(view.UnpackTypeOf(rig) >= 0);

        SimSnapshot snap = new SimHost(sim).Current;
        Assert.True(LeftBehindText.ShowUnpackHint(sim.HudFor(0), snap, 0));
        Assert.Equal("Unpack the Mother Rig to build", LeftBehindText.Placeholder(LeftBehindText.UnpackHintKey));
        Assert.False(LeftBehindText.ShowUnpackHint(sim.HudFor(1), snap, 1));   // the Authority AI is not Left Behind
    }

    [Fact]
    public void The_pack_offer_follows_the_rules_and_the_progress()
    {
        SimRules rules = RulesLoader.Load(RulesDir).Sim;
        int rigBuilding = SimRules.IndexOf(rules.Buildings, "lb_mother_rig", b => b.Id);
        int auth = SimRules.IndexOf(rules.Buildings, "auth_power", b => b.Id);
        Assert.True(rigBuilding >= 0 && auth >= 0);

        PackOffer pack = PackOffer.ForBuilding(rules, rigBuilding, 0)!.Value;
        Assert.Equal(PackAction.Pack, pack.Action);
        Assert.True(pack.Enabled);
        Assert.False(PackOffer.ForBuilding(rules, rigBuilding, 400)!.Value.Enabled);   // already packing
        Assert.Null(PackOffer.ForBuilding(rules, auth, 0));                              // not a rig
        Assert.Null(PackOffer.ForBuilding(rules, -1, 0));

        PackOffer unpack = PackOffer.ForPackedRig(rules, rigBuilding, 0)!.Value;
        Assert.Equal(PackAction.Unpack, unpack.Action);
        Assert.Equal(rigBuilding, unpack.UnpackType);
        Assert.False(PackOffer.ForPackedRig(rules, rigBuilding, 10)!.Value.Enabled);
        Assert.Null(PackOffer.ForPackedRig(rules, -1, 0));

        Assert.Equal(CommandKind.Pack, PackOffer.PackCommand(0, EntityId.None).Kind);
        Assert.Equal(new CellCoord(3, 4), PackOffer.UnpackCommand(0, EntityId.None, new CellCoord(3, 4)).Cell);
        Assert.Equal(50, PackOffer.BarFill(500, 100));
        Assert.Equal(100, PackOffer.BarFill(5000, 100));
    }

    [Fact]
    public void Unpack_placement_only_issues_on_a_valid_site()
    {
        var p = new UnpackPlacement(new EntityId(1, 1), 0, 3, 3, c => c.X >= 5);
        Assert.Null(p.Click(0));   // no ghost yet
        p.Hover(new CellCoord(2, 2));
        Assert.False(p.Valid);
        Assert.Null(p.Click(0));
        p.Hover(new CellCoord(10, 10));
        Assert.Equal(new CellCoord(9, 9), p.TopLeft);   // centred on the cursor
        Command c = p.Click(0)!.Value;
        Assert.Equal(CommandKind.Pack, c.Kind);
        Assert.Equal(new CellCoord(9, 9), c.Cell);
    }

    [Fact]
    public void A_packed_rig_unpacks_through_the_placement_command_and_then_offers_pack()
    {
        using SkirmishGame g = SkirmishGame.Create(RulesDir, Of(SlotKind.Human, Faction.LeftBehind, SlotKind.Ai, Faction.Authority));
        Simulation sim = g.Sim;
        var host = new SimHost(sim);
        EntityId rig = PackedRigOf(sim, 0);
        PlayerView view = sim.ViewFor(0);
        int type = view.UnpackTypeOf(rig);
        BuildingType bt = sim.Setup.Rules.Buildings[type];
        var placement = new UnpackPlacement(rig, type, bt.FootprintW, bt.FootprintH, c => sim.ViewFor(0).CanUnpackAt(rig, c));

        CellCoord at = new SimHost(sim).Current.Units.First(u => u.Id == rig && u.Kind == UnitKind.Vehicle).Position.ToCell();
        bool found = false;
        for (int dy = -6; dy <= 6 && !found; dy++)
            for (int dx = -6; dx <= 6 && !found; dx++)
            {
                placement.Hover(new CellCoord(at.X + dx, at.Y + dy));
                found = placement.Valid;
            }

        Assert.True(found, "no valid unpack site near the rig");
        host.Issue(placement.Click(0)!.Value);
        host.StepTicks(900);

        bool hasBuilding = false;
        foreach (BuildingView b in host.Current.Buildings) hasBuilding |= !b.Ghost && b.Player == 0 && b.TypeIndex == type;
        Assert.True(hasBuilding, "the rig did not unpack");
        Assert.False(LeftBehindText.ShowUnpackHint(sim.HudFor(0), host.Current, 0));
        Assert.NotNull(PackOffer.ForBuilding(sim.Setup.Rules, type, 0));
    }

    // ---- wreck and storm layers ------------------------------------------------------------------

    [Fact]
    public void Wreck_opacity_fades_with_the_ticks_left()
    {
        Assert.Equal(WreckStormOverlay.WreckMaxAlpha, WreckStormOverlay.WreckAlpha(1000, 1000));
        Assert.Equal(WreckStormOverlay.WreckMaxAlpha, WreckStormOverlay.WreckAlpha(5, 0));
        byte half = WreckStormOverlay.WreckAlpha(500, 1000);
        byte low = WreckStormOverlay.WreckAlpha(10, 1000);
        Assert.InRange(half, WreckStormOverlay.WreckMinAlpha + 1, WreckStormOverlay.WreckMaxAlpha - 1);
        Assert.True(low < half);
        Assert.True(low >= WreckStormOverlay.WreckMinAlpha);
        Assert.Equal(0, WreckStormOverlay.WreckAlpha(0, 1000));
        Assert.Equal(WreckStormOverlay.Shards(7), WreckStormOverlay.Shards(7));   // same wreck, same look
    }

    [Fact]
    public void The_draw_lists_follow_a_scripted_sims_wrecks_and_storms()
    {
        using SkirmishGame g = SkirmishGame.Create(RulesDir, Of(SlotKind.Human, Faction.LeftBehind, SlotKind.Ai, Faction.Authority));
        Simulation sim = g.Sim;
        // script the state parts directly (test only)
        var salvage = sim.Salvage;
        salvage.CellX[3] = 10; salvage.CellY[3] = 12; salvage.Value[3] = 80; salvage.ValueStart[3] = 80; salvage.TicksLeft[3] = 100;
        var fx = sim.Effects;
        fx.StormX[2] = 1; fx.StormY[2] = 20; fx.StormRadius[2] = 3; fx.StormTicks[2] = 500; fx.StormOwner[2] = 1;
        int total = sim.Setup.Rules.Economy.WreckTicks;

        var wrecks = new List<WreckDraw>();
        WreckStormOverlay.PlanWrecks(sim.ViewFor(0), total, new List<WreckView>(), wrecks);
        WreckDraw w = Assert.Single(wrecks);
        Assert.Equal(new CellCoord(10, 12), w.Cell);
        Assert.Equal(WreckStormOverlay.WreckAlpha(100, total), w.Alpha);

        var storms = new List<StormDraw>();
        WreckStormOverlay.PlanStorms(sim.ViewFor(0), sim.Tick, 64, 64, new List<StormView>(), storms);
        StormDraw s = Assert.Single(storms);
        Assert.Equal(2, s.Slot);
        Assert.Equal(new CellCoord(0, 17), s.Min);    // x clipped to the map edge
        Assert.Equal(new CellCoord(4, 23), s.Max);    // Chebyshev square: centre +- radius
        Assert.True(s.Alpha > 0);

        fx.StormTicks[2] = 10;   // about to end: fades
        WreckStormOverlay.PlanStorms(sim.ViewFor(0), sim.Tick, 64, 64, new List<StormView>(), storms);
        Assert.True(storms[0].Alpha < s.Alpha);
        fx.StormTicks[2] = 0;
        WreckStormOverlay.PlanStorms(sim.ViewFor(0), sim.Tick, 64, 64, new List<StormView>(), storms);
        Assert.Empty(storms);
    }

    // ---- salvage right click ---------------------------------------------------------------------

    [Fact]
    public void Haulers_and_tinkers_salvage_but_soldiers_do_not_and_wrecks_are_found_by_cell()
    {
        SimRules rules = RulesLoader.Load(RulesDir).Sim;
        int hauler = SimRules.IndexOf(rules.Vehicles, "lb_hauler", v => v.Id);
        int tinker = SimRules.IndexOf(rules.Infantry, "lb_tinker", v => v.Id);
        int rig = SimRules.IndexOf(rules.Vehicles, "lb_mother_rig_packed", v => v.Id);
        Assert.True(SalvageOrders.CanSalvage(rules, ActorKind.Vehicle, hauler));
        Assert.True(SalvageOrders.CanSalvage(rules, ActorKind.Infantry, tinker));
        Assert.False(SalvageOrders.CanSalvage(rules, ActorKind.Vehicle, rig));
        Assert.False(SalvageOrders.CanSalvage(rules, ActorKind.Vehicle, -1));
        Assert.False(SalvageOrders.CanSalvage(rules, ActorKind.Building, 0));

        var list = new List<WreckView> { new(1, new CellCoord(4, 5), 10, 10) };
        Assert.True(SalvageOrders.WreckAt(list, new CellCoord(4, 5)));
        Assert.False(SalvageOrders.WreckAt(list, new CellCoord(5, 4)));
    }

    // ---- headless skirmish -----------------------------------------------------------------------

    [Fact]
    public void A_Left_Behind_ai_against_an_Authority_ai_runs_3000_ticks_without_crashing()
    {
        SkirmishSettings s = Of(SlotKind.Ai, Faction.LeftBehind, SlotKind.Ai, Faction.Authority);
        using SkirmishGame g = SkirmishGame.Create(RulesDir, s);
        var host = new SimHost(g.Sim) { Ai = g.Ai };
        host.StepTicks(3000);
        Assert.Equal(3000, g.Sim.Tick);
        Assert.Equal(Faction.LeftBehind, g.Sim.HudFor(0).Faction);
    }

    [Fact]
    public void A_human_Left_Behind_player_against_a_Left_Behind_ai_runs_without_crashing()
    {
        using SkirmishGame g = SkirmishGame.Create(RulesDir, Of(SlotKind.Human, Faction.LeftBehind, SlotKind.Ai, Faction.LeftBehind));
        var host = new SimHost(g.Sim) { Ai = g.Ai };
        host.StepTicks(1000);
        Assert.Equal(1000, g.Sim.Tick);
    }

    private static EntityId PackedRigOf(Simulation sim, byte player)
    {
        foreach (UnitView u in new SimHost(sim).Current.Units)
            if (u.Player == player && u.Kind == UnitKind.Vehicle && sim.ViewFor(player).IsPackedRig(u.Id)) return u.Id;
        return EntityId.None;
    }
}
