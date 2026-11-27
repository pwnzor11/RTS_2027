// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.S24;
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Llm;
using Rts.Sim.Core;
using Xunit.Abstractions;

namespace AI.Tests.S47Llm;

/// <summary>S47 (M16-A-08): scripted games on the S24 order rig where the slot-0 AI is led by a real <see cref="Commander"/> whose provider is a
/// <see cref="FakeChatProvider"/> (no network). Replies land at tick ~601; assertions are margins, as in S24.</summary>
public class S47LlmGameTests(ITestOutputHelper log)
{
    /// <summary>The commander's orders become commands exactly as the game's CommanderSet does it.</summary>
    private sealed class CommanderOrders(Commander c) : IOrderSource
    {
        public void TakeOrderCommands(int execTick, byte player, ref ushort sequence, List<Command> outCommands)
        {
            IReadOnlyList<OrderDraft> orders = c.TakeOrders();
            if (orders.Count > 0) new CommanderReply(null, null, orders).AddOrderCommands(outCommands, execTick, player, ref sequence);
        }
    }

    private static Commander Attach(OrderRig rig, IChatProvider? provider)
    {
        var c = new Commander(0, provider, CommanderPersona.Plain("Authority"), "Authority", 0, false, new CommanderChatLog());
        rig.Driver.SetDirectiveSource(0, c);
        rig.Driver.SetOrderSource(0, new CommanderOrders(c));
        return c;
    }

    /// <summary>Runs <paramref name="ticks"/> and waits for each request when it starts, so a reply never goes stale (the pool thread is not part of the game).</summary>
    private static void Run(OrderRig rig, Commander c, int ticks, Action<OrderRig>? each = null)
    {
        Task? waited = null;
        rig.Run(ticks, r =>
        {
            if (c.LastCall is { } t && !ReferenceEquals(t, waited)) { t.Wait(TimeSpan.FromSeconds(30)); waited = t; }
            each?.Invoke(r);
        });
    }

    private static int Nearest(OrderRig rig, CellCoord cell)
    {
        var own = new List<Rts.Sim.View.ViewActor>();
        rig.Sim.ViewFor(0).CopyOwn(own);
        int best = int.MaxValue;
        foreach (var a in own)
        {
            if (!a.IsMobile || TestWorld.Rules.Sim.Vehicles.Count == 0) continue;
            if (a.Kind == ActorKind.Vehicle && TestWorld.Rules.Sim.Vehicles[a.TypeIndex].HarvestCapacity > 0) continue;
            best = Math.Min(best, Math.Max(Math.Abs(a.Cell.X - cell.X), Math.Abs(a.Cell.Y - cell.Y)));
        }
        return best;
    }

    private static string Reply(string directive, string orders = "") =>
        "{\"directive\":{" + directive + "}" + (orders.Length > 0 ? ",\"orders\":[" + orders + "]" : "") + "}";

    [Fact]
    public void ATimedAttackFromTheCommanderLaunchesAtItsMinuteAndTheArmyMovesToTheCell()
    {
        var rig = new OrderRig();
        CellCoord cell = rig.NearYard(10);
        const int minute = 6;
        int due = minute * 60 * DirectiveCommands.TicksPerSecond;
        var fake = new FakeChatProvider();
        fake.EnqueueText(Reply("\"posture\":\"balanced\",\"focus\":\"army\"",
            $"{{\"slot\":0,\"kind\":\"attack\",\"cell\":[{cell.X},{cell.Y}],\"amount\":1,\"minute\":{minute}}}"));
        for (int i = 0; i < 20; i++) fake.EnqueueText("{\"say\":\"hold\"}");   // later calls change nothing
        using Commander c = Attach(rig, fake);

        int first = -1, wavesAtDue = -1, nearAtDue = -1;   // (the cell has to be one the player has seen, so it is near the yard: the army may already stand around it)
        Run(rig, c, due + 4500, r =>
        {
            if (r.Sim.Tick == due - 1) { wavesAtDue = r.Ai.Waves; nearAtDue = Nearest(r, cell); }
            if (first < 0 && r.Ai.Waves > 0) first = r.Sim.Tick;
        });
        int nearLater = Nearest(rig, cell);
        log.WriteLine($"due {due}, first wave {first}, waves at due {wavesAtDue}, nearest at due {nearAtDue}, nearest later {nearLater}");
        Assert.Equal(CommanderOrderKind.AttackAt, rig.Sim.CommanderOrdersOf(0)[0].Kind);
        Assert.Equal(0, wavesAtDue);
        Assert.InRange(first, due - 2, due + 300);
        Assert.True(nearLater <= 6, $"army units stand at the ordered cell after the launch (nearest {nearLater})");
    }

    [Fact]
    public void ACounterRequestMakesProductionFollowTheCounterMix()
    {
        const int ticks = 7000;
        var plain = new OrderRig();
        plain.Run(ticks);

        var led = new OrderRig();
        var fake = new FakeChatProvider();
        fake.EnqueueText(Reply("\"posture\":\"balanced\",\"focus\":\"army\",\"mix\":\"antiarmour\",\"counter_minutes\":10"));
        for (int i = 0; i < 20; i++) fake.EnqueueText(Reply("\"posture\":\"balanced\",\"focus\":\"army\",\"mix\":\"auto\""));   // the hold carries these
        using Commander c = Attach(led, fake);
        Run(led, c, ticks);

        Directive d = led.Sim.ViewFor(0).Directive;
        Assert.Equal(DirectiveCodec.MixAntiArmour, d.Mix);
        Assert.True(d.Strong);

        double Share(OrderRig r)
        {
            var bias = DirectiveBias.Plain with { Mix = DirectiveCodec.MixAntiArmour, MixPermille = 1000 };
            var rules = TestWorld.Rules.Sim;
            var own = new List<Rts.Sim.View.ViewActor>();
            r.Sim.ViewFor(0).CopyOwn(own);
            long score = 0; int n = 0;
            foreach (var a in own)
            {
                if (a.Kind == ActorKind.Infantry) { score += bias.CounterScore(rules, rules.Infantry[a.TypeIndex].PrimaryWeapon, rules.Infantry[a.TypeIndex].Cost); n++; }
                else if (a.Kind == ActorKind.Vehicle && rules.Vehicles[a.TypeIndex].HarvestCapacity <= 0)
                { score += bias.CounterScore(rules, rules.Vehicles[a.TypeIndex].PrimaryWeapon, rules.Vehicles[a.TypeIndex].Cost); n++; }
            }
            return n == 0 ? 0 : (double)score / n;
        }
        double sp = Share(plain), sl = Share(led);
        log.WriteLine($"mean anti-armour score per unit: plain {sp:0}, with the counter request {sl:0}");
        Assert.True(sl > sp, "the army follows the counter mix");
    }

    [Fact]
    public void ACommanderWithNoProviderLeavesTheGameBitIdentical()
    {
        var plain = new OrderRig();
        plain.Run(3500);
        var off = new OrderRig();
        using Commander c = Attach(off, null);
        off.Run(3500);
        Assert.Equal(plain.Sim.ComputeStateHash(), off.Sim.ComputeStateHash());
        Assert.Equal(plain.Collected.Count, off.Collected.Count);
        for (int i = 0; i < plain.Collected.Count; i++) Assert.Equal(plain.Collected[i], off.Collected[i]);
    }
}
