// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Sidebar;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Ui;

public sealed class SkirmishGameTests
{
    private static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found");

    [Fact]
    public void Map_description_names_the_curated_map_for_seed_zero()
    {
        Assert.Contains("curated", SkirmishGame.DescribeMap(0), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Generated", SkirmishGame.DescribeMap(77), StringComparison.Ordinal);
    }

    [Fact]
    public void Seed_zero_is_the_curated_map_with_a_yard_each_and_no_debug()
    {
        SkirmishGame g = SkirmishGame.Create(RulesDir, 0, 1);
        Assert.True(g.Curated);
        Assert.Equal("rustbelt_m2", g.Map.Name);
        Assert.False(g.Sim.Setup.AllowDebug);
        Assert.Equal(2, g.Sim.Setup.PlayerCount);
        Assert.Equal(1, g.Ai.Count);
        for (byte p = 0; p < 2; p++)
        {
            PlayerHud hud = g.Sim.HudFor(p);
            Assert.True(hud.Cash > 0);
            Assert.NotEmpty(hud.Options);
        }

        Assert.True(g.AiId is "skirmisher" or "recruit");
        Assert.Equal(AiRegistry.Contains("recruit") ? "recruit" : "skirmisher", g.AiId);
    }

    [Fact]
    public void Other_seeds_generate_a_map()
    {
        SkirmishGame g = SkirmishGame.Create(RulesDir, 5, 2);
        Assert.False(g.Curated);
        Assert.Equal(SkirmishGame.GeneratedMapSize, g.Map.Width);
    }

    [Fact]
    public void Idle_player_and_ai_run_without_exceptions()
    {
        SkirmishGame g = SkirmishGame.Create(RulesDir, 0, 1);
        var host = new SimHost(g.Sim) { Ai = g.Ai };
        host.StepTicks(400);
        Assert.Equal(400, g.Sim.Tick);
    }

    [Fact]
    public void Sidebar_options_drive_the_real_sim_build_then_place()
    {
        SkirmishGame g = SkirmishGame.Create(RulesDir, 0, 1);
        Simulation sim = g.Sim;
        var host = new SimHost(sim);   // no AI: only the human acts
        int power = SimRules.IndexOf(sim.Setup.Rules.Buildings, "auth_power", b => b.Id);

        PlayerHud hud = sim.HudFor(0);
        BuildOption opt = hud.Options.Single(o => o.Category == ProductionCategory.Structure && o.TypeIndex == power);
        Assert.Equal(CameoClick.Build, SidebarCommands.LeftClick(hud, opt));
        host.Issue(SidebarCommands.Build(0, opt)!.Value);
        host.StepTicks(2);
        Assert.Single(sim.HudFor(0).Queues.Single(q => q.Category == ProductionCategory.Structure).Items);

        // Right click on the cameo cancels it again.
        host.Issue(SidebarCommands.Cancel(0, sim.HudFor(0), opt)!.Value);
        host.StepTicks(2);
        Assert.Empty(sim.HudFor(0).Queues.Single(q => q.Category == ProductionCategory.Structure).Items);

        // Build it for real, then the tab is blocked except for the finished item, which places.
        host.Issue(SidebarCommands.Build(0, opt)!.Value);
        for (int i = 0; i < 2000 && SidebarCommands.ReadyType(sim.HudFor(0), ProductionCategory.Structure) != power; i++) host.StepTicks(1);
        hud = sim.HudFor(0);
        Assert.Equal(power, SidebarCommands.ReadyType(hud, ProductionCategory.Structure));
        BuildOption ready = hud.Options.Single(o => o.Category == ProductionCategory.Structure && o.TypeIndex == power);
        Assert.Equal(QueueBlockReason.StructureWaiting, ready.Blocked);
        Assert.Equal(CameoClick.EnterPlacement, SidebarCommands.LeftClick(hud, ready));

        CellCoord start = g.Map.StartPositions[0];
        CellCoord? spot = null;
        for (int dy = -6; dy <= 6 && spot is null; dy++)
            for (int dx = -6; dx <= 6 && spot is null; dx++)
            {
                var tl = new CellCoord(start.X + dx, start.Y + dy);
                if (sim.CanPlaceReady(0, power, tl)) spot = tl;
            }

        Assert.NotNull(spot);
        host.Issue(SidebarCommands.Place(0, power, spot!.Value));
        host.StepTicks(2);
        Assert.Equal(-1, SidebarCommands.ReadyType(sim.HudFor(0), ProductionCategory.Structure));
        Assert.True(sim.HudFor(0).PowerOut > 0);
    }

    [Fact]
    public void Host_runs_the_ai_before_each_step_and_issues_its_commands()
    {
        SkirmishGame g = SkirmishGame.Create(RulesDir, 0, 1);
        var host = new SimHost(g.Sim) { Ai = g.Ai };
        // The driver starts lazily on the first Collect (inside StepCore) and then keeps ticking.
        host.StepTicks(1);
        Assert.Equal(1, g.Sim.Tick);
        Assert.NotNull(host.Ai);
    }
}
