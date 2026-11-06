// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;

namespace Rts.Scripting.Tests.M8_03;

/// <summary>Save a running test mission and load it into a fresh host.</summary>
internal static class SaveLoad
{
    public static Simulation Reload(Simulation sim, MissionDef def, string lua, MissionHost freshHost)
    {
        using var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        return Simulation.Load(ms, MissionSession.MakeSetup(def, Rts.Data.MapFile.Load(Path.Combine(TestData.DataDir, "maps", def.MapFile)),
            Rts.Data.RulesLoader.Load(Path.Combine(TestData.DataDir, "rules")), freshHost));
    }

    /// <summary>Runs <paramref name="total"/> ticks straight, and again with a save and load after <paramref name="saveAt"/>; returns both final hashes.</summary>
    public static (ulong Straight, ulong Reloaded, MissionHost StraightHost, MissionHost ReloadedHost) Compare(string lua, int saveAt, int total)
    {
        var (s1, h1) = TestData.Tiny(lua);
        TestData.Run(s1, total);

        var (s2, h2) = TestData.Tiny(lua);
        TestData.Run(s2, saveAt);
        var host = new MissionHost(h2.Def, lua);
        Simulation loaded = Reload(s2, h2.Def, lua, host);
        TestData.Run(loaded, total - saveAt);
        return (s1.ComputeStateHash(), loaded.ComputeStateHash(), h1, host);
    }
}
