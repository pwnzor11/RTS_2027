// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Data;
using Rts.Game;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Game.Ui.Spectator;
using Rts.Sim;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Spectator;

public sealed class SpectatorUiTests
{
    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found");

    private static SimHost RunningHost(int ticks)
    {
        SimHost host = SpectatorSetup.CreateHost(SpectatorSetup.Create(RulesDir, 5, 1));
        host.StepTicks(ticks);
        return host;
    }

    [Fact]
    public void Stats_rows_equal_StatsOf_and_TierOf()
    {
        SimHost host = RunningHost(600);
        IReadOnlyList<SpectatorRow> rows = SpectatorTable.Collect(host.Sim);
        Assert.Equal(2, rows.Count);
        foreach (SpectatorRow r in rows)
        {
            Assert.Equal(host.Sim.StatsOf((byte)r.Player), r.Stats);
            Assert.Equal(host.Sim.TierOf((byte)r.Player), r.Tier);
            Assert.Equal(host.Sim.Players.Cash[r.Player], r.Cash);
            Assert.Equal(host.Sim.Players.Defeated[r.Player], r.Defeated);
        }
    }

    [Fact]
    public void Army_value_and_structures_match_an_independent_count_from_the_full_snapshot()
    {
        SimHost host = RunningHost(600);
        host.SetViewer(-1);
        SimSnapshot snap = host.Current;
        var rules = host.Sim.Setup.Rules;
        foreach (SpectatorRow r in SpectatorTable.Collect(host.Sim))
        {
            int army = 0;
            foreach (UnitView u in snap.Units)
                if (u.Player == r.Player) army += u.Kind == UnitKind.Vehicle ? rules.Vehicles[u.TypeIndex].Cost : rules.Infantry[u.TypeIndex].Cost;
            int structures = snap.Buildings.Count(b => b.Player == r.Player && !b.Ghost);
            Assert.Equal(army, r.ArmyValue);
            Assert.Equal(structures, r.Structures);
        }
    }

    [Fact]
    public void Compact_and_final_lines_name_every_player()
    {
        SimHost host = RunningHost(100);
        IReadOnlyList<SpectatorRow> rows = SpectatorTable.Collect(host.Sim);
        IReadOnlyList<string> final = SpectatorTable.Final(rows);
        Assert.Equal(rows.Count + 1, final.Count);
        Assert.Equal(SpectatorTable.DetailHeader, final[0]);
        Assert.StartsWith("P1", final[1]);
        Assert.StartsWith("P2", final[2]);
        Assert.StartsWith("P1", SpectatorTable.Compact(rows[0]));
    }

    [Fact]
    public void Defeated_rows_are_marked()
    {
        var row = new SpectatorRow(1, 1, Rts.Sim.Rules.Faction.Authority, 1, 0, 0, 0, 0, 0, 0, 0, default, 0, true);
        Assert.EndsWith("OUT", SpectatorTable.Compact(row));
        Assert.EndsWith("OUT", SpectatorTable.Detail(row));
    }

    [Fact]
    public void Income_per_minute_comes_from_the_last_sixty_seconds_of_samples()
    {
        var income = new IncomeTracker();
        Assert.Equal(0, income.IncomePerMinute(0));
        // 10 credits per second for player 0, 0 for player 1, for 90 seconds.
        for (int s = 0; s <= 90; s++) income.Observe(s * 20, new[] { s * 10, 0 });
        Assert.Equal(600, income.IncomePerMinute(0));
        Assert.Equal(0, income.IncomePerMinute(1));
        Assert.Equal(0, income.IncomePerMinute(5));
    }

    [Fact]
    public void Income_ignores_a_repeated_tick()
    {
        var income = new IncomeTracker();
        income.Observe(0, new[] { 0 });
        income.Observe(20, new[] { 10 });
        income.Observe(20, new[] { 999 });
        Assert.Equal(600, income.IncomePerMinute(0));
    }

    [Fact]
    public void Inspect_picks_the_nearest_unit_of_any_player()
    {
        UnitView a = new(new EntityId(1, 1), UnitKind.Infantry, 0, 0, new LeptonCoord(0, 0), 0, 10);
        UnitView b = new(new EntityId(2, 1), UnitKind.Vehicle, 1, 0, new LeptonCoord(0, 0), 0, 10);
        var units = new[] { new RenderUnit(a, new Vector2(100, 100)), new RenderUnit(b, new Vector2(110, 100)) };
        Assert.Equal(b.Id, SpectatorInspect.PickUnit(units, new Vector2(111, 100))!.Value.Id);
        Assert.Null(SpectatorInspect.PickUnit(units, new Vector2(900, 900)));
    }

    [Fact]
    public void Inspect_picks_a_structure_by_footprint()
    {
        var b = new BuildingView(new EntityId(3, 1), 1, 0, new CellCoord(4, 4), 2, 2, 50, false);
        var list = new[] { b };
        Assert.Equal(b.Id, SpectatorInspect.PickBuilding(list, new CellCoord(5, 5))!.Value.Id);
        Assert.Null(SpectatorInspect.PickBuilding(list, new CellCoord(6, 4)));
    }

    [Fact]
    public void Game_over_headline_names_the_winning_team()
    {
        Assert.Equal("Team 1 wins", GameOverScreen.SpectatorHeadline(0));
        Assert.Equal("Team 2 wins", GameOverScreen.SpectatorHeadline(1));
        Assert.Equal("Draw", GameOverScreen.SpectatorHeadline(-1));
    }
}
