// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

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
}
