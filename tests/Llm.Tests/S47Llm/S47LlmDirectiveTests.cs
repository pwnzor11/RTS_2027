// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;
using Rts.Sim;
using Rts.Sim.Core;

namespace Llm.Tests.S47Llm;

/// <summary>S47 (M16-A-08): the counter-building window (parser, hold, commander, random control) and the richer summary (order status,
/// units lost by type).</summary>
public class S47LlmDirectiveTests
{
    private static CommanderReply P(string json) => ReplyParser.Parse(json);
    private static string Key(CommanderReply r) => r.Directive + "|" + string.Join(";", r.OrderList);
    private static DirectiveDraft D(string mix, string extra = "") =>
        P("{\"directive\":{\"posture\":\"balanced\",\"focus\":\"army\",\"mix\":\"" + mix + "\"" + extra + "}}").Directive!.Value;

    // ---- parser ----------------------------------------------------------------------------------

    [Fact]
    public void Counter_minutes_with_a_counter_mix_is_kept_and_makes_the_mix_strong()
    {
        DirectiveDraft d = D("antiarmour", ",\"counter_minutes\":3");
        Assert.Equal(3, d.CounterMinutes);
        Assert.True(d.StrongMix);
        Assert.Equal(DirectiveCodec.MixAntiArmour, d.Mix);
    }

    [Theory]
    [InlineData("99", 10)]
    [InlineData("\"4\"", 4)]
    [InlineData("-5", 0)]
    [InlineData("2.9", 2)]
    public void Counter_minutes_is_clamped_and_read_loosely(string raw, int expected) =>
        Assert.Equal(expected, D("antiair", ",\"counter_minutes\":" + raw).CounterMinutes);

    [Theory]
    [InlineData("auto")]
    [InlineData("vehicles")]
    [InlineData("antistructure")]
    public void Counter_minutes_is_dropped_for_a_mix_that_is_no_counter(string mix)
    {
        DirectiveDraft d = D(mix, ",\"counter_minutes\":5");
        Assert.Equal(0, d.CounterMinutes);
        Assert.False(d.StrongMix);
    }

    [Fact]
    public void Junk_counter_minutes_is_zero_and_the_rest_of_the_directive_survives()
    {
        DirectiveDraft d = D("antiinfantry", ",\"counter_minutes\":\"soon\"");
        Assert.Equal(0, d.CounterMinutes);
        Assert.Equal(DirectiveCodec.MixAntiInfantry, d.Mix);
        Assert.False(d.StrongMix);
    }

    [Fact]
    public void The_prompt_names_the_new_field_and_the_order_states()
    {
        string text = CommanderPrompt.System(CommanderPersona.Plain("Authority"), "Authority");
        Assert.Contains("counter_minutes", CommanderPrompt.Schema);
        Assert.Contains("counter_minutes", text);
        Assert.Contains("YOUR ORDERS", text);
        Assert.Contains("FAILED", text);
    }

    // ---- hold ------------------------------------------------------------------------------------

    private static DirectiveDraft Plain(byte mix = 0, byte minutes = 0) =>
        new(1, 2, 255, mix, false, -1, -1, false, minutes);

    [Fact]
    public void Hold_keeps_the_counter_mix_in_auto_directives_until_the_window_ends()
    {
        var h = new CounterHold();
        DirectiveDraft first = h.Apply(Plain(DirectiveCodec.MixAntiArmour, 2), 100);
        Assert.True(first.StrongMix);
        Assert.Equal(100 + 2 * CounterHold.TicksPerMinute, h.Until);

        DirectiveDraft later = h.Apply(Plain(), 100 + CounterHold.TicksPerMinute);
        Assert.Equal(DirectiveCodec.MixAntiArmour, later.Mix);
        Assert.True(later.StrongMix);

        DirectiveDraft after = h.Apply(Plain(), 100 + 2 * CounterHold.TicksPerMinute);
        Assert.Equal(DirectiveCodec.MixAuto, after.Mix);
        Assert.False(after.StrongMix);
        Assert.Equal(-1, h.Until);
    }

    [Fact]
    public void Another_mix_from_the_model_ends_the_hold()
    {
        var h = new CounterHold();
        h.Apply(Plain(DirectiveCodec.MixAntiAir, 5), 0);
        DirectiveDraft d = h.Apply(Plain(DirectiveCodec.MixVehicles), 10);
        Assert.Equal(DirectiveCodec.MixVehicles, d.Mix);
        Assert.False(h.Active(11));
        Assert.Equal(DirectiveCodec.MixAuto, h.Apply(Plain(), 12).Mix);
    }

    [Fact]
    public void Expire_gives_the_last_directive_back_with_auto_mix_once()
    {
        var h = new CounterHold();
        DirectiveDraft sent = h.Apply(Plain(DirectiveCodec.MixAntiInfantry, 1), 0);
        Assert.Null(h.Expire(sent, CounterHold.TicksPerMinute - 1));
        DirectiveDraft? back = h.Expire(sent, CounterHold.TicksPerMinute);
        Assert.NotNull(back);
        Assert.Equal(DirectiveCodec.MixAuto, back!.Value.Mix);
        Assert.False(back.Value.StrongMix);
        Assert.Equal(sent.Posture, back.Value.Posture);
        Assert.Null(h.Expire(sent, CounterHold.TicksPerMinute + 5));
    }

    [Fact]
    public void A_hold_that_ran_out_before_the_next_reply_does_not_undo_that_reply()
    {
        var h = new CounterHold();
        h.Apply(Plain(DirectiveCodec.MixAntiAir, 1), 0);
        DirectiveDraft d = h.Apply(Plain(DirectiveCodec.MixAntiAir), CounterHold.TicksPerMinute + 1);   // the model chose anti-air again, no hold
        Assert.False(d.StrongMix);
        Assert.Null(h.Expire(d, CounterHold.TicksPerMinute + 2));
    }

    // ---- commander -------------------------------------------------------------------------------

    private const string Counter2 = "{\"directive\":{\"posture\":\"balanced\",\"focus\":\"army\",\"mix\":\"antiarmour\",\"counter_minutes\":1}}";
    private const string AutoMix = "{\"directive\":{\"posture\":\"aggressive\",\"focus\":\"army\",\"mix\":\"auto\"}}";

    [Fact]
    public async Task Commander_holds_the_counter_through_auto_replies_then_returns_to_auto()
    {
        Simulation sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        var fake = new FakeChatProvider();
        foreach (string r in new[] { Counter2, AutoMix, AutoMix, AutoMix, AutoMix }) fake.EnqueueText(r);
        using var c = new Commander(0, fake, CommanderPersona.Plain("Authority"), "Authority", 0, false, new CommanderChatLog());
        var issued = new List<(int Tick, int Arg)>();
        while (sim.Tick < 3100)
        {
            SummaryRig.Run(sim, 1);
            if (c.Poll(sim.ViewFor(0), out int arg, out _)) issued.Add((sim.Tick, arg));
            if (c.LastCall is { } t) await t.WaitAsync(TimeSpan.FromSeconds(10));
        }
        byte Mix(int i) => DirectiveCodec.Unpack(issued[i].Arg).Mix;
        // reply 1 at ~601 starts a 1-minute hold (to ~1801); the reply at ~1201 says auto and keeps it; the reply at ~1801 is outside the window
        Assert.Equal(DirectiveCodec.MixAntiArmour, Mix(0));
        Assert.True(DirectiveCodec.IsStrong(issued[0].Arg));
        Assert.Equal(DirectiveCodec.MixAntiArmour, Mix(1));
        Assert.True(DirectiveCodec.IsStrong(issued[1].Arg));
        Assert.Equal(DirectiveCodec.MixAuto, Mix(2));
        Assert.False(DirectiveCodec.IsStrong(issued[2].Arg));
        Assert.All(issued.Skip(2), i => Assert.Equal(DirectiveCodec.MixAuto, DirectiveCodec.Unpack(i.Arg).Mix));
    }

    [Fact]
    public async Task Commander_sends_a_plain_directive_when_the_window_ends_with_no_new_reply()
    {
        Simulation sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        var fake = new FakeChatProvider();
        fake.EnqueueText(Counter2);
        fake.Enqueue(new ChatResult(false, "", "no more", 0, 0));   // the next call fails: the commander goes offline
        using var c = new Commander(0, fake, CommanderPersona.Plain("Authority"), "Authority", 0, false, new CommanderChatLog());
        var issued = new List<(int Tick, int Arg)>();
        while (sim.Tick < 2000)
        {
            SummaryRig.Run(sim, 1);
            if (c.Poll(sim.ViewFor(0), out int arg, out _)) issued.Add((sim.Tick, arg));
            if (c.LastCall is { } t) await t.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(2, issued.Count);
        Assert.Equal(DirectiveCodec.MixAntiArmour, DirectiveCodec.Unpack(issued[0].Arg).Mix);
        Assert.InRange(issued[1].Tick, 601 + CounterHold.TicksPerMinute, 603 + CounterHold.TicksPerMinute);
        Assert.Equal(DirectiveCodec.MixAuto, DirectiveCodec.Unpack(issued[1].Arg).Mix);
        Assert.False(DirectiveCodec.IsStrong(issued[1].Arg));
    }

    [Fact]
    public void Commander_without_a_provider_issues_nothing_at_all()
    {
        Simulation sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        using var c = new Commander(0, null, CommanderPersona.Plain("Authority"), "Authority", 0, false, new CommanderChatLog());
        for (int i = 0; i < 2500; i++)
        {
            SummaryRig.Run(sim, 1);
            Assert.False(c.Poll(sim.ViewFor(0), out _, out _));
        }
        Assert.Empty(c.TakeOrders());
        Assert.False(c.Online);
    }

    // ---- random control --------------------------------------------------------------------------

    private static List<(int Tick, int Arg)> RandomRun(RandomCommanderSource src, int ticks)
    {
        Simulation sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        var list = new List<(int, int)>();
        for (int i = 0; i < ticks; i++)
        {
            SummaryRig.Run(sim, 1);
            if (src.Poll(sim.ViewFor(0), out int arg, out _)) list.Add((sim.Tick, arg));
        }
        return list;
    }

    [Fact]
    public void Random_counters_are_off_by_default_and_leave_the_old_stream_alone()
    {
        var off = RandomRun(new RandomCommanderSource(7, 64), 9000);
        var offAgain = RandomRun(new RandomCommanderSource(7, 64) { Counters = false }, 9000);
        Assert.Equal(off, offAgain);

        // with counters on, the decisions come at the same ticks and draw the same main stream
        var on = RandomRun(new RandomCommanderSource(7, 64) { Counters = true }, 9000);
        var a = new RandomCommanderSource(7, 64);
        var b = new RandomCommanderSource(7, 64) { Counters = true };
        for (int i = 0; i < 25; i++) Assert.Equal(Key(a.Next()), Key(b.Next()));
        Assert.True(on.Count >= off.Count);
        Assert.Equal(off.Select(x => x.Tick).Take(14), on.Select(x => x.Tick).Where(t => off.Any(o => o.Tick == t)).Take(14));
    }

    [Fact]
    public void Random_counters_send_strong_counter_mixes_and_a_plain_directive_when_they_end()
    {
        var on = RandomRun(new RandomCommanderSource(11, 64) { Counters = true }, 30 * 600);
        var off = RandomRun(new RandomCommanderSource(11, 64), 30 * 600);
        Assert.NotEqual(off, on);
        Assert.Contains(on, x => CounterHold.IsCounter(DirectiveCodec.Unpack(x.Arg).Mix) && DirectiveCodec.IsStrong(x.Arg));
        Assert.Equal(on, RandomRun(new RandomCommanderSource(11, 64) { Counters = true }, 30 * 600));   // one seed, one game
    }

    // ---- summary ---------------------------------------------------------------------------------

    /// <summary>The sim drops a cell order for a cell the player has never seen: a unit stands there for a few ticks, then dies.</summary>
    private static void Explore(Simulation sim, CellCoord cell)
    {
        EntityId scout = SummaryRig.LateInfantry(sim, 0, SummaryRig.Infantry("dir_conscript"), cell);
        SummaryRig.Run(sim, 3);
        SummaryRig.Kill(sim, 0, scout, ActorKind.Infantry);
        SummaryRig.Run(sim, 2);
    }

    private static void Order(Simulation sim, int slot, CommanderOrderKind kind, CellCoord cell, int amount, int due) =>
        sim.Step(new[] { CommanderOrderCodec.ToCommand(sim.Tick, 0, (ushort)(900 + slot), kind, slot, cell, amount, due) });

    [Fact]
    public void No_orders_means_no_orders_block()
    {
        Simulation sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        var h = new SummaryHistory();
        h.Observe(sim.ViewFor(0));
        Assert.Empty(h.Orders);
        Assert.DoesNotContain("YOUR ORDERS", BattlefieldSummary.Build(sim.ViewFor(0), h));
    }

    [Fact]
    public void A_timed_attack_goes_from_pending_to_waiting_to_done()
    {
        Simulation sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        sim.SpawnVehicle(0, SummaryRig.Vehicle("auth_tank"), new CellCoord(10, 10));
        var cell = new CellCoord(30, 30);
        int tank = SummaryRig.Rules.Sim.Vehicles.ToArray()[SummaryRig.Vehicle("auth_tank")].Cost;
        SummaryRig.Run(sim, 5);
        Explore(sim, cell);
        Order(sim, 1, CommanderOrderKind.AttackAt, cell, tank * 2, 1200);
        var h = new SummaryHistory();

        h.Observe(sim.ViewFor(0));
        Assert.Equal(OrderState.Pending, Assert.Single(h.Orders).State);
        string early = BattlefieldSummary.Build(sim.ViewFor(0), h);
        Assert.Contains("YOUR ORDERS", early);
        Assert.Contains("slot 1 attack cell 30,30: PENDING", early);

        SummaryRig.Run(sim, 1300);
        h.Observe(sim.ViewFor(0));
        Assert.Equal(OrderState.Waiting, h.Orders[0].State);
        Assert.Contains("short of", h.Orders[0].Detail);

        SummaryRig.LateVehicle(sim, 0, SummaryRig.Vehicle("auth_tank"), new CellCoord(11, 10));
        SummaryRig.Run(sim, 2);
        h.Observe(sim.ViewFor(0));
        Assert.Equal(OrderState.Marching, h.Orders[0].State);

        SummaryRig.LateVehicle(sim, 0, SummaryRig.Vehicle("auth_tank"), new CellCoord(31, 30));   // a unit now stands at the cell
        SummaryRig.Run(sim, 2);
        h.Observe(sim.ViewFor(0));
        Assert.Equal(OrderState.Done, h.Orders[0].State);

        // done stays done when the unit leaves
        SummaryRig.Run(sim, 10);
        h.Observe(sim.ViewFor(0));
        Assert.Equal(OrderState.Done, h.Orders[0].State);
    }

    [Fact]
    public void An_attack_that_loses_most_of_its_army_is_reported_failed_and_a_new_issue_resets_it()
    {
        Simulation sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        EntityId[] tanks = new EntityId[3];
        for (int i = 0; i < 3; i++) tanks[i] = SummaryRig.LateVehicle(sim, 0, SummaryRig.Vehicle("auth_tank"), new CellCoord(10 + i, 10));
        var cell = new CellCoord(40, 40);
        SummaryRig.Run(sim, 3);
        Explore(sim, cell);
        Order(sim, 0, CommanderOrderKind.AttackAt, cell, 1, 0);
        var h = new SummaryHistory();
        SummaryRig.Run(sim, 5);
        h.Observe(sim.ViewFor(0));
        Assert.Equal(OrderState.Marching, h.Orders[0].State);

        SummaryRig.Kill(sim, 0, tanks[0], ActorKind.Vehicle);
        SummaryRig.Kill(sim, 0, tanks[1], ActorKind.Vehicle);
        SummaryRig.Kill(sim, 0, tanks[2], ActorKind.Vehicle);
        SummaryRig.LateVehicle(sim, 0, SummaryRig.Vehicle("auth_tank"), new CellCoord(12, 12));
        SummaryRig.Run(sim, 2);
        h.Observe(sim.ViewFor(0));
        Assert.Equal(OrderState.Failed, h.Orders[0].State);
        Assert.Contains("FAILED", BattlefieldSummary.Build(sim.ViewFor(0), h));

        Order(sim, 0, CommanderOrderKind.AttackAt, cell, 1, 0);   // issued again: a new IssuedTick
        h.Observe(sim.ViewFor(0));
        Assert.NotEqual(OrderState.Failed, h.Orders[0].State);
    }

    [Fact]
    public void Hold_tech_and_reserve_report_their_state()
    {
        Simulation sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        sim.SpawnVehicle(0, SummaryRig.Vehicle("auth_tank"), new CellCoord(20, 20));
        sim.SpawnVehicle(0, SummaryRig.Vehicle("auth_tank"), new CellCoord(40, 40));
        SummaryRig.Run(sim, 5);
        Order(sim, 0, CommanderOrderKind.HoldCell, new CellCoord(21, 20), 0, 0);
        Order(sim, 1, CommanderOrderKind.TechTo, Directive.NoCell, 5, 0);
        Order(sim, 2, CommanderOrderKind.Reserve, Directive.NoCell, 700, 0);
        var h = new SummaryHistory();
        SummaryRig.Run(sim, 3);
        h.Observe(sim.ViewFor(0));
        Assert.Equal(3, h.Orders.Count);
        Assert.Equal(OrderState.Active, h.Orders[0].State);
        Assert.Contains("1 of 2 army units are in place", h.Orders[0].Detail);
        Assert.Equal(OrderState.Active, h.Orders[1].State);
        Assert.Contains("of 5", h.Orders[1].Detail);
        Assert.Contains("keeping 700cr", h.Orders[2].Detail);
    }

    [Fact]
    public void Fights_name_the_units_lost_and_the_net_value()
    {
        Simulation sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        EntityId t1 = SummaryRig.LateVehicle(sim, 0, SummaryRig.Vehicle("auth_tank"), new CellCoord(14, 8));
        EntityId t2 = SummaryRig.LateVehicle(sim, 0, SummaryRig.Vehicle("auth_tank"), new CellCoord(15, 8));
        SummaryRig.Run(sim, 20);
        var h = new SummaryHistory();
        h.Observe(sim.ViewFor(0));
        SummaryRig.Kill(sim, 0, t1, ActorKind.Vehicle);
        SummaryRig.Kill(sim, 0, t2, ActorKind.Vehicle);
        h.Observe(sim.ViewFor(0));
        FightLine(h, out string line);
        Assert.Contains("(2xauth_tank)", line);
        Assert.Contains("net -", line);
        Assert.Equal(("auth_tank", 2), (SummaryRig.Rules.Sim.Vehicles.ToArray()[h.Fights[0].LostUnits![0].Type].Id, h.Fights[0].LostUnits![0].Count));
    }

    private static void FightLine(SummaryHistory h, out string line)
    {
        Simulation sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        string all = BattlefieldSummary.Build(sim.ViewFor(0), h);
        line = all.Split('\n').First(l => l.Contains("you lost"));
    }
}
