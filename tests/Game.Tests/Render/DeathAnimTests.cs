// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Render;

/// <summary>M10-14b: the soldier death animation book (timing, fallback) and the cause plumbing from the sim's death event.</summary>
public sealed class DeathAnimTests
{
    private static SpriteManifest Sheet(params string[] states)
    {
        var lines = new List<string> { "file = \"t.png\"", "frame_w = 32", "frame_h = 32", "origin_x = 16", "origin_y = 24" };
        int first = 0;
        foreach (string s in states)
        {
            lines.AddRange(new[] { "[[state]]", $"name = \"{s}\"", "facings = 1", "frames = 6", $"first = {first}", "ticks_per_frame = 3", "loop = false" });
            first += 6;
        }
        return SpriteManifest.Parse(string.Join("\n", lines), "inf", "p");
    }

    private static DeathRecord Death(int tick, DeathCause cause = DeathCause.Unknown, byte facing = 64) =>
        new(RemainKind.InfantryMark, 0, 0, new LeptonCoord(256, 256), default, 0, 0, facing, tick, cause);

    [Fact]
    public void Sheet_with_die_plays_it_for_frames_times_ticks_then_hands_the_death_back()
    {
        SpriteManifest m = Sheet("idle", "die");
        var book = new DeathAnimBook();
        Assert.True(book.Add(Death(100), m));
        DeathAnim a = book.Items[0];
        Assert.Equal("die", a.State);
        Assert.Equal(18, a.Duration);
        Assert.Equal(6, DeathAnimBook.Frame(m, a, 100));    // die.first = 6
        Assert.Equal(6, DeathAnimBook.Frame(m, a, 102));
        Assert.Equal(7, DeathAnimBook.Frame(m, a, 103));
        Assert.Equal(11, DeathAnimBook.Frame(m, a, 117));   // last frame
        Assert.Equal(-1, DeathAnimBook.Frame(m, a, 118));   // over
        Assert.Equal(-1, DeathAnimBook.Frame(m, a, 99));    // not started

        var done = new List<DeathRecord>();
        book.Release(117, done);
        Assert.Empty(done);
        Assert.Single(book.Items);
        book.Release(118, done);
        Assert.Equal(Death(100), Assert.Single(done));      // the mark is left now, exactly as without art
        Assert.Empty(book.Items);
    }

    [Fact]
    public void Sheet_without_any_die_state_adds_nothing()
    {
        var book = new DeathAnimBook();
        Assert.False(book.Add(Death(5, DeathCause.Burn), Sheet("idle")));
        Assert.Empty(book.Items);
    }

    [Fact]
    public void Cause_state_is_used_when_present_else_die()
    {
        var book = new DeathAnimBook();
        Assert.True(book.Add(Death(1, DeathCause.Rad), Sheet("idle", "die", "die_rad")));
        Assert.True(book.Add(Death(1, DeathCause.Burn), Sheet("idle", "die", "die_rad")));
        Assert.Equal("die_rad", book.Items[0].State);
        Assert.Equal("die", book.Items[1].State);
    }

    [Fact]
    public void Clock_going_back_drops_the_animation_without_a_mark()
    {
        var book = new DeathAnimBook();
        book.Add(Death(100), Sheet("die"));
        var done = new List<DeathRecord>();
        book.Release(50, done);
        Assert.Empty(done);
        Assert.Empty(book.Items);
    }

    [Fact]
    public void Death_event_warhead_maps_to_a_cause()
    {
        SimRules rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!).Sim;
        int Wh(string id) => Enumerable.Range(0, rules.Warheads.Count).First(i => rules.Warheads[i].Id == id);
        Assert.Equal(DeathCause.Burn, DeathStates.CauseOfEvent(Wh("lb_flame"), rules));
        Assert.Equal(DeathCause.Rad, DeathStates.CauseOfEvent(Wh("dir_rad_shell"), rules));
        Assert.Equal(DeathCause.Blast, DeathStates.CauseOfEvent(Wh("auth_he"), rules));
        Assert.Equal(DeathCause.Rad, DeathStates.CauseOfEvent(FxEvent.RadiationCause, rules));
        Assert.Equal(DeathCause.Unknown, DeathStates.CauseOfEvent(FxEvent.NoWarhead, rules));
        Assert.Equal(DeathCause.Unknown, DeathStates.CauseOfEvent(9999, rules));
        Assert.Equal(DeathCause.Unknown, DeathStates.CauseOfEvent(0, null));
    }

    [Fact]
    public void Death_watcher_fills_the_cause_from_the_event()
    {
        SimRules rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!).Sim;
        int flame = Enumerable.Range(0, rules.Warheads.Count).First(i => rules.Warheads[i].Id == "lb_flame");
        var land = new LandType[16 * 16];
        SimSnapshot Snap(int tick, IEnumerable<UnitView> units, IEnumerable<FxEvent> fx) =>
            new(tick, 16, 16, units.ToList(), land, 0, new List<BuildingView>(), -1, Array.Empty<byte>(), fx.ToList());
        var inf = new UnitView(new EntityId(3, 1), UnitKind.Infantry, 0, 0, new LeptonCoord(5 * 256, 5 * 256), 200, 10);
        var watcher = new DeathWatcher();
        var deaths = new List<DeathRecord>();
        watcher.Observe(Snap(10, new[] { inf }, Array.Empty<FxEvent>()), deaths, rules);
        watcher.Observe(Snap(11, Array.Empty<UnitView>(), new[] { new FxEvent(FxKind.UnitDestroyed, inf.Position, 1, flame) }), deaths, rules);
        DeathRecord d = Assert.Single(deaths);
        Assert.Equal(DeathCause.Burn, d.Cause);
        Assert.Equal(200, d.Facing);
    }
}
