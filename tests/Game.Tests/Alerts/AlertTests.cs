// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Audio;
using Rts.Game.Ui.Alerts;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Alerts;

/// <summary>M11-10d: merging, rate limit, ordering, fade, pings, the jump cursor, the layout and the deriver.</summary>
public sealed class AlertTests
{
    private const int L = SimConstants.LeptonsPerCell;
    private static LeptonCoord Cell(int x, int y) => new(x * L, y * L);
    private static GameAlert At(AlertKind k, int tick, int x = 5, int y = 5) => new(k, tick, Cell(x, y));

    [Fact]
    public void Newest_line_is_on_top_and_at_most_four_show()
    {
        var log = new AlertLog();
        log.Add(new GameAlert(AlertKind.LowPower, 0));
        log.Add(new GameAlert(AlertKind.ConstructionComplete, 1));
        log.Add(At(AlertKind.BuildingLost, 2));
        log.Add(At(AlertKind.UnitLost, 3));
        log.Add(At(AlertKind.BaseUnderAttack, 4));
        IReadOnlyList<AlertLine> lines = log.Lines(5);
        Assert.Equal(AlertLog.MaxLines, lines.Count);
        Assert.Equal("Base under attack", lines[0].Text);
        Assert.Equal("Unit lost", lines[1].Text);
    }

    [Fact]
    public void A_repeat_in_the_same_area_merges_into_one_line()
    {
        var log = new AlertLog();
        for (int t = 0; t < 60; t += 5) log.Add(At(AlertKind.UnitUnderAttack, t, 5 + (t % 3), 5));
        Assert.Single(log.Lines(60));
    }

    [Fact]
    public void Under_attack_counts_at_most_once_per_ten_seconds_per_area()
    {
        var log = new AlertLog();
        Assert.True(log.Add(At(AlertKind.BaseUnderAttack, 0)));
        Assert.False(log.Add(At(AlertKind.BaseUnderAttack, 60)));             // merged into the live line, not counted
        Assert.Equal("Base under attack", log.Lines(60)[0].Text);
        Assert.True(log.Add(At(AlertKind.BaseUnderAttack, 200)));             // ten seconds on
        Assert.Equal("Base under attack x2", log.Lines(200)[0].Text);
    }

    [Fact]
    public void After_a_line_fades_a_repeat_inside_the_window_is_dropped_not_shown_again()
    {
        var log = new AlertLog();
        log.Add(At(AlertKind.UnitUnderAttack, 0));
        Assert.Empty(log.Lines(AlertLog.LifeTicks));
        Assert.False(log.Add(At(AlertKind.UnitUnderAttack, AlertLog.LifeTicks + 10)));
        Assert.Empty(log.Lines(AlertLog.LifeTicks + 10));
        Assert.True(log.Add(At(AlertKind.UnitUnderAttack, 10 * AlertInfo.TicksPerSecond + 1)));
    }

    [Fact]
    public void Different_areas_are_different_alerts()
    {
        var log = new AlertLog();
        Assert.True(log.Add(At(AlertKind.BaseUnderAttack, 0, 5, 5)));
        Assert.True(log.Add(At(AlertKind.BaseUnderAttack, 1, 60, 5)));
        Assert.Equal(2, log.Lines(2).Count);
    }

    [Fact]
    public void Lines_fade_out_over_the_last_ticks_and_then_go()
    {
        var log = new AlertLog();
        log.Add(new GameAlert(AlertKind.LowPower, 0));
        Assert.Equal(255, log.Lines(10)[0].Alpha);
        byte mid = log.Lines(AlertLog.LifeTicks - AlertLog.FadeTicks / 2)[0].Alpha;
        Assert.InRange((int)mid, 1, 254);
        Assert.Empty(log.Lines(AlertLog.LifeTicks));
    }

    [Fact]
    public void Low_power_shows_no_count_and_stays_while_it_is_reported()
    {
        var log = new AlertLog();
        for (int t = 0; t < 1000; t++) log.Add(new GameAlert(AlertKind.LowPower, t));
        Assert.Equal("Low power", log.Lines(1000)[0].Text);
    }

    [Fact]
    public void Pings_exist_for_alerts_with_a_place_and_are_red_for_danger()
    {
        var log = new AlertLog();
        log.Add(At(AlertKind.BaseUnderAttack, 0));
        log.Add(At(AlertKind.UnitReady, 0, 30, 30));
        log.Add(new GameAlert(AlertKind.LowPower, 0));
        IReadOnlyList<AlertPing> pings = log.Pings(10);
        Assert.Equal(2, pings.Count);
        Assert.True(pings[0].Danger);
        Assert.False(pings[1].Danger);
        Assert.Empty(log.Pings(AlertLog.PingTicks));
    }

    [Fact]
    public void Jump_list_holds_the_last_places_newest_first_and_skips_routine_alerts()
    {
        var log = new AlertLog();
        log.Add(At(AlertKind.UnitReady, 0, 1, 1));
        log.Add(At(AlertKind.BaseUnderAttack, 1, 5, 5));
        log.Add(At(AlertKind.UnitLost, 2, 50, 50));
        IReadOnlyList<JumpTarget> t = log.JumpTargets();
        Assert.Equal(2, t.Count);
        Assert.Equal(AlertKind.UnitLost, t[0].Kind);
        for (int i = 0; i < 10; i++) log.Add(At(AlertKind.BuildingLost, 10 + i, 20 * (i + 1) % 200, 100));
        Assert.True(log.JumpTargets().Count <= AlertLog.JumpMax);
    }

    [Fact]
    public void The_key_goes_to_the_newest_then_steps_back_and_wraps()
    {
        var log = new AlertLog();
        log.Add(At(AlertKind.BaseUnderAttack, 0, 5, 5));
        log.Add(At(AlertKind.UnitLost, 1, 50, 50));
        log.Add(At(AlertKind.BuildingLost, 2, 90, 90));
        var cursor = new AlertJumpCursor();
        Assert.Equal(Cell(90, 90), cursor.Press(log, 10));
        Assert.Equal(Cell(50, 50), cursor.Press(log, 12));
        Assert.Equal(Cell(5, 5), cursor.Press(log, 14));
        Assert.Equal(Cell(90, 90), cursor.Press(log, 16));
    }

    [Fact]
    public void A_new_alert_or_a_long_pause_starts_the_key_over_at_the_newest()
    {
        var log = new AlertLog();
        log.Add(At(AlertKind.BaseUnderAttack, 0, 5, 5));
        log.Add(At(AlertKind.UnitLost, 1, 50, 50));
        var cursor = new AlertJumpCursor();
        cursor.Press(log, 10);
        Assert.Equal(Cell(5, 5), cursor.Press(log, 12));
        log.Add(At(AlertKind.BuildingLost, 13, 90, 90));
        Assert.Equal(Cell(90, 90), cursor.Press(log, 14));
        Assert.Equal(Cell(90, 90), cursor.Press(log, 14 + AlertJumpCursor.ResetTicks + 1));
    }

    [Fact]
    public void The_key_does_nothing_with_no_alert()
    {
        Assert.Null(new AlertJumpCursor().Press(new AlertLog(), 5));
    }

    [Fact]
    public void The_stack_sits_top_right_and_drops_below_a_panel_it_would_overlap()
    {
        var view = new Rectangle(0, 0, 1000, 700);
        Rectangle free = AlertLayout.Place(view, new Point(200, 100), 8, 8, Array.Empty<Rectangle>());
        Assert.Equal(new Rectangle(792, 8, 200, 100), free);
        var bar = new Rectangle(500, 8, 480, 40);                      // an objective bar reaching the right side
        Rectangle moved = AlertLayout.Place(view, new Point(200, 100), 8, 8, new[] { bar, Rectangle.Empty });
        Assert.Equal(bar.Bottom + 8, moved.Y);
        Assert.False(moved.Intersects(bar));
        var far = new Rectangle(8, 8, 300, 40);                        // a left panel does not push it
        Assert.Equal(8, AlertLayout.Place(view, new Point(200, 100), 8, 8, new[] { far }).Y);
    }

    // ---- deriver ----

    private static SimSnapshot Snap(int tick, UnitView[]? units = null, BuildingView[]? buildings = null, FxEvent[]? fx = null, SuperweaponView[]? sw = null) =>
        new(tick, 64, 64, units ?? Array.Empty<UnitView>(), Array.Empty<Rts.Sim.Rules.LandType>(), 0UL, buildings ?? Array.Empty<BuildingView>(), 0,
            Array.Empty<byte>(), fx) { Superweapons = sw ?? Array.Empty<SuperweaponView>() };

    private static List<GameAlert> Derive(SimSnapshot a, SimSnapshot b, params AudioEventKind[] events)
    {
        var alerts = new List<GameAlert>();
        new AlertDeriver(u => u.TypeIndex == 9).Derive(new AudioFrame(a, b, 0), events.Select(e => new AudioEvent(e)).ToList(), alerts);
        return alerts;
    }

    private static UnitView Unit(ushort id, int type, int hp, byte player = 0) =>
        new(new EntityId(id, 0), UnitKind.Vehicle, player, type, Cell(10, 10), 0, hp);

    private static BuildingView Building(ushort id, int hp) => new(new EntityId(id, 0), 0, 1, new CellCoord(20, 20), 2, 2, hp, false);

    [Fact]
    public void A_hit_building_is_a_base_alert_at_its_centre()
    {
        var alerts = Derive(Snap(1, buildings: new[] { Building(1, 100) }), Snap(2, buildings: new[] { Building(1, 80) }), AudioEventKind.UnderAttack);
        GameAlert a = Assert.Single(alerts);
        Assert.Equal(AlertKind.BaseUnderAttack, a.Kind);
        Assert.Equal(Cell(21, 21), a.Pos);
    }

    [Fact]
    public void A_hit_harvester_and_a_hit_tank_are_told_apart()
    {
        var alerts = Derive(Snap(1, new[] { Unit(1, 9, 100), Unit(2, 3, 100) }), Snap(2, new[] { Unit(1, 9, 90), Unit(2, 3, 90) }), AudioEventKind.UnderAttack);
        Assert.Contains(alerts, a => a.Kind == AlertKind.HarvesterUnderAttack);
        Assert.Contains(alerts, a => a.Kind == AlertKind.UnitUnderAttack);
    }

    [Fact]
    public void Nothing_is_raised_without_the_audio_under_attack_event_or_for_other_players()
    {
        Assert.Empty(Derive(Snap(1, new[] { Unit(1, 3, 100) }), Snap(2, new[] { Unit(1, 3, 90) })));
        Assert.Empty(Derive(Snap(1, new[] { Unit(1, 3, 100, 1) }), Snap(2, new[] { Unit(1, 3, 90, 1) }), AudioEventKind.UnderAttack));
    }

    [Fact]
    public void A_destroyed_own_building_and_unit_are_reported_lost()
    {
        var b = Building(1, 10);
        var fx = new[] { new FxEvent(FxKind.BuildingDestroyed, Cell(21, 21), 2), new FxEvent(FxKind.UnitDestroyed, Cell(10, 10), 1) };
        var alerts = Derive(Snap(1, new[] { Unit(1, 3, 10) }, new[] { b }), Snap(2, fx: fx), AudioEventKind.UnderAttack);
        Assert.Contains(alerts, a => a.Kind == AlertKind.BuildingLost && a.Pos == Cell(21, 21));
        Assert.Contains(alerts, a => a.Kind == AlertKind.UnitLost && a.Pos == Cell(10, 10));
        Assert.Contains(alerts, a => a.Kind == AlertKind.BaseUnderAttack);
    }

    [Fact]
    public void A_sold_building_is_not_a_loss()
    {
        Assert.Empty(Derive(Snap(1, buildings: new[] { Building(1, 100) }), Snap(2), AudioEventKind.Sold));
    }

    [Fact]
    public void Hud_sound_events_become_alerts_without_a_place()
    {
        var alerts = Derive(Snap(1), Snap(2), AudioEventKind.LowPower, AudioEventKind.ConstructionComplete);
        Assert.Contains(alerts, a => a.Kind == AlertKind.LowPower && a.Pos is null);
        Assert.Contains(alerts, a => a.Kind == AlertKind.ConstructionComplete && a.Pos is null);
    }

    [Fact]
    public void A_new_own_unit_is_unit_ready_at_its_place()
    {
        var alerts = Derive(Snap(1), Snap(2, new[] { Unit(5, 3, 100) }), AudioEventKind.UnitReady);
        Assert.Equal(Cell(10, 10), Assert.Single(alerts).Pos);
    }

    [Fact]
    public void Superweapon_ready_and_launched_are_derived_for_the_local_player()
    {
        var charging = new SuperweaponView(new EntityId(1, 0), 1, "x", 0, 10, false, false, 5) { Owner = 0 };
        var ready = charging with { Ready = true, Charge = 10 };
        var b = new[] { Building(1, 100) };
        Assert.Contains(Derive(Snap(1, buildings: b, sw: new[] { charging }), Snap(2, buildings: b, sw: new[] { ready })), a => a.Kind == AlertKind.SuperweaponReady);
        var fx = new[] { new FxEvent(FxKind.Superweapon, Cell(40, 40), 5) };
        Assert.Contains(Derive(Snap(1, buildings: b, sw: new[] { ready }), Snap(2, buildings: b, fx: fx, sw: new[] { charging })), a => a.Kind == AlertKind.SuperweaponLaunched && a.Pos == Cell(40, 40));
    }
}
