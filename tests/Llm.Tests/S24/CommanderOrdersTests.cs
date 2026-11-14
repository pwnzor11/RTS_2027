// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;
using Rts.Sim;
using Rts.Sim.Core;

namespace Llm.Tests;

/// <summary>S24 (M5-12): the reply's orders and the new mixes and strong flag, the commands they become, the recent-fights block and the
/// seeded random commander.</summary>
public class CommanderOrdersTests
{
    private static CommanderReply P(string json) => ReplyParser.Parse(json);

    // ---- parser ----------------------------------------------------------------------------------

    [Fact]
    public void Orders_parse_with_kind_cell_amount_and_minute()
    {
        var r = P("{\"orders\":[{\"slot\":1,\"kind\":\"attack\",\"cell\":[10,12],\"amount\":900,\"minute\":3}," +
                  "{\"slot\":2,\"kind\":\"hold\",\"cell\":[4,5]},{\"slot\":3,\"kind\":\"clear\"}]}");
        Assert.Equal(3, r.OrderList.Count);
        Assert.Equal(new OrderDraft(1, CommanderOrderKind.AttackAt, 10, 12, 900, 3), r.OrderList[0]);
        Assert.Equal(new OrderDraft(2, CommanderOrderKind.HoldCell, 4, 5, 0, 0), r.OrderList[1]);
        Assert.Equal(new OrderDraft(3, CommanderOrderKind.None, -1, -1, 0, 0), r.OrderList[2]);
    }

    [Fact]
    public void At_most_four_orders_are_kept_and_the_first_four_win()
    {
        var r = P("{\"orders\":[{\"slot\":0,\"kind\":\"reserve\",\"amount\":1},{\"slot\":1,\"kind\":\"reserve\",\"amount\":2}," +
                  "{\"slot\":2,\"kind\":\"reserve\",\"amount\":3},{\"slot\":3,\"kind\":\"reserve\",\"amount\":4},{\"slot\":0,\"kind\":\"reserve\",\"amount\":5}]}");
        Assert.Equal(4, r.OrderList.Count);
        Assert.Equal(4, r.OrderList[3].Amount);
    }

    [Fact]
    public void Bad_orders_are_dropped_one_by_one()
    {
        var r = P("{\"orders\":[{\"slot\":9,\"kind\":\"hold\",\"cell\":[1,1]},{\"slot\":0,\"kind\":\"warp\"}," +
                  "{\"slot\":0,\"kind\":\"attack\"},\"junk\",{\"slot\":0,\"kind\":\"reserve\",\"amount\":500}]}");
        Assert.Equal(new[] { new OrderDraft(0, CommanderOrderKind.Reserve, -1, -1, 500, 0) }, r.OrderList);
    }

    [Fact]
    public void Missing_or_wrong_orders_give_an_empty_list_never_null()
    {
        Assert.Empty(P("{}").OrderList);
        Assert.Empty(P("{\"orders\":\"none\"}").OrderList);
        Assert.Empty(P("").OrderList);
    }

    [Fact]
    public void Order_numbers_are_clamped_and_loose_values_accepted()
    {
        var r = P("{\"orders\":[{\"slot\":\"2\",\"kind\":\"tech\",\"amount\":9}," +
                  "{\"slot\":0,\"kind\":\"tech\",\"amount\":0}," +
                  "{\"slot\":0,\"kind\":\"attack\",\"cell\":[3,3],\"amount\":-40,\"minute\":99999}," +
                  "{\"slot\":1,\"kind\":\"raid\",\"cell\":[7,7],\"amount\":99999999}]}");
        Assert.Equal(new OrderDraft(2, CommanderOrderKind.TechTo, -1, -1, 5, 0), r.OrderList[0]);
        Assert.Equal(new OrderDraft(0, CommanderOrderKind.TechTo, -1, -1, 2, 0), r.OrderList[1]);
        Assert.Equal(new OrderDraft(0, CommanderOrderKind.AttackAt, 3, 3, 0, 600), r.OrderList[2]);
        Assert.Equal(new OrderDraft(1, CommanderOrderKind.Raid, 7, 7, CommanderOrderCodec.MaxAmount, 0), r.OrderList[3]);
    }

    [Fact]
    public void Kind_may_be_given_as_its_number()
    {
        Assert.Equal(new OrderDraft(1, CommanderOrderKind.Reserve, -1, -1, 300, 0),
            P("{\"orders\":[{\"slot\":1,\"kind\":6,\"amount\":\"300\"}]}").OrderList.Single());
        Assert.Empty(P("{\"orders\":[{\"slot\":1,\"kind\":7,\"amount\":300}]}").OrderList);
    }

    [Fact]
    public void Cell_kinds_without_a_cell_are_dropped_and_cell_kinds_ignore_extra_minutes()
    {
        var r = P("{\"orders\":[{\"slot\":0,\"kind\":\"expand\"},{\"slot\":0,\"kind\":\"expand\",\"cell\":[-1,2]},{\"slot\":1,\"kind\":\"raid\",\"cell\":[2,2],\"minute\":5}]}");
        Assert.Equal(new OrderDraft(1, CommanderOrderKind.Raid, 2, 2, 0, 0), r.OrderList.Single());
    }

    [Fact]
    public void Anti_armour_and_anti_infantry_mixes_and_the_strong_flag_parse()
    {
        Assert.Equal(new DirectiveDraft(1, 2, 255, 5, false, -1, -1, true),
            P("{\"directive\":{\"posture\":\"balanced\",\"focus\":\"army\",\"mix\":\"anti_armour\",\"strong_mix\":true}}").Directive);
        Assert.Equal(new DirectiveDraft(1, 2, 255, 6, false, -1, -1, false),
            P("{\"directive\":{\"posture\":\"balanced\",\"focus\":\"army\",\"mix\":\"Anti-Infantry\",\"strong_mix\":\"no\"}}").Directive);
        Assert.Equal((byte)5, P("{\"directive\":{\"mix\":\"antiarmor\"}}").Directive!.Value.Mix);
        Assert.Equal((byte)6, P("{\"directive\":{\"mix\":6}}").Directive!.Value.Mix);
        Assert.Equal((byte)6, P("{\"directive\":{\"mix\":99}}").Directive!.Value.Mix);   // out of range clamps to the last mix
        Assert.False(P("{\"directive\":{}}").Directive!.Value.StrongMix);
    }

    // ---- commands --------------------------------------------------------------------------------

    [Fact]
    public void Order_commands_are_the_codec_commands_with_a_sequence_per_order()
    {
        var reply = P("{\"orders\":[{\"slot\":1,\"kind\":\"attack\",\"cell\":[10,12],\"amount\":900,\"minute\":3},{\"slot\":2,\"kind\":\"tech\",\"amount\":4}]}");
        var list = new List<Command>();
        ushort seq = DirectiveCommands.OrderSequenceBase;
        reply.AddOrderCommands(list, 100, 0, ref seq);

        Assert.Equal(2, list.Count);
        Assert.Equal(CommanderOrderCodec.ToCommand(100, 0, 0x8000, CommanderOrderKind.AttackAt, 1, new CellCoord(10, 12), 900, 3 * 60 * 20), list[0]);
        Assert.Equal(CommanderOrderCodec.ToCommand(100, 0, 0x8001, CommanderOrderKind.TechTo, 2, Directive.NoCell, 4, 0), list[1]);
        Assert.Equal(3 * 60 * 20, list[0].Arg2);
        Assert.Equal((ushort)0x8002, seq);
        Assert.Equal((CommanderOrderKind.AttackAt, 1, 900), CommanderOrderCodec.Unpack(list[0].Arg));
    }

    [Fact]
    public void A_clear_order_builds_a_none_command_and_an_empty_reply_builds_nothing()
    {
        var list = new List<Command>();
        ushort seq = 1;
        P("{\"orders\":[{\"slot\":3,\"kind\":\"clear\"}]}").AddOrderCommands(list, 5, 0, ref seq);
        Assert.Equal((CommanderOrderKind.None, 3, 0), CommanderOrderCodec.Unpack(list.Single().Arg));

        ushort seq2 = 1;
        P("{\"say\":\"hi\"}").AddOrderCommands(list, 5, 0, ref seq2);
        Assert.Single(list);
        Assert.Equal(1, seq2);
    }

    [Fact]
    public void Directive_command_packs_the_strong_flag()
    {
        var strong = new DirectiveDraft(1, 2, 255, 6, false, -1, -1, true).ToCommand(50, 0, 1);
        var plain = new DirectiveDraft(1, 2, 255, 6, false, -1, -1, false).ToCommand(50, 0, 1);
        Assert.Equal(DirectiveCodec.PackStrong(DirectiveCodec.Pack(1, 2, 255, 6, false), true), strong.Arg);
        Assert.NotEqual(0, strong.Arg & DirectiveCodec.StrongMixBit);
        Assert.Equal(0, plain.Arg & DirectiveCodec.StrongMixBit);
    }

    // ---- recent fights block ---------------------------------------------------------------------

    [Fact]
    public void Recent_fights_block_appears_after_a_second_observation_and_reports_lost_army()
    {
        var (sim, h) = CommanderFightScene();
        // the scene observed once: no interval yet, so no block
        Assert.DoesNotContain("RECENT FIGHTS", BattlefieldSummary.Build(sim.ViewFor(0), h));

        int tankCost = SummaryRig.Rules.Sim.Vehicles.ToArray()[SummaryRig.Vehicle("auth_tank")].Cost;
        SummaryRig.Kill(sim, 0, sim.Units.Slots.IdOf(2), ActorKind.Vehicle);
        h.Observe(sim.ViewFor(0));
        string text = BattlefieldSummary.Build(sim.ViewFor(0), h);
        Assert.Contains("RECENT FIGHTS", text);
        Assert.Contains($"you lost {tankCost}cr", text);
        Assert.Contains("lost ground", text);
    }

    [Fact]
    public void Quiet_interval_reports_held_ground_and_the_history_keeps_three()
    {
        var (sim, h) = CommanderFightScene();
        for (int i = 0; i < 5; i++)
        {
            SummaryRig.Run(sim, 20);
            h.Observe(sim.ViewFor(0));
        }
        Assert.Equal(SummaryHistory.MaxFights, h.Fights.Count);
        Assert.All(h.Fights, f => Assert.False(f.LostGround));
        Assert.All(h.Fights, f => Assert.Equal(0, f.OwnLost));
        Assert.Contains("held ground", BattlefieldSummary.Build(sim.ViewFor(0), h));
    }

    /// <summary>The summary scene's base and army (a copy of the Scene in SummaryTests), observed once.</summary>
    private static (Simulation Sim, SummaryHistory History) CommanderFightScene()
    {
        var sim = SummaryRig.Sim();
        sim.PlaceBuilding(0, SummaryRig.Building("auth_conyard"), new CellCoord(5, 5));
        sim.PlaceBuilding(0, SummaryRig.Building("auth_power"), new CellCoord(5, 10));
        sim.SpawnVehicle(0, SummaryRig.Vehicle("auth_tank"), new CellCoord(14, 8));
        sim.SpawnVehicle(0, SummaryRig.Vehicle("auth_tank"), new CellCoord(15, 8));
        sim.SpawnInfantry(1, SummaryRig.Infantry("dir_conscript"), new CellCoord(18, 8));
        sim.PlaceBuilding(1, SummaryRig.Building("dir_shaft"), new CellCoord(55, 55));
        sim.Players.Cash[0] = 1000;
        SummaryRig.Run(sim, 20 * 60);
        var h = new SummaryHistory();
        h.Observe(sim.ViewFor(0));
        return (sim, h);
    }

    // ---- random commander ------------------------------------------------------------------------

    private static string Key(CommanderReply r) => r.Directive + "|" + string.Join(";", r.OrderList);

    [Fact]
    public void Random_commander_is_deterministic_for_a_seed_and_differs_between_seeds()
    {
        var a = new RandomCommanderSource(42, 64);
        var b = new RandomCommanderSource(42, 64);
        var c = new RandomCommanderSource(43, 64);
        var fromA = Enumerable.Range(0, 30).Select(_ => Key(a.Next())).ToList();
        var fromB = Enumerable.Range(0, 30).Select(_ => Key(b.Next())).ToList();
        var fromC = Enumerable.Range(0, 30).Select(_ => Key(c.Next())).ToList();
        Assert.Equal(fromA, fromB);
        Assert.NotEqual(fromA, fromC);
    }

    [Fact]
    public void Random_commander_stays_inside_the_parser_ranges()
    {
        var src = new RandomCommanderSource(7, 64);
        for (int i = 0; i < 200; i++)
        {
            CommanderReply r = src.Next();
            DirectiveDraft d = r.Directive!.Value;
            Assert.InRange(d.Posture, (byte)0, (byte)3);
            Assert.InRange(d.Focus, (byte)0, (byte)4);
            Assert.InRange(d.Mix, (byte)0, (byte)6);
            Assert.InRange(d.FocusX, (short)-1, (short)63);
            Assert.True(r.OrderList.Count <= CommanderOrderCodec.Slots);
            foreach (OrderDraft o in r.OrderList)
            {
                Assert.InRange(o.Slot, (byte)0, (byte)3);
                Assert.InRange(o.Amount, 0, CommanderOrderCodec.MaxAmount);
                if (o.Kind == CommanderOrderKind.TechTo) Assert.InRange(o.Amount, 2, 5);
            }
        }
    }
}
