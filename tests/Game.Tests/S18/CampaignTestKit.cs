// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Reflection;
using Rts.Scripting;
using Rts.Sim;

namespace Rts.Game.Tests.S18;

/// <summary>S18 test helpers for the campaign tests.</summary>
internal static class CampaignTestKit
{
    /// <summary>Ends the mission as a script win would (Mission.Win(0)), through the host's own code path. The host must have run at least one tick.</summary>
    public static void ForceWin(MissionHost host, Simulation sim)
    {
        Assert.True(sim.Tick > 0, "run a tick first so the host is bound to the sim");
        MethodInfo end = typeof(MissionHost).GetMethod("EndMission", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("MissionHost.EndMission not found");
        end.Invoke(host, new object[] { 0, true });
    }

    /// <summary>Runs one of the mission script's own top-level functions as a trigger callback would (errors go to <see cref="MissionHost.Errors"/>).</summary>
    public static void RunScriptCallback(MissionHost host, string name)
    {
        var script = (MoonSharp.Interpreter.Script)typeof(MissionHost).GetField("_script", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(host)!;
        MethodInfo invoke = typeof(MissionHost).GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Instance)!;
        invoke.Invoke(host, new object[] { name, script.Globals.Get(name) });
    }
}
