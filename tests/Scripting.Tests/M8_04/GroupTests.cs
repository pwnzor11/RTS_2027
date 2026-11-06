// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.M8_04;

/// <summary>M8-04: Actor.CreateGroup places formations, applies veterancy and tag prefixes, and is deterministic.</summary>
public sealed class GroupTests
{
    private static string Script(string formation, string extra = "") => @"function OnInit()
  local t = Actor.CreateGroup({ 'auth_rifleman', 'auth_rifleman', 'auth_rifleman', 'auth_rifleman' }, 0, { x = 20, y = 15 }, { formation = '" + formation + "'" + extra + @" })
  state.n = #t
  for i, tag in ipairs(t) do local c = Actor.Cell(tag); state['x' .. i] = c.x; state['y' .. i] = c.y; state['t' .. i] = tag end
end";

    private static (int X, int Y)[] Cells(MissionHost h, int n) =>
        Enumerable.Range(1, n).Select(i => (int.Parse(TestAccess.StateValue(h, "x" + i)), int.Parse(TestAccess.StateValue(h, "y" + i)))).ToArray();

    [Fact]
    public void Line_spreads_along_x_column_along_y_and_box_fills_a_square_around_the_cell()
    {
        var (s1, h1, _) = World.Build(Script("line"));
        TestData.Run(s1, 1);
        Assert.Empty(h1.Errors);
        (int X, int Y)[] line = Cells(h1, 4);
        Assert.All(line, c => Assert.Equal(15, c.Y));
        Assert.Equal(4, line.Select(c => c.X).Distinct().Count());

        var (s2, h2, _) = World.Build(Script("column"));
        TestData.Run(s2, 1);
        (int X, int Y)[] col = Cells(h2, 4);
        Assert.All(col, c => Assert.Equal(20, c.X));
        Assert.Equal(4, col.Select(c => c.Y).Distinct().Count());

        var (s3, h3, _) = World.Build(Script("box"));
        TestData.Run(s3, 1);
        (int X, int Y)[] box = Cells(h3, 4);
        Assert.Equal(4, box.Distinct().Count());
        Assert.All(box, c => { Assert.InRange(c.X, 19, 21); Assert.InRange(c.Y, 14, 16); });
        Assert.Equal(2, box.Select(c => c.X).Distinct().Count());
        Assert.Equal(2, box.Select(c => c.Y).Distinct().Count());
    }

    [Fact]
    public void Scatter_stays_within_three_cells_uses_distinct_cells_and_repeats_exactly()
    {
        var (s1, h1, _) = World.Build(Script("scatter"));
        var (s2, h2, _) = World.Build(Script("scatter"));
        TestData.Run(s1, 1); TestData.Run(s2, 1);
        Assert.Empty(h1.Errors);
        (int X, int Y)[] a = Cells(h1, 4);
        Assert.Equal(4, a.Distinct().Count());
        Assert.All(a, c => { Assert.InRange(c.X, 15, 25); Assert.InRange(c.Y, 10, 20); });
        Assert.Equal(a, Cells(h2, 4));
        Assert.Equal(s1.ComputeStateHash(), s2.ComputeStateHash());
    }

    [Fact]
    public void Rank_sets_the_veterancy_level_of_every_member_and_zero_leaves_recruits()
    {
        var (s1, h1, _) = World.Build(Script("line", ", rank = 2"));
        TestData.Run(s1, 1);
        Assert.Empty(h1.Errors);
        int ranked = 0;
        for (int i = 1; i < s1.Infantry.Capacity; i++)
            if (s1.Infantry.Slots.IsIndexAlive(i) && s1.Infantry.Player[i] == 0) { Assert.Equal(2, s1.RankOf(ActorKind.Infantry, i)); ranked++; }
        Assert.Equal(4, ranked);

        var (s2, _, _) = World.Build("function OnInit() Actor.CreateGroup({ 'auth_jeep', 'auth_tank' }, 0, { x = 20, y = 15 }, { rank = 3 }) end");
        TestData.Run(s2, 1);
        int vehicles = 0;
        for (int i = 1; i < s2.Units.Capacity; i++)
            if (s2.Units.Slots.IsIndexAlive(i) && s2.Units.Player[i] == 0) { Assert.Equal(3, s2.RankOf(ActorKind.Vehicle, i)); vehicles++; }
        Assert.Equal(2, vehicles);

        var (s3, _, _) = World.Build(Script("line", ", rank = 0"));
        TestData.Run(s3, 1);
        for (int i = 1; i < s3.Infantry.Capacity; i++)
            if (s3.Infantry.Slots.IsIndexAlive(i)) Assert.Equal(0, s3.RankOf(ActorKind.Infantry, i));
    }

    [Fact]
    public void Tag_prefix_names_the_members_and_a_second_group_with_the_same_prefix_is_an_error_that_spawns_nothing()
    {
        const string Lua = @"function OnInit()
  state.t = Actor.CreateGroup({ 'auth_rifleman', 'auth_rifleman' }, 0, { x = 20, y = 15 }, { tagPrefix = 'sq' })
  state.alive = tostring(Actor.Alive('sq1')) .. tostring(Actor.Alive('sq2')) .. tostring(Actor.Alive('sq3'))
  Trigger.AfterDelay(1, Again)
end
function Again() Actor.CreateGroup({ 'auth_rifleman' }, 0, { x = 25, y = 15 }, { tagPrefix = 'sq' }) end";
        var (sim, host, _) = World.Build(Lua);
        TestData.Run(sim, 4);
        Assert.Single(host.Errors);
        Assert.Contains("Actor.CreateGroup", host.Errors[0]);
        Assert.Contains("already in use", host.Errors[0]);
        Assert.Equal("sq1,sq2", TestAccess.StateValue(host, "t"));
        Assert.Equal("truetruefalse", TestAccess.StateValue(host, "alive"));
        Assert.Equal(2, World.Count(sim, 0, "auth_rifleman"));
    }

    [Fact]
    public void Members_never_share_a_cell_even_when_the_wanted_cells_are_taken()
    {
        const string Lua = "function OnInit() Actor.CreateGroup({ 'auth_jeep','auth_jeep','auth_jeep','auth_jeep','auth_jeep' }, 0, { x = 20, y = 15 }, { formation = 'line' }); Actor.CreateGroup({ 'auth_jeep','auth_jeep','auth_jeep' }, 0, { x = 20, y = 15 }, { formation = 'line' }) end";
        var (sim, host, _) = World.Build(Lua);
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        var cells = new HashSet<CellCoord>();
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i)) Assert.True(cells.Add(sim.Units.Position[i].ToCell()));
        Assert.Equal(8, cells.Count);
    }

    [Fact]
    public void A_group_survives_a_save_and_the_run_continues_identically()
    {
        string lua = Script("box", ", rank = 1") + "\nfunction Later() end";
        var (a, ha, def) = World.Build(lua);
        TestData.Run(a, 40);
        (Simulation b, _) = World.Reload(a, def, lua, Faction.Authority, false);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        TestData.Run(a, 200); TestData.Run(b, 200);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
    }
}
