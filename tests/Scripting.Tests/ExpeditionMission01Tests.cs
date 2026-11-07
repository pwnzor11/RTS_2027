// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>The first Expedition mission (exp_01) loads cleanly, cannot be won by idling, and a <see cref="MissionBot"/> lands, builds and holds the beachhead.</summary>
public sealed class ExpeditionMission01Tests
{
    private const string Mission = "exp_01";
    private const int TickLimit = 20000;

    [Fact]
    public void Idle_player_does_not_win_and_the_script_runs_clean()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        var bot = new MissionBot(m);
        bot.Idle(3000);
        Assert.Empty(m.Host.Errors);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.Equal("expedition", m.Def.Faction);
        Assert.Equal(1, m.Def.Act);
        Assert.Equal(2, m.Def.Schema);
        Assert.Equal(3, m.Host.VisibleObjectives.Count());
        Assert.Equal(4, m.Host.Objectives.Count);   // plus the hidden "craft" bonus
        Assert.False(bot.Done("landing"));
    }

    [Fact]
    public void Idle_player_for_a_long_time_never_wins()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        new MissionBot(m).Idle(TickLimit);
        Assert.Empty(m.Host.Errors);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void A_bot_lands_builds_and_holds_the_beachhead(int tier)
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, Mission);
        m.Host.SetDifficulty(tier);
        var bot = new MissionBot(m);

        // 1. Deploy the Command Ship into the Landing Depot.
        bot.Send(CommandKind.Deploy, FlagshipId(m.Sim), new CellCoord(86, 82), 0);
        Assert.True(bot.RunUntilObjective("landing", 600), bot.Diagnostics());
        CellCoord depot = DepotCell(m.Sim);

        // 2. Power, billet, seawall, each beside the depot; then more marines.
        CellCoord[] spots = Around(depot, 7);
        Assert.True(bot.BuildAndPlace("exp_pile_house", spots), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("exp_billet", spots), bot.Diagnostics());
        Assert.True(bot.BuildAndPlace("exp_pillbox", spots), bot.Diagnostics());
        Assert.True(bot.RunUntilObjective("build", TickLimit), bot.Diagnostics());
        for (int i = 0; i < 4; i++) bot.Train("exp_marine");

        // 3. Hold: marines stand guard at the depot while the waves come.
        var hold = new CellCoord(depot.X + 1, depot.Y - 3);
        for (int guard = 0; m.Sim.Tick < TickLimit && !m.Sim.GameOver; guard++)
        {
            if (guard % 50 == 0) bot.OrderAll(CommandKind.AttackMove, ExpeditionMissionTestKit.EnemyNear(m.Sim, depot, 20) ?? hold);
            else bot.Idle();
        }

        Assert.Empty(m.Host.Errors);
        Assert.True(m.Host.Outcome == MissionOutcome.Won, bot.Diagnostics());
        Assert.True(m.Sim.Tick < TickLimit, bot.Diagnostics());
        Assert.All(m.Host.Objectives.Where(o => o.Primary), o => Assert.Equal(ObjectiveStatus.Completed, o.Status));
    }

    private static EntityId FlagshipId(Simulation s)
    {
        int t = SimRules.IndexOf(s.Setup.Rules.Vehicles, "exp_command_ship", v => v.Id);
        for (int i = 1; i < s.Units.Capacity; i++)
            if (s.Units.Slots.IsIndexAlive(i) && s.Units.Player[i] == 0 && s.Units.TypeIndex[i] == t) return s.Units.Slots.IdOf(i);
        throw new InvalidOperationException("no Command Ship");
    }

    private static CellCoord DepotCell(Simulation s)
    {
        int t = SimRules.IndexOf(s.Setup.Rules.Buildings, "exp_landing_depot", b => b.Id);
        for (int i = 1; i < s.Buildings.Capacity; i++)
            if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == 0 && s.Buildings.TypeIndex[i] == t) return s.Buildings.Cell[i];
        throw new InvalidOperationException("no Landing Depot");
    }

    /// <summary>Cells on rings around <paramref name="c"/> (radius 3 up to <paramref name="max"/>), north side first so the bot stays on land.</summary>
    private static CellCoord[] Around(CellCoord c, int max)
    {
        var list = new List<CellCoord>();
        for (int r = 3; r <= max; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && c.Y + dy >= 0) list.Add(new CellCoord(c.X + dx, c.Y + dy));
        list.Sort((a, b) => a.Y.CompareTo(b.Y));
        return list.ToArray();
    }
}
