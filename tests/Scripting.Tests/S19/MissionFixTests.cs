// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim;

namespace Rts.Scripting.Tests.S19;

/// <summary>S19 mission script fixes (S18 audit MIS-06 and following): one test per fix.</summary>
public sealed class MissionFixTests
{
    [Fact]
    public void Mis06_Lb01_deploy_objective_points_at_the_unpack_site_not_the_enemy_anchor()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "lb_01");
        TestData.Run(m.Sim, 2);
        MissionObjective deploy = m.Host.Objectives.Single(o => o.Id == "deploy");
        Assert.True(m.Host.TryObjectiveCell(deploy, out CellCoord c));
        Assert.True(c.X < 40 && c.Y < 30, $"deploy marker at ({c.X},{c.Y})");   // the camp zone, far from auth_power at (58,58)
    }

    [Fact]
    public void Mis08_Dir01_enemy_ai_defends_its_own_base_not_the_players_hatch_site()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_01");
        TestData.Run(m.Sim, 2);
        string zone = m.Host.AiHookOf(1).DefendZone;
        Assert.Equal("enemy_base", zone);
        MissionZone z = m.Def.ZoneNamed(zone)!.Value;
        MissionZone site = m.Def.ZoneNamed("site")!.Value;
        Assert.True(z.X0 > site.X1 && z.Y0 > site.Y1, "the defend zone must not overlap the hatch site");
    }

    /// <summary>Test-only direct kill of the building of this type and player at this cell.</summary>
    private static void Kill(MissionSession m, string typeId, int player, int x, int y)
    {
        var sim = m.Sim;
        int type = Rts.Sim.Rules.SimRules.IndexOf(sim.Setup.Rules.Buildings, typeId, b => b.Id);
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player && sim.Buildings.TypeIndex[i] == type
                && sim.Buildings.Cell[i].X == x && sim.Buildings.Cell[i].Y == y)
            {
                sim.Buildings.Health[i] = 0;
                return;
            }

        throw new InvalidOperationException("no such building");
    }

    [Fact]
    public void Mis09_Dir04_razing_the_command_post_before_the_charge_does_not_win()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_04");
        var bot = new MissionBot(m);
        bot.Idle(100);
        Kill(m, "auth_engineering", 1, 60, 65);   // a conventional kill, long before the Device is charged
        bot.Idle(60);
        Assert.Empty(m.Host.Errors);
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.NotEqual(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "fire").Status);
    }

    [Fact]
    public void Mis18_Dir04_says_charged_only_when_the_devices_real_charge_is_ready()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_04");
        var bot = new MissionBot(m);
        bot.Idle(2900);
        int type = Rts.Sim.Rules.SimRules.IndexOf(m.Sim.Setup.Rules.Buildings, "dir_sunrise", b => b.Id);
        int device = -1;
        for (int i = 1; i < m.Sim.Buildings.Capacity; i++)
            if (m.Sim.Buildings.Slots.IsIndexAlive(i) && m.Sim.Buildings.Player[i] == 0 && m.Sim.Buildings.TypeIndex[i] == type) device = i;
        Assert.True(device > 0);
        m.Sim.Effects.BuildingCharge[device] = 1000;   // as if a power loss had paused the charge
        bot.Idle(300);                                   // the 3050-tick timer has run out
        Assert.Equal("false", TestAccess.StateValue(m.Host, "charged"));
        Assert.NotEqual(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "hold").Status);

        m.Sim.Effects.BuildingCharge[device] = 1_000_000;   // the real charge is ready
        bot.Idle(60);
        Assert.Empty(m.Host.Errors);
        Assert.Equal("true", TestAccess.StateValue(m.Host, "charged"));
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "hold").Status);
    }

    /// <summary>Ticks until the first superweapon of <paramref name="player"/> is ready, from the sim's own view (0 when ready).</summary>
    private static int ChargeLeft(MissionSession m, byte player)
    {
        SuperweaponView v = m.Sim.HudFor(player).Superweapons.Single();
        return v.Ready ? 0 : v.Total - v.Charge;
    }

    [Fact]
    public void Mis10_Auth04_the_AI_owned_Device_is_held_back_to_the_scripted_sunrise_timer()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_04");
        var bot = new MissionBot(m);
        bot.Idle(3100);   // past the rule's own 3000-tick charge
        Assert.Empty(m.Host.Errors);
        int left = ChargeLeft(m, 1);
        Assert.InRange(left, 12800, 13000);   // 16000 - 3100 (the timer ends at 16000)
        Assert.Equal(16000, m.Host.Timers.Single(t => t.Id == "sunrise").EndTick);
    }

    [Fact]
    public void Mis10_Exp04_the_redoubt_Device_does_not_charge_during_the_mission()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "exp_04");
        var bot = new MissionBot(m);
        bot.Idle(3100);
        Assert.Empty(m.Host.Errors);
        Assert.True(ChargeLeft(m, 1) > 100_000);
    }

    [Fact]
    public void Mis14_Tut01_fight_text_names_no_outpost_that_does_not_exist()
    {
        string locale = File.ReadAllText(Path.Combine(TestData.DataDir, "missions", "tut_01", "locale.toml"));
        Assert.DoesNotContain("outpost", locale, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("south-east", locale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mis11_Auth04_killing_the_vent_pushes_the_sunrise_timer_and_the_Devices_charge_out_by_1500()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "auth_04");
        var bot = new MissionBot(m);
        bot.Idle(600);
        int endBefore = m.Host.Timers.Single(t => t.Id == "sunrise").EndTick;
        int chargeLeftBefore = ChargeLeft(m, 1);
        Kill(m, "dir_reactor_vent", 1, 51, 84);
        bot.Idle(40);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "vent").Status);
        Assert.Equal(endBefore + 1500, m.Host.Timers.Single(t => t.Id == "sunrise").EndTick);
        Assert.True(ChargeLeft(m, 1) > chargeLeftBefore + 1300, "the Device's real charge must be held back with the timer");
    }

}
