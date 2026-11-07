// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests;

/// <summary>S12: a mission passes its players' factions to the sim, so faction rules (Expedition beachhead, Left Behind supply) apply.</summary>
public sealed class MissionFactionsTests
{
    [Fact]
    public void An_all_authority_mission_keeps_the_old_setup()
    {
        MissionDef def = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, "auth_01"));
        Assert.Null(MissionSession.FactionsOf(def));
    }

    [Theory]
    [InlineData("exp_01", Faction.Expedition)]
    [InlineData("dir_01", Faction.Directorate)]
    public void The_human_player_plays_the_missions_faction(string id, Faction expected)
    {
        MissionDef def = MissionLoader.Load(MissionLoader.FolderOf(TestData.DataDir, id));
        IReadOnlyList<Faction>? f = MissionSession.FactionsOf(def);
        Assert.NotNull(f);
        int human = def.Players.ToList().FindIndex(p => p.Role == "human");
        Assert.Equal(expected, f![human]);
        Assert.All(def.Players.Where(p => p.Role != "human" && p.Faction.Length == 0), p => Assert.Equal(Faction.Authority, f[p.Slot]));
    }

    [Fact]
    public void An_expedition_mission_runs_with_the_expedition_faction_in_the_sim()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "exp_01");
        Assert.Equal(Faction.Expedition, m.Sim.Setup.FactionOf(0));
    }
}
