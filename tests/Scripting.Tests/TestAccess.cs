// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using MoonSharp.Interpreter;

namespace Rts.Scripting.Tests;

/// <summary>Reads script state for assertions without widening the host's public surface.</summary>
internal static class TestAccess
{
    /// <summary>The value of state[key] as text; a sequence table prints as comma-separated values; nil as "nil".</summary>
    public static string StateValue(MissionHost host, string key)
    {
        var script = (Script)typeof(MissionHost).GetField("_script", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(host)!;
        DynValue v = script.Globals.Get("state").Table.Get(key);
        if (v.Type == DataType.Table) return string.Join(",", v.Table.Values.Select(x => x.ToPrintString()));
        return v.Type == DataType.Nil ? "nil" : v.ToPrintString();
    }
}
