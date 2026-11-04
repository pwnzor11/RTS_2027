// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests.M3_12;

/// <summary>M3-12: a runaway Lua loop is stopped with a script error per callback; the simulation keeps running.</summary>
public sealed class InstructionBudgetTests
{
    [Fact]
    public void A_runaway_OnInit_is_stopped_with_a_budget_error_and_the_sim_keeps_running()
    {
        var (sim, host) = TestData.Tiny("function OnInit() while true do end end");
        TestData.Run(sim, 1);
        Assert.Contains(host.Errors, e => e.Contains("OnInit", StringComparison.Ordinal) && e.Contains("instruction budget", StringComparison.Ordinal));
        TestData.Run(sim, 3);   // the step carries on: no exception escapes
        Assert.Equal(4, sim.Tick);
    }

    [Fact]
    public void A_runaway_repeating_trigger_errors_each_time_it_fires()
    {
        var (sim, host) = TestData.Tiny("function Beat() while true do end end\nfunction OnInit() Trigger.Every(5, Beat) end");
        TestData.Run(sim, 12);   // fires at ticks 5 and 10
        int budgetErrors = host.Errors.Count(e => e.Contains("instruction budget", StringComparison.Ordinal));
        Assert.Equal(2, budgetErrors);
        Assert.Equal(12, sim.Tick);
    }

    [Fact]
    public void A_finite_busy_loop_well_under_the_budget_raises_no_error()
    {
        var (sim, host) = TestData.Tiny("function OnInit() local n = 0 for i = 1, 5000 do n = n + i end state.n = n end");
        TestData.Run(sim, 2);
        Assert.Empty(host.Errors);
        Assert.Equal("12502500", TestAccess.StateValue(host, "n"));
    }
}
