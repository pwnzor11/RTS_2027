// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Reflection;
using MoonSharp.Interpreter;

namespace Rts.Scripting.Tests.S18;

/// <summary>
/// S18 MIS-07: what a failed spawn does to a mission callback. Each test runs one shipped callback as a trigger (so errors are collected
/// exactly as in play) after crowding the spawn site with the player's own soldiers.
/// </summary>
public sealed class SpawnFailureTests
{
    private static Script ScriptOf(MissionHost host) =>
        (Script)typeof(MissionHost).GetField("_script", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;

    /// <summary>
    /// Before the first tick: wraps the mission's OnInit so that two ticks in, S18Go fills <paramref name="zone"/> with the player's riflemen (inside the
    /// tick, as script spawns must be), runs <paramref name="prepare"/> (Lua) and then schedules the shipped <paramref name="callback"/> as a trigger.
    /// </summary>
    private static void Arrange(MissionHost host, MissionZone zone, string prepare, string callback)
    {
        ScriptOf(host).DoString(
            "local s18init = OnInit\n"
            + "function OnInit() s18init(); Trigger.AfterDelay(2, S18Go) end\n"
            + "function S18Go()\n"
            + $"  for y = {zone.Y0}, {zone.Y1} do for x = {zone.X0}, {zone.X1} do Actor.Create('auth_rifleman', 0, {{ x = x, y = y }}) end end\n"
            + "  " + prepare + "\n"
            + $"  Trigger.AfterDelay(1, {callback})\n"
            + "end");
    }

    // S19 MIS-07: the yard airlift goes to the nearest legal site when a unit stands on the planned one (was Audit_..., which pinned the soft-lock).
    [Fact]
    public void Auth01_CheckRaids_with_a_unit_on_the_yard_site_still_gives_the_refinery_objective()
    {
        MissionSession m = PrepareCheckRaids();
        Assert.Empty(m.Host.Errors);
        Assert.Contains(m.Host.Objectives, o => o.Id == "refinery");
        Assert.True(m.Host.Objectives.Single(o => o.Id == "hold").Status == ObjectiveStatus.Completed);
        int yard = Rts.Sim.Rules.SimRules.IndexOf(m.Sim.Setup.Rules.Buildings, "auth_conyard", b => b.Id);
        int built = 0;
        for (int i = 1; i < m.Sim.Buildings.Capacity; i++)
            if (m.Sim.Buildings.Slots.IsIndexAlive(i) && m.Sim.Buildings.Player[i] == 0 && m.Sim.Buildings.TypeIndex[i] == yard) built++;
        Assert.Equal(1, built);   // the yard stands, beside the crowded site
    }

    private static MissionSession PrepareCheckRaids()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_01");
        // The 3x3 the airlifted conyard needs is crowded; the survey phase is set as CheckConvoy and both raids leave it ("hold" added).
        Arrange(m.Host, new MissionZone(23, 25, 25, 27),
            "state.phase = 'survey'; state.raids = 2; state.raidTags = {}; Objective.Add('hold', 'a1.obj.hold', true)", "CheckRaids");
        new MissionBot(m).Idle(6);
        return m;
    }

    [Fact]
    public void Lb04_Landing_into_a_crowded_zone_still_starts_the_fleet_timer()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "lb_04");
        m.Host.SetDifficulty(5);
        Arrange(m.Host, m.Def.ZoneNamed("landing")!.Value, "", "Landing");   // the player's soldiers stand in the landing zone
        new MissionBot(m).Idle(6);

        Assert.Equal("true", TestAccess.StateValue(m.Host, "landing"));
        Assert.Empty(m.Host.Errors);
        Assert.Contains(m.Host.Timers, t => t.Id == "fleet");   // the defeat timer exists, so the mission can still end
        Assert.Equal(16, TestAccess.StateValue(m.Host, "landingTags").Split(',').Length);
    }
}
